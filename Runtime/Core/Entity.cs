using ReactiveSolutions.AttributeSystem.Core.Data;
using ReactiveSolutions.AttributeSystem.Unity.Data;
using SemanticKeys;
using System;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Core
{
    /// <summary>
    /// The core engine for an entity in the attribute system.
    /// (Tip: Rename this class to "Entity" using your IDE's refactor tool!)
    /// </summary>
    public class Entity : IDisposable
    {
        private readonly ReactiveDictionary<SemanticKey, Attribute> _attributes = new();
        public IReadOnlyReactiveDictionary<SemanticKey, Attribute> Attributes => _attributes;

        private readonly Dictionary<SemanticKey, Entity> _externalProviders = new();
        private readonly Subject<SemanticKey> _onProviderRegistered = new();

        private readonly Dictionary<SemanticKey, LinkGroup> _linkGroups = new();

        private readonly AttributeTagManager _tagManager = new AttributeTagManager();
        public IReadOnlyReactiveDictionary<SemanticKey, int> Tags => _tagManager.Tags;

        // Tracks the lifecycle of Innate StatBlocks and Nested Entities
        private readonly CompositeDisposable _profileDisposables = new CompositeDisposable();
        private readonly List<Entity> _nestedEntities = new List<Entity>();

        public void ApplyProfile(EntityProfileSO profileSO, IModifierFactory modifierFactory)
        => ApplyProfile(profileSO.Profile, modifierFactory);

        /// <summary>
        /// Applies an EntityProfile to this processor, setting up base stats, tags, nested entities, and innate buffs.
        /// </summary>
        public void ApplyProfile(EntityProfile profile, IModifierFactory modifierFactory)
        {
            if (profile == null) return;

            // 1. Base Attributes
            foreach (var entry in profile.BaseAttributes)
            {
                if (entry.Attribute != null)
                {
                    SetOrUpdateBaseValue(entry.Attribute, entry.BaseValue);
                }
            }

            // 2. Innate Tags
            foreach (var tag in profile.InnateTags)
            {
                if (tag != null) AddTag(tag);
            }

            // 3. Link Groups
            foreach (var groupKey in profile.LinkGroups)
            {
                if (groupKey != null) GetOrCreateLinkGroup(groupKey);
            }

            // 4. Nested Entities (Recursive Composition)
            foreach (var nestedEntry in profile.NestedEntities)
            {
                if (nestedEntry.ProviderKey != null && nestedEntry.Profile != null)
                {
                    var childEntity = new Entity();
                    childEntity.ApplyProfile(nestedEntry.Profile, modifierFactory);

                    RegisterExternalProvider(nestedEntry.ProviderKey, childEntity);
                    _nestedEntities.Add(childEntity);
                }
            }

            // 4.5 LinkGroup Population
            foreach (var memberEntry in profile.LinkGroupMembers)
            {
                if (memberEntry.ProviderKey != null && memberEntry.GroupKey != null)
                {
                    if (_externalProviders.TryGetValue(memberEntry.ProviderKey, out var childEntity))
                    {
                        var group = GetOrCreateLinkGroup(memberEntry.GroupKey);
                        group.AddMember(childEntity);
                    }
                    else
                    {
                        Debug.LogWarning($"[Entity] Could not add '{memberEntry.ProviderKey}' to LinkGroup '{memberEntry.GroupKey}' because it is not a registered provider.");
                    }
                }
            }


            // 5. Attribute Pointers
            foreach (var pointer in profile.Pointers)
            {
                if (pointer.Alias != null && pointer.TargetAttribute != null)
                {
                    SetPointer(pointer.Alias, pointer.TargetAttribute, pointer.ProviderPath);
                }
            }

            // 6. Innate Stat Blocks
            foreach (var statBlock in profile.InnateStatBlocks)
            {
                if (statBlock != null)
                {
                    var handle = statBlock.ApplyToEntity(this, modifierFactory);
                    if (handle != null)
                    {
                        _profileDisposables.Add(handle);
                    }
                }
            }
        }

        // --- Tag Management ---

        public void AddTag(SemanticKey tag) => _tagManager.AddTag(tag);
        public void RemoveTag(SemanticKey tag) => _tagManager.RemoveTag(tag);
        public bool HasTag(SemanticKey tag) => _tagManager.HasTag(tag);

        // --- Link Group Management ---

        public LinkGroup GetOrCreateLinkGroup(SemanticKey key)
        {
            if (!_linkGroups.TryGetValue(key, out var group))
            {
                group = new LinkGroup();
                _linkGroups[key] = group;
            }
            return group;
        }

        public LinkGroup GetLinkGroup(SemanticKey key)
        {
            _linkGroups.TryGetValue(key, out var group);
            return group;
        }

        // --- Pointer Management ---

        public IDisposable SetPointer(SemanticKey alias, SemanticKey target, List<SemanticKey> path = null)
        {
            if (alias == target && (path == null || path.Count == 0))
            {
                Debug.LogWarning($"[Entity] Cannot point alias '{alias}' to itself.");
                return Disposable.Empty;
            }

            if (IsLocallyCircular(alias, target))
            {
                Debug.LogError($"[Entity] Circular pointer detected: {alias} -> {target}");
                return Disposable.Empty;
            }

            var attr = GetOrCreateAttribute(alias);
            return attr.AddPointer(target, path);
        }

        private bool IsLocallyCircular(SemanticKey alias, SemanticKey target)
        {
            var currentKey = target;
            int safeguard = 0;

            while (_attributes.TryGetValue(currentKey, out var attr))
            {
                var pointerTarget = attr.ActivePointerTarget;
                if (pointerTarget == null) return false;

                if (pointerTarget.Value.Path != null && pointerTarget.Value.Path.Count > 0) return false;

                currentKey = pointerTarget.Value.Name;

                if (currentKey == alias) return true;
                if (++safeguard > 100) return true;
            }
            return false;
        }

        // --- External Providers ---

        public void RegisterExternalProvider(SemanticKey key, Entity processor)
        {
            Debug.Assert(processor != null, $"[Entity] Trying to register a null provider for key: {key}");
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

        public IObservable<Entity> ObserveProvider(SemanticKey key)
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

        public void Dispose()
        {
            // Clean up profile stat blocks
            _profileDisposables.Dispose();

            // Cascade disposal to nested entities
            foreach (var nested in _nestedEntities)
            {
                nested.Dispose();
            }
            _nestedEntities.Clear();
        }

        public void AddActiveStatBlock(StatBlock statBlock, IModifierFactory modifierFactory)
        {
            Debug.Assert(statBlock != null, "Entity: Cannot apply a null StatBlock.");
            Debug.Assert(modifierFactory != null, "Entity: ModifierFactory is required to apply a StatBlock.");

            if (statBlock == null || modifierFactory == null) return;

            // 'this' works perfectly here assuming Entity implements IAttributeController
            var handle = statBlock.ApplyToEntity(this, modifierFactory);

            if (handle != null)
            {
                // Store the handle so it can be cleaned up when the Entity dies or the buff expires
                _profileDisposables.Add(handle);
            }
        }
    }
}