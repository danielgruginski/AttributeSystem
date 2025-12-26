using System;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Core
{
    /// <summary>
    /// The core logic engine that manages local attributes and links to external providers.
    /// Supports dot notation (e.g., "Owner.Strength") for cross-referencing stats.
    /// </summary>
    public class AttributeProcessor : IDisposable
    {
        private readonly ReactiveDictionary<string, Attribute> _attributes = new();
        private readonly Dictionary<string, AttributeProcessor> _externalProviders = new();
        private readonly Subject<string> _onProviderRegistered = new();

        // Tracks pending modifier additions for external targets to prevent leaks
        private readonly CompositeDisposable _pendingModifiers = new();

        private const char SEPARATOR = '.';

        /// <summary>
        /// Registers a foreign processor under a specific alias (e.g., "Owner").
        /// This resolves any pending reactive searches for that alias.
        /// </summary>
        public void RegisterExternalProvider(string key, AttributeProcessor processor)
        {
            Debug.Assert(processor != null, $"[AttributeProcessor] Trying to register a null provider for key: {key}");
            _externalProviders[key] = processor;
            _onProviderRegistered.OnNext(key);
        }

        /// <summary>
        /// Returns an observable that emits the Attribute once it is available.
        /// Handles the "wait" logic for external providers automatically.
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

        public IReadOnlyReactiveDictionary<string, Attribute> Attributes => _attributes;
        public IObservable<Attribute> OnAttributeAdded => _attributes.ObserveAdd().Select(evt => evt.Value);

        /// <summary>
        /// Synchronously attempts to find an attribute. 
        /// Returns null if local attribute is missing or external provider is not linked.
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
        /// Returns a local attribute, creating it if it doesn't exist.
        /// Does NOT create attributes for external paths (returns current state instead).
        /// </summary>
        public Attribute GetOrCreateAttribute(string name, float defaultBaseIfMissing = 0f)
        {
            // If the name points to an external target, we cannot "Create" it locally.
            // We return whatever GetAttribute finds (which might be null).
            if (name.Contains(SEPARATOR)) return GetAttribute(name);

            if (!_attributes.TryGetValue(name, out var attr))
            {
                attr = new Attribute(name, defaultBaseIfMissing, this);
                _attributes[name] = attr;
            }
            return attr;
        }

        public void SetOrUpdateBaseValue(string attributeName, float newBase)
        {
            var attr = GetOrCreateAttribute(attributeName);
            if (attr != null)
            {
                attr.SetBaseValue(newBase);
            }
        }

        /// <summary>
        /// Adds a modifier to an attribute. If the target is external, 
        /// it waits reactively for the provider to be registered.
        /// </summary>
        public void AddModifier(string sourceId, IAttributeModifier modifier, string attributeName)
        {
            if (modifier == null)
            {
                Debug.LogError($"[AttributeProcessor] Attempted to add a null modifier for '{attributeName}' from source '{sourceId}'.");
                return;
            }

            // Fix for NRE: If the target is external, use the reactive search instead of GetOrCreate.
            if (attributeName.Contains(SEPARATOR))
            {
                GetAttributeObservable(attributeName)
                    .Take(1) // We only need the reference once to attach the modifier
                    .Subscribe(attr => attr.AddModifier(modifier))
                    .AddTo(_pendingModifiers);
                return;
            }

            var attr = GetOrCreateAttribute(attributeName, 0f);
            attr.AddModifier(modifier);
        }

        /// <summary>
        /// Removes all modifiers matching the SourceId from local attributes 
        /// and propagates the request to linked external providers.
        /// </summary>
        public void RemoveModifiersBySource(string sourceId)
        {
            // 1. Clean local attributes
            foreach (var attribute in _attributes.Values)
            {
                var toRemove = attribute.Modifiers.Where(m => m.SourceId == sourceId).ToList();
                foreach (var mod in toRemove)
                {
                    attribute.RemoveModifier(mod);
                }
            }

            // 2. Propagate to external providers to ensure modifiers we "pushed" to them are removed
            foreach (var provider in _externalProviders.Values)
            {
                provider.RemoveModifiersBySource(sourceId);
            }
        }

        public void Dispose()
        {
            _pendingModifiers.Dispose();
            _onProviderRegistered.OnCompleted();
            _onProviderRegistered.Dispose();

            foreach (var attr in _attributes.Values)
            {
                attr.Dispose();
            }
            _attributes.Clear();
        }
    }
}
