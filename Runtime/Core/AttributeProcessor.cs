using SemanticKeys;
using System;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Core
{
    /// <summary>
    /// The central manager for character attributes.
    /// Handles storage, retrieval, and modification of attributes.
    /// Now supports Lazy Resolution for modifiers on missing providers.
    /// </summary>
    public class AttributeProcessor
    {

        // The core storage. ReactiveDictionary allows other systems to listen for adds/removes.
        private readonly ReactiveDictionary<SemanticKey, Attribute> _attributes = new();
        public IReadOnlyReactiveDictionary<SemanticKey, Attribute> Attributes => _attributes;

        private readonly Dictionary<SemanticKey, AttributeProcessor> _externalProviders = new();
        private readonly Subject<SemanticKey> _onProviderRegistered = new();


        // Stores modifiers waiting for a specific provider (Key = ProviderName)
        // Key: The NEXT provider in the chain we are waiting for.
        private readonly Dictionary<SemanticKey, List<PendingModifier>> _pendingModifiers = new();

        private struct PendingModifier
        {
            public string SourceId;
            public IAttributeModifier Modifier;
            public SemanticKey TargetAttribute;
            public List<SemanticKey> RemainingPath; // The rest of the path after the current step
        }
        // --------------------------

        /// <summary>
        /// Links an external processor to a key (e.g., Registering the Player as 'Owner' for a Sword).
        /// </summary>
        public void RegisterExternalProvider(SemanticKey key, AttributeProcessor processor)
        {
            Debug.Assert(processor != null, $"[AttributeProcessor] Trying to register a null provider for key: {key}");
            _externalProviders[key] = processor;
            _onProviderRegistered.OnNext(key);

            // Flush pending modifiers waiting for this provider
            if (_pendingModifiers.TryGetValue(key, out var pendingList))
            {
                foreach (var req in pendingList)
                {
                    // Forward the modifier to the next step
                    // If RemainingPath is empty, it adds locally on that processor.
                    // If not, that processor will route it or queue it again.
                    processor.AddModifier(req.SourceId, req.Modifier, req.TargetAttribute, req.RemainingPath);
                }
                _pendingModifiers.Remove(key);
            }
        }

        public IObservable<Attribute> GetAttributeObservable(SemanticKey attributeName, List<SemanticKey> providerPath = null)
        {
            // Base Case: No path means local attribute
            if (providerPath == null || providerPath.Count == 0)
            {
                return GetLocalAttributeObservable(attributeName);
            }

            // Recursive Step: Look for the first provider in the list
            SemanticKey nextProviderKey = providerPath[0];
            var remainingPath = providerPath.Count > 1 ? providerPath.GetRange(1, providerPath.Count - 1) : new List<SemanticKey>();

            return _onProviderRegistered
                .StartWith(_externalProviders.ContainsKey(nextProviderKey) ? nextProviderKey : SemanticKey.None)
                .Where(k => k == nextProviderKey)
                .Take(1)
                .SelectMany(_ => _externalProviders[nextProviderKey].GetAttributeObservable(attributeName, remainingPath));
        }

        private IObservable<Attribute> GetLocalAttributeObservable(SemanticKey name)
        {
            return Observable.Create<Attribute>(observer =>
            {
                if (_attributes.TryGetValue(name, out var attr))
                {
                    observer.OnNext(attr);
                    observer.OnCompleted();
                }
                else
                {
                    return OnAttributeAdded
                        .Where(addedAttr => addedAttr.Name == name)
                        .Take(1)
                        .Subscribe(observer);
                }
                return Disposable.Empty;
            });
        }

        /*private IObservable<Attribute> GetExternalAttributeObservable(string providerKey, string attributeName)
        {
            return _onProviderRegistered
                .StartWith(_externalProviders.ContainsKey(providerKey) ? providerKey : null)
                .Where(k => k == providerKey)
                .Take(1)
                .SelectMany(_ => _externalProviders[providerKey].GetAttributeObservable(attributeName));
        }*/

        /// <summary>
        /// A reactive stream that fires whenever a new Attribute is added.
        /// Bridges can use this to subscribe to attributes that may not exist yet.
        /// </summary>
        public IObservable<Attribute> OnAttributeAdded => _attributes.ObserveAdd().Select(evt => evt.Value);

        public Attribute GetAttribute(SemanticKey name) => GetAttribute(name, null);

        public Attribute GetAttribute(SemanticKey name, List<SemanticKey> providerPath)
        {
            if (providerPath == null || providerPath.Count == 0)
            {
                return _attributes.TryGetValue(name, out var attr) ? attr : null;
            }

            SemanticKey nextKey = providerPath[0];
            if (_externalProviders.TryGetValue(nextKey, out var provider))
            {
                var remaining = providerPath.Count > 1 ? providerPath.GetRange(1, providerPath.Count - 1) : null;
                return provider.GetAttribute(name, remaining);
            }
            return null;
        }

        /// <summary>
        /// Safely gets an attribute, creating it with a default base value if it's missing.
        /// This is ideal for consumers (bridges, UI) that need to read an attribute's final value
        /// without necessarily setting its base.
        /// </summary>
        public Attribute GetOrCreateAttribute(SemanticKey name, float defaultBaseIfMissing = 0f)
        {
            // GetOrCreate only works locally for safety
            if (!_attributes.TryGetValue(name, out var attr))
            {
                attr = new Attribute(name, defaultBaseIfMissing, this);
                _attributes[name] = attr;
            }
            return attr;
        }

        /// <summary>
        /// Explicitly sets or updates the base value of an attribute. This is the designated
        /// method for establishing a character's foundational stats.
        /// Currently GetAttribute(...) will autogenerate the Attribute if missing.
        /// </summary>
        public void SetOrUpdateBaseValue(SemanticKey attributeName, float newBase)
        {
            var attr = GetOrCreateAttribute(attributeName);
            attr.SetBaseValue(newBase);
        }

        public void AddModifier(string sourceId, IAttributeModifier modifier, SemanticKey attributeName)
            => AddModifier(sourceId, modifier, attributeName, null);

        /// <summary>
        /// Adds a modifier, traversing the provider path if necessary.
        /// Handles queuing for missing providers automatically.
        /// </summary>
        public void AddModifier(string sourceId, IAttributeModifier modifier, SemanticKey attributeName, List<SemanticKey> providerPath)
        {
            // Base Case: Local Add
            if (providerPath == null || providerPath.Count == 0)
            {
                var attr = GetOrCreateAttribute(attributeName, 0f);
                attr.AddModifier(modifier);
                return;
            }

            // Recursive Step
            SemanticKey nextKey = providerPath[0];
            var remaining = providerPath.Count > 1 ? providerPath.GetRange(1, providerPath.Count - 1) : new List<SemanticKey>();

            if (_externalProviders.TryGetValue(nextKey, out var provider))
            {
                provider.AddModifier(sourceId, modifier, attributeName, remaining);
            }
            else
            {
                // Queue it
                if (!_pendingModifiers.ContainsKey(nextKey))
                    _pendingModifiers[nextKey] = new List<PendingModifier>();

                _pendingModifiers[nextKey].Add(new PendingModifier
                {
                    SourceId = sourceId,
                    Modifier = modifier,
                    TargetAttribute = attributeName,
                    RemainingPath = remaining
                });
            }
        }

        /// <summary>
        /// Removes modifiers and triggers their Detach/Dispose lifecycle.
        /// </summary>
        public void RemoveModifiersBySource(string sourceId)
        {
            foreach (var attribute in _attributes.Values)
            {
                // We find all modifiers matching the ID and remove them
                var toRemove = attribute.Modifiers.Where(m => m.SourceId == sourceId).ToList();
                foreach (var mod in toRemove)
                {
                    attribute.RemoveModifier(mod);
                }
            }

            // Note: We might also want to clean up pending modifiers if the source is removed before the provider arrives.
            foreach (var key in _pendingModifiers.Keys.ToList())
            {
                _pendingModifiers[key].RemoveAll(p => p.SourceId == sourceId);
            }
        }
    }
}