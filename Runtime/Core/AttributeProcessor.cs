using SemanticKeys;
using System;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Core
{
    public class AttributeProcessor
    {
        private readonly ReactiveDictionary<SemanticKey, Attribute> _attributes = new();
        public IReadOnlyReactiveDictionary<SemanticKey, Attribute> Attributes => _attributes;

        private readonly Dictionary<SemanticKey, AttributeProcessor> _externalProviders = new();
        private readonly Subject<SemanticKey> _onProviderRegistered = new();

        private readonly AttributeTagManager _tagManager = new AttributeTagManager();
        public IReadOnlyReactiveDictionary<SemanticKey, int> Tags => _tagManager.Tags;

        public void AddTag(SemanticKey tag) => _tagManager.AddTag(tag);
        public void RemoveTag(SemanticKey tag) => _tagManager.RemoveTag(tag);
        public bool HasTag(SemanticKey tag) => _tagManager.HasTag(tag);

        // --- Pointer Management ---

        public IDisposable SetPointer(SemanticKey alias, SemanticKey target, List<SemanticKey> path = null)
        {
            if (alias == target && (path == null || path.Count == 0))
            {
                Debug.LogWarning($"[AttributeProcessor] Cannot point alias '{alias}' to itself.");
                return Disposable.Empty;
            }

            if (IsLocallyCircular(alias, target))
            {
                Debug.LogError($"[AttributeProcessor] Circular pointer detected: {alias} -> {target}");
                // We return Empty disposable to avoid breaking the caller, but the pointer is NOT added.
                return Disposable.Empty;
            }

            var attr = GetOrCreateAttribute(alias);
            return attr.AddPointer(target, path);
        }

        private bool IsLocallyCircular(SemanticKey alias, SemanticKey target)
        {
            // We are about to set Alias -> Target.
            // Check if Target eventually points back to Alias.
            var currentKey = target;
            int safeguard = 0;

            // Traverse the chain
            while (_attributes.TryGetValue(currentKey, out var attr))
            {
                // Does this attribute point somewhere else?
                var pointerTarget = attr.ActivePointerTarget;
                if (pointerTarget == null)
                {
                    // It's a concrete attribute (end of chain)
                    return false;
                }

                // If it points remotely, we can't easily check cycles synchronously here.
                // We assume remote chains are checked by the remote processor or handled elsewhere.
                if (pointerTarget.Value.Path != null && pointerTarget.Value.Path.Count > 0)
                {
                    return false;
                }

                currentKey = pointerTarget.Value.Name;

                if (currentKey == alias) return true;
                if (++safeguard > 100) return true;
            }
            return false;
        }

        public void RemovePointer(SemanticKey alias)
        {
            // Legacy support if needed.
        }

        // --- External Providers ---

        public void RegisterExternalProvider(SemanticKey key, AttributeProcessor processor)
        {
            Debug.Assert(processor != null, $"[AttributeProcessor] Trying to register a null provider for key: {key}");
            _externalProviders[key] = processor;
            _onProviderRegistered.OnNext(key);
        }

        public void UnregisterExternalProvider(SemanticKey key)
        {
            if (_externalProviders.ContainsKey(key))
            {
                _externalProviders.Remove(key);
                _onProviderRegistered.OnNext(key);
            }
        }

        public IObservable<AttributeProcessor> ObserveProvider(SemanticKey key)
        {
            return _onProviderRegistered
                .Where(k => k.Equals(key))
                .StartWith(key)
                .Select(_ => _externalProviders.TryGetValue(key, out var p) ? p : null)
                .DistinctUntilChanged();
        }

        // --- Retrieval ---

        public IObservable<Attribute> GetAttributeObservable(SemanticKey attributeName, List<SemanticKey> providerPath = null)
        {
            if (providerPath == null || providerPath.Count == 0)
            {
                return GetLocalAttributeObservable(attributeName);
            }

            SemanticKey nextProviderKey = providerPath[0];
            var remainingPath = providerPath.Count > 1 ? providerPath.GetRange(1, providerPath.Count - 1) : new List<SemanticKey>();

            return _onProviderRegistered
                .StartWith(nextProviderKey)
                .Where(k => k == nextProviderKey)
                .Select(_ => _externalProviders.TryGetValue(nextProviderKey, out var p) ? p : null)
                .Select(p => p != null
                    ? p.GetAttributeObservable(attributeName, remainingPath)
                    : Observable.Return<Attribute>(null))
                .Switch();
        }

        private IObservable<Attribute> GetLocalAttributeObservable(SemanticKey name)
        {
            return Observable.Create<Attribute>(observer =>
            {
                if (_attributes.TryGetValue(name, out var current))
                {
                    observer.OnNext(current);
                }

                var updates = Observable.Merge(
                    _attributes.ObserveAdd().Where(e => e.Key == name).Select(e => e.Value),
                    _attributes.ObserveReplace().Where(e => e.Key == name).Select(e => e.NewValue)
                );

                return updates.Subscribe(observer);
            });
        }

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

        public Attribute GetOrCreateAttribute(SemanticKey name, float defaultBaseIfMissing = 0f)
        {
            if (!_attributes.TryGetValue(name, out var attr))
            {
                attr = new Attribute(name, defaultBaseIfMissing, this);
                _attributes[name] = attr;
            }
            return attr;
        }

        public void SetOrUpdateBaseValue(SemanticKey key, float value)
        {
            var attr = GetOrCreateAttribute(key);
            attr.SetBaseValue(value);
        }

        public IDisposable AddModifier(string sourceId, IAttributeModifier modifier, SemanticKey attributeName)
            => AddModifier(sourceId, modifier, attributeName, null);

        public IDisposable AddModifier(string sourceId, IAttributeModifier modifier, SemanticKey attributeName, List<SemanticKey> providerPath)
        {
            if (providerPath == null || providerPath.Count == 0)
            {
                var attr = GetOrCreateAttribute(attributeName, 0f);
                return attr.AddModifier(modifier);
            }
            else
            {
                return new AttributeConnection(this, providerPath, attributeName, modifier, sourceId);
            }
        }
    }
}