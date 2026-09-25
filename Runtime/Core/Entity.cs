using ReactiveSolutions.AttributeSystem.Core.Data;
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
    /// </summary>
    public class Entity : IDisposable
    {
        public bool IsDisposed { get; private set; }

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

        /// <summary>
        /// Applies an EntityProfile to this processor, setting up base stats, tags, nested entities, and innate buffs.
        /// Nested profiles and StatBlocks referenced by ID are loaded from their JSON files.
        /// </summary>
        public void ApplyProfile(EntityProfile profile)
            => ApplyProfile(profile, new HashSet<object>());

        /// <param name="applying">The profiles (objects, and the IDs of JSON profiles) being applied further up the
        /// nesting chain, so a profile that nests itself is reported instead of recursing forever.</param>
        private void ApplyProfile(EntityProfile profile, HashSet<object> applying)
        {
            if (profile == null) return;

            applying.Add(profile);
            if (profile.JsonId != null) applying.Add(profile.JsonId);

            // SemanticKey is a struct: unassigned entries are SemanticKey.None, never null.

            // 1. Base Attributes
            foreach (var entry in profile.BaseAttributes)
            {
                if (entry.Attribute != SemanticKey.None)
                {
                    SetOrUpdateBaseValue(entry.Attribute, entry.BaseValue);
                }
            }

            // 2. Innate Tags
            foreach (var tag in profile.InnateTags)
            {
                if (tag != SemanticKey.None) AddTag(tag);
            }

            // 3. Link Groups
            foreach (var groupKey in profile.LinkGroups)
            {
                if (groupKey != SemanticKey.None) GetOrCreateLinkGroup(groupKey);
            }

            // 4. Nested Entities (Recursive Composition): a profile built in code, or one saved as JSON
            foreach (var nestedEntry in profile.NestedEntities)
            {
                if (nestedEntry.ProviderKey == SemanticKey.None) continue;

                string nestedId = nestedEntry.Profile == null && !string.IsNullOrEmpty(nestedEntry.ProfileId)
                    ? EntityProfileJsonLoader.NormalizeId(nestedEntry.ProfileId)
                    : null;
                if (nestedEntry.Profile == null && nestedId == null) continue;

                bool isApplying = nestedEntry.Profile != null
                    ? applying.Contains(nestedEntry.Profile) || (nestedEntry.Profile.JsonId != null && applying.Contains(nestedEntry.Profile.JsonId))
                    : applying.Contains(nestedId);
                if (isApplying)
                {
                    string profileName = nestedEntry.Profile != null ? nestedEntry.Profile.ProfileName : nestedId;
                    Debug.LogError($"[Entity] Skipped nested entity '{nestedEntry.ProviderKey}': profile '{profileName}' " +
                                   "is already being applied further up. A profile can't nest itself.");
                    continue;
                }

                // The loader logs an error if the JSON can't be loaded.
                var nestedProfile = nestedEntry.Profile ?? EntityProfileJsonLoader.Load(nestedId);
                if (nestedProfile == null) continue;

                var childEntity = new Entity();
                childEntity.ApplyProfile(nestedProfile, applying);

                RegisterExternalProvider(nestedEntry.ProviderKey, childEntity);
                _nestedEntities.Add(childEntity);
            }

            // 5. Attribute Pointers
            foreach (var pointer in profile.Pointers)
            {
                if (pointer.Alias != SemanticKey.None && pointer.TargetAttribute != SemanticKey.None)
                {
                    SetPointer(pointer.Alias, pointer.TargetAttribute, pointer.ProviderPath);
                }
            }

            // 6. Innate Stat Blocks: JSON files by ID, then the ones stored in the profile
            foreach (var statBlockId in profile.InnateStatBlockIds)
            {
                if (string.IsNullOrEmpty(statBlockId)) continue;

                // The loader logs an error if the JSON can't be loaded.
                var statBlock = new StatBlock();
                if (StatBlockJsonLoader.TryLoadInto(statBlockId, statBlock)) ApplyInnateStatBlock(statBlock);
            }

            foreach (var statBlock in profile.InnateStatBlocks)
            {
                if (statBlock != null) ApplyInnateStatBlock(statBlock);
            }

            applying.Remove(profile);
            if (profile.JsonId != null) applying.Remove(profile.JsonId);
        }

        private void ApplyInnateStatBlock(StatBlock statBlock)
        {
            var handle = statBlock.ApplyToEntity(this);
            if (handle != null)
            {
                _profileDisposables.Add(handle);
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

            // Only a local pointer can close a loop among this entity's own aliases; a remote target
            // (non-empty path) is a different attribute even if it shares the name.
            bool isLocal = path == null || path.Count == 0;
            if (isLocal && IsLocallyCircular(alias, target))
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
            if (IsDisposed) return;
            Debug.Assert(processor != null, $"[Entity] Trying to register a null provider for key: {key}");
            _externalProviders[key] = processor;
            _onProviderRegistered.OnNext(key);
        }

        public void UnregisterExternalProvider(SemanticKey key)
        {
            if (IsDisposed) return;
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

        /// <summary>
        /// Emits the attribute (and any replacement of it) once it exists.
        /// Emits null while a provider on the path is missing.
        /// </summary>
        public IObservable<Attribute> GetAttributeObservable(SemanticKey attributeName, List<SemanticKey> providerPath = null)
            => ObserveAttribute(attributeName, providerPath, emitNullIfMissing: false);

        /// <summary>
        /// Emits the attribute's final value; nothing while the attribute (or a provider on the path) is missing.
        /// </summary>
        public IObservable<float> ObserveValue(SemanticKey attributeName, List<SemanticKey> providerPath = null)
            => GetAttributeObservable(attributeName, providerPath)
                .Select(attr => attr == null ? Observable.Empty<float>() : (IObservable<float>)attr.ObservableValue)
                .Switch();

        /// <summary>
        /// Like GetAttributeObservable, but with emitNullIfMissing a missing LOCAL attribute also emits null,
        /// so value lookups (modifier arguments, pointers, conditions) can read it as 0 instead of waiting.
        /// </summary>
        internal IObservable<Attribute> ObserveAttribute(SemanticKey attributeName, List<SemanticKey> providerPath, bool emitNullIfMissing)
        {
            if (providerPath == null || providerPath.Count == 0)
            {
                return GetLocalAttributeObservable(attributeName, emitNullIfMissing);
            }

            SemanticKey nextProviderKey = providerPath[0];
            var remainingPath = providerPath.Count > 1 ? providerPath.GetRange(1, providerPath.Count - 1) : new List<SemanticKey>();

            return _onProviderRegistered
                .StartWith(nextProviderKey)
                .Where(k => k == nextProviderKey)
                .Select(_ => _externalProviders.TryGetValue(nextProviderKey, out var p) ? p : null)
                .Select(p => p != null
                    ? p.ObserveAttribute(attributeName, remainingPath, emitNullIfMissing)
                    : Observable.Return<Attribute>(null))
                .Switch();
        }

        private IObservable<Attribute> GetLocalAttributeObservable(SemanticKey name, bool emitNullIfMissing)
        {
            return Observable.Create<Attribute>(observer =>
            {
                if (_attributes.TryGetValue(name, out var current))
                {
                    observer.OnNext(current);
                }
                else if (emitNullIfMissing)
                {
                    observer.OnNext(null);
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
            if (IsDisposed) return Disposable.Empty;

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
            if (IsDisposed) return;
            IsDisposed = true;

            // Clean up profile stat blocks
            _profileDisposables.Dispose();

            // Cascade disposal to nested entities
            foreach (var nested in _nestedEntities)
            {
                nested.Dispose();
            }
            _nestedEntities.Clear();

            // Stop every attribute pipeline, releasing its subscriptions to other entities.
            // Attributes stay readable (last value) but no longer update.
            foreach (var attribute in new List<Attribute>(_attributes.Values))
            {
                attribute.Dispose();
            }
        }
    }
}