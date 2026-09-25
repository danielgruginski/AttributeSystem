using ReactiveSolutions.AttributeSystem.Core.Data;
using SemanticKeys;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Core.Builders
{
    /// <summary>
    /// A fluent API for constructing EntityProfiles entirely through code.
    /// </summary>
    public class ProfileBuilder
    {
        private readonly EntityProfile _profile;

        private ProfileBuilder()
        {
            // Now instantiates a pure POCO!
            _profile = new EntityProfile();
        }

        public static ProfileBuilder Create(string profileName = "NewProfile")
        {
            var builder = new ProfileBuilder();
            builder._profile.ProfileName = profileName;
            return builder;
        }

        /// <summary>
        /// Builds on a template profile JSON (e.g. "Templates/Character" under Resources/Data/EntityProfiles): it is applied
        /// before this profile, once per entity, so this profile's values override the template's.
        /// </summary>
        public ProfileBuilder AddTemplate(string profileId)
        {
            _profile.Templates.Add(new TemplateEntry { ProfileId = profileId });
            return this;
        }

        /// <summary>Builds on a template profile built in code: applied before this profile, once per entity.</summary>
        public ProfileBuilder AddTemplate(EntityProfile template)
        {
            _profile.Templates.Add(new TemplateEntry { Profile = template });
            return this;
        }

        /// <summary>
        /// When an entity created from this profile is nested in another (e.g. a sword in a RightHand), it reaches that
        /// entity under <paramref name="key"/> (e.g. Links.Owner).
        /// </summary>
        public ProfileBuilder SetParentKey(SemanticKey key)
        {
            _profile.ParentKey = key;
            return this;
        }

        public ProfileBuilder AddBaseAttribute(SemanticKey name, float value)
        {
            _profile.BaseAttributes.Add(new BaseAttributeEntry { Attribute = name, BaseValue = value });
            return this;
        }

        /// <summary>
        /// Instantly replicates multiple attributes with the same starting value.
        /// (Perfect for initializing Strength, Dexterity, Constitution, etc., all at once).
        /// </summary>
        public ProfileBuilder AddBaseAttributes(float baseValue, params SemanticKey[] attributeNames)
        {
            foreach (var name in attributeNames)
            {
                AddBaseAttribute(name, baseValue);
            }
            return this;
        }

        /// <summary>
        /// Makes <paramref name="resource"/> a pool (e.g. Health): an amount between 0 and <paramref name="max"/>
        /// (e.g. ValueSource.FromAttribute(Stats.MaxHealth), or a constant), full when the entity is created.
        /// </summary>
        public ProfileBuilder AddPool(SemanticKey resource, ValueSource max, PoolMaxChange onMaxChange = PoolMaxChange.KeepPercent)
        {
            _profile.Pools.Add(new PoolEntry { Resource = resource, Max = max, OnMaxChange = onMaxChange });
            return this;
        }

        public ProfileBuilder AddInnateTag(SemanticKey tag)
        {
            _profile.InnateTags.Add(tag);
            return this;
        }

        public ProfileBuilder AddLinkGroup(SemanticKey groupName)
        {
            _profile.LinkGroups.Add(groupName);
            return this;
        }

        public ProfileBuilder AddPointer(SemanticKey alias, SemanticKey target, params SemanticKey[] providerPath)
        {
            var pathList = new List<SemanticKey>();
            foreach (var p in providerPath) pathList.Add(p);

            _profile.Pointers.Add(new PointerEntry
            {
                Alias = (alias),
                TargetAttribute = (target),
                ProviderPath = pathList
            });
            return this;
        }

        public ProfileBuilder AddNestedEntity(SemanticKey providerKey, EntityProfile profile)
        {
            _profile.NestedEntities.Add(new NestedEntityEntry
            {
                ProviderKey = (providerKey),
                Profile = profile
            });
            return this;
        }

        /// <summary>
        /// Creates a nested entity inline using another builder.
        /// </summary>
        public ProfileBuilder AddNestedEntity(SemanticKey providerKey, Action<ProfileBuilder> buildAction)
        {
            var nestedBuilder = Create(providerKey + "_Profile");
            buildAction?.Invoke(nestedBuilder);
            return AddNestedEntity(providerKey, nestedBuilder.Build());
        }

        /// <summary>
        /// Adds a nested entity created from a profile JSON file (e.g. "Weapons/IronSword" under Resources/Data/EntityProfiles).
        /// </summary>
        public ProfileBuilder AddNestedEntity(SemanticKey providerKey, string profileId)
        {
            _profile.NestedEntities.Add(new NestedEntityEntry
            {
                ProviderKey = providerKey,
                ProfileId = profileId
            });
            return this;
        }

        public ProfileBuilder AddInnateStatBlock(StatBlock statBlock)
        {
            _profile.InnateStatBlocks.Add(statBlock);
            return this;
        }

        /// <summary>
        /// Adds an innate StatBlock JSON file by ID (e.g. "Passives/Undead" under Resources/Data/StatBlocks).
        /// </summary>
        public ProfileBuilder AddInnateStatBlock(StatBlockID statBlockId)
        {
            _profile.InnateStatBlockIds.Add(statBlockId);
            return this;
        }

        /// <summary>
        /// Creates an innate StatBlock inline using the StatBlockBuilder.
        /// </summary>
        public ProfileBuilder AddInnateStatBlock(Action<StatBlockBuilder> buildAction)
        {
            var statBuilder = StatBlockBuilder.Create();
            buildAction?.Invoke(statBuilder);
            return AddInnateStatBlock(statBuilder.Build());
        }

        public EntityProfile Build() => _profile;
    }
}