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
        private const char SEPARATOR = '.';

        // The core storage. ReactiveDictionary allows other systems to listen for adds/removes.
        private readonly ReactiveDictionary<string, Attribute> _attributes = new();
        public IReadOnlyReactiveDictionary<string, Attribute> Attributes => _attributes;

        private readonly Dictionary<string, AttributeProcessor> _externalProviders = new();
        private readonly Subject<string> _onProviderRegistered = new();

        // --- NEW: Pending Queue ---
        // Stores modifiers waiting for a specific provider (Key = ProviderName)
        private readonly Dictionary<string, List<PendingModifier>> _pendingModifiers = new();

        private struct PendingModifier
        {
            public string SourceId;
            public IAttributeModifier Modifier;
            public string AttributeName; // The target attribute name on that provider
        }
        // --------------------------

        /// <summary>
        /// Links an external processor to a key (e.g., Registering the Player as 'Owner' for a Sword).
        /// </summary>
        public void RegisterExternalProvider(string key, AttributeProcessor processor)
        {
            Debug.Assert(processor != null, $"[AttributeProcessor] Trying to register a null provider for key: {key}");
            _externalProviders[key] = processor;
            _onProviderRegistered.OnNext(key);

            // Check if anyone was waiting for this provider
            if (_pendingModifiers.TryGetValue(key, out var pendingList))
            {
                foreach (var req in pendingList)
                {
                    // Apply to the newly registered processor
                    processor.AddModifier(req.SourceId, req.Modifier, req.AttributeName);
                }
                _pendingModifiers.Remove(key);
            }
        }

        /// <summary>
        /// Gets an observable for an attribute. Supports dot notation (e.g., "Owner.Strength").
        /// </summary>
        public IObservable<Attribute> GetAttributeObservable(string fullName)
        {
            if (fullName.Contains(SEPARATOR))
            {
                var parts = fullName.Split(SEPARATOR);
                if (parts.Length == 2)
                {
                    return GetExternalAttributeObservable(parts[0], parts[1]);
                }
                Debug.LogError($"[AttributeProcessor] Invalid attribute format: {fullName}. Expected 'Provider.Attribute'");
            }

            return GetLocalAttributeObservable(fullName);
        }

        private IObservable<Attribute> GetLocalAttributeObservable(string name)
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

        private IObservable<Attribute> GetExternalAttributeObservable(string providerKey, string attributeName)
        {
            return _onProviderRegistered
                .StartWith(_externalProviders.ContainsKey(providerKey) ? providerKey : null)
                .Where(k => k == providerKey)
                .Take(1)
                .SelectMany(_ => _externalProviders[providerKey].GetAttributeObservable(attributeName));
        }

        /// <summary>
        /// A reactive stream that fires whenever a new Attribute is added.
        /// Bridges can use this to subscribe to attributes that may not exist yet.
        /// </summary>
        public IObservable<Attribute> OnAttributeAdded => _attributes.ObserveAdd().Select(evt => evt.Value);

        /// <summary>
        /// Retrieves an attribute if it exists, otherwise returns null.
        /// Use this for read-only checks where creating a new attribute is unintended.
        /// Will attempt to search external providers if dot notation is used.
        /// </summary>
        public Attribute GetAttribute(string name)
        {
            if (name.Contains(SEPARATOR))
            {
                var parts = name.Split(SEPARATOR);
                if (_externalProviders.TryGetValue(parts[0], out var provider))
                {
                    return provider.GetAttribute(parts[1]);
                }
                return null;
            }
            return _attributes.TryGetValue(name, out var attr) ? attr : null;
        }

        /// <summary>
        /// Safely gets an attribute, creating it with a default base value if it's missing.
        /// This is ideal for consumers (bridges, UI) that need to read an attribute's final value
        /// without necessarily setting its base.
        /// </summary>
        public Attribute GetOrCreateAttribute(string name, float defaultBaseIfMissing = 0f)
        {
            // We cannot 'Create' an attribute on an external provider remotely unless we own it.
            // But usually, GetOrCreate is called for local attributes or read operations.
            if (name.Contains(SEPARATOR)) return GetAttribute(name);

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
        public void SetOrUpdateBaseValue(string attributeName, float newBase)
        {
            var attr = GetOrCreateAttribute(attributeName);
            attr.SetBaseValue(newBase);
        }

        /// <summary>
        /// Adds a modifier to an attribute, creating the attribute if it doesn't exist.
        /// Now handles External Paths ("Owner.Strength") by queuing if the provider is missing.
        /// </summary>
        /// <param name="sourceId">The ID of the source adding the modifier (e.g., "SwordOfThePhoenix").</param>
        /// <param name="modifier">The IAttributeModifier instance (Flat, Formulaic, Clamp, etc.).</param>
        /// <param name="attributeName">The name of the attribute to modify (e.g., "AttackDamage").</param>
        public void AddModifier(string sourceId, IAttributeModifier modifier, string attributeName)
        {
            Debug.Assert(!string.IsNullOrEmpty(sourceId), "Modifier source ID cannot be null or empty.");
            Debug.Assert(modifier != null, "Modifier object cannot be null.");
            Debug.Assert(!string.IsNullOrEmpty(attributeName), "Target attribute name cannot be null or empty.");

            // 1. Check for External Path (e.g. "Owner.Strength")
            if (attributeName.Contains(SEPARATOR))
            {
                var parts = attributeName.Split(SEPARATOR);
                string providerKey = parts[0];
                string targetAttr = parts[1];

                if (_externalProviders.TryGetValue(providerKey, out var provider))
                {
                    // Provider exists, forward the call
                    provider.AddModifier(sourceId, modifier, targetAttr);
                }
                else
                {
                    // Provider MISSING! Queue it.
                    if (!_pendingModifiers.ContainsKey(providerKey))
                        _pendingModifiers[providerKey] = new List<PendingModifier>();

                    _pendingModifiers[providerKey].Add(new PendingModifier
                    {
                        SourceId = sourceId,
                        Modifier = modifier,
                        AttributeName = targetAttr
                    });

                    //Debug.Log($"[AttributeProcessor] Queued modifier for missing provider: {providerKey}");
                }
                return;
            }

            // 2. Standard Local Add
            var attr = GetOrCreateAttribute(attributeName, 0f);
            attr.AddModifier(modifier);
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