using ReactiveSolutions.AttributeSystem.Core.Data;
using SemanticKeys;
using System;
using System.Collections.Generic;
using UnityEngine;
using sk; // Included to match your test files (e.g., Modifiers.Static)

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
            builder._profile.name = profileName;
            return builder;
        }

        // Helper to instantly convert strings to SemanticKeys
        //private SemanticKey Key(string name) => new SemanticKey(name, name, null);

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

        public ProfileBuilder AddNestedEntityToGroup(SemanticKey providerKey, SemanticKey groupKey)
        {
            _profile.LinkGroupMembers.Add(new LinkGroupMemberEntry
            {
                ProviderKey = providerKey,
                GroupKey = groupKey
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

        public ProfileBuilder AddInnateStatBlock(StatBlock statBlock)
        {
            _profile.InnateStatBlocks.Add(statBlock);
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