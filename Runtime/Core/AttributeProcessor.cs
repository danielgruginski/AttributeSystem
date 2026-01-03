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

        // REMOVED: _pointers dictionary.
        // Pointers are now first-class citizens (PointerAttribute instances) inside _attributes.
        //private Dictionary<SemanticKey, SemanticKey> _pointers = new Dictionary<SemanticKey, SemanticKey>();

        private readonly Dictionary<SemanticKey, AttributeProcessor> _externalProviders = new();
        private readonly Subject<SemanticKey> _onProviderRegistered = new();

        private readonly AttributeTagManager _tagManager = new AttributeTagManager();

        /// <summary>
        /// Exposes the tags as a read-only reactive collection.
        /// </summary>
        public IReadOnlyReactiveDictionary<SemanticKey, int> Tags => _tagManager.Tags;

        public void AddTag(SemanticKey tag) => _tagManager.AddTag(tag);

        public void RemoveTag(SemanticKey tag) => _tagManager.RemoveTag(tag);

        public bool HasTag(SemanticKey tag) => _tagManager.HasTag(tag);

        // --- Pointer Management ---

        public void SetPointer(SemanticKey alias, SemanticKey target, List<SemanticKey> path = null)
        {
            if (alias == target && (path == null || path.Count == 0))
            {
                Debug.LogWarning($"[AttributeProcessor] Cannot point alias '{alias}' to itself.");
                return;
            }

            if (IsLocallyCircular(alias, target))
            {
                Debug.LogError($"[AttributeProcessor] Circular pointer detected: {alias} -> {target}");
                return;
            }

            // TODO: Test if an external provider chain would cause a cycle too.


            // Check collision with existing attribute
            if (_attributes.TryGetValue(alias, out var existing))
            {
                if (existing is PointerAttribute pointer)
                {
                    // If it's already a pointer, just retarget it
                    pointer.SetTarget(target, path);
                    return;
                }
                else
                {
                    // It's a concrete attribute. We are overwriting it with a pointer.
                    // This effectively "deletes" the old attribute and replaces it with an alias.
                    Debug.LogWarning($"[AttributeProcessor] Overwriting concrete attribute '{alias}' with a Pointer to '{target}'. Previous data lost.");
                    existing.Dispose();
                    _attributes.Remove(alias);
                }
            }

            // Create new pointer
            var newPointer = new PointerAttribute(alias, target, this, path);
            _attributes[alias] = newPointer;
        }

        public void RemovePointer(SemanticKey alias)
        {
            if (_attributes.TryGetValue(alias, out var attr) && attr is PointerAttribute pointer)
            {
                // Remove from dictionary FIRST so observers (like dependent pointers)
                // get the notification and unhook/fallback BEFORE we kill the object.
                _attributes.Remove(alias);
                pointer.Dispose();
            }
        }

        private bool IsLocallyCircular(SemanticKey alias, SemanticKey target)
        {
            // We are about to set Alias -> Target.
            // Check if Target eventually points back to Alias.
            var currentKey = target;
            int safeguard = 0;

            while (_attributes.TryGetValue(currentKey, out var attr) && attr is PointerAttribute ptr)
            {

                currentKey = ptr.TargetKey;

                if (currentKey == alias) return true;
                if (++safeguard > 100) return true;
            }
            return false;
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

        }
        /// <summary>
        /// Unregisters a provider, causing any dependent Connections to drop their modifiers.
        /// </summary>
        public void UnregisterExternalProvider(SemanticKey key)
        {
            if (_externalProviders.ContainsKey(key))
            {
                _externalProviders.Remove(key);
                _onProviderRegistered.OnNext(key); // Notify Connections (will resolve to null)
            }
        }
        /// <summary>
        /// Reactive stream that fires the new Processor whenever the provider for 'key' changes.
        /// Used by AttributeConnection to track topology changes.
        /// </summary>
        public IObservable<AttributeProcessor> ObserveProvider(SemanticKey key)
        {
            return _onProviderRegistered
                .Where(k => k.Equals(key))
                .StartWith(key) // Check immediately
                .Select(_ => _externalProviders.TryGetValue(key, out var p) ? p : null)
                .DistinctUntilChanged();
        }


        public IObservable<Attribute> GetAttributeObservable(SemanticKey attributeName, List<SemanticKey> providerPath = null)
        {
            // Base Case: No path means local attribute
            if (providerPath == null || providerPath.Count == 0)
            {
                // Resolve pointer locally before observing
                return GetLocalAttributeObservable(attributeName);
            }

            // Recursive Step: Look for the first provider in the list
            SemanticKey nextProviderKey = providerPath[0];
            var remainingPath = providerPath.Count > 1 ? providerPath.GetRange(1, providerPath.Count - 1) : new List<SemanticKey>();

            // Recursive Read is still useful for simple reads
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
        public void SetOrUpdateBaseValue(SemanticKey key, float value)
        {
            // GetAttribute automatically resolves aliases now
            var attr = GetOrCreateAttribute(key);
            // Calling SetBaseValue is polymorphic:
            // - Concrete: Sets internal value
            // - Pointer: Redirects to target
            attr.SetBaseValue(value);
        }

        public IDisposable AddModifier(string sourceId, IAttributeModifier modifier, SemanticKey attributeName)
            => AddModifier(sourceId, modifier, attributeName, null);

        /// <summary>
        /// Adds a modifier, traversing the provider path if necessary.
        /// Handles queuing for missing providers automatically.
        /// </summary>
        public IDisposable AddModifier(string sourceId, IAttributeModifier modifier, SemanticKey attributeName, List<SemanticKey> providerPath)
        {
            if (providerPath == null || providerPath.Count == 0)
            {
                // Local add
                var attr = GetOrCreateAttribute(attributeName, 0f);
                // Polymorphic AddModifier:
                // - Concrete: Adds to modifiers list
                // - Pointer: Adds to local list AND proxies to target
                return attr.AddModifier(modifier);
            }
            else
            {
                // Remote add -> Create Connection
                // The connection handles the lifecycle, path traversal, and implements IDisposable.
                return new AttributeConnection(this, providerPath, attributeName, modifier, sourceId);
            }
        }
    }
}