using SemanticKeys;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Core.Data
{
    /// <summary>
    /// Represents a single entry for a base attribute value inside a profile.
    /// </summary>
    [Serializable]
    public struct BaseAttributeEntry
    {
        [Tooltip("The attribute to initialize.")]
        public SemanticKey Attribute;

        [Tooltip("The starting base value for this attribute.")]
        public float BaseValue;
    }

    /// <summary>
    /// Represents a mapping to redirect local attribute queries to a different attribute.
    /// </summary>
    [Serializable]
    public struct PointerEntry
    {
        [Tooltip("The local alias name for this attribute.")]
        public SemanticKey Alias;

        [Tooltip("The path to the provider holding the target attribute. Leave empty if local.")]
        public List<SemanticKey> ProviderPath;

        [Tooltip("The real attribute that this alias points to.")]
        public SemanticKey TargetAttribute;
    }

    /// <summary>
    /// Represents an entirely self-contained sub-entity generated inside this profile.
    /// </summary>
    [Serializable]
    public struct NestedEntityEntry
    {
        [Tooltip("The key under which this nested entity will be registered (e.g., 'RightHand', 'InnateDemon').")]
        public SemanticKey ProviderKey;

        [Tooltip("The profile used to generate this nested entity.")]
        public EntityProfile Profile;
    }


    [Serializable]
    public struct LinkGroupMemberEntry
    {
        [Tooltip("The key of the LinkGroup to add the entity to.")]
        public SemanticKey GroupKey;

        [Tooltip("The ProviderKey of the nested entity to add to the group.")]
        public SemanticKey ProviderKey;
    }



    /// <summary>
    /// A pure C# POCO blueprint for initializing an Entity.
    /// Fully serializable to JSON/YAML for saving, loading, or modding.
    /// </summary>
    [Serializable]
    public class EntityProfile
    {

        public string name;

        [Header("Base Stats")]
        [Tooltip("Initial attributes and their base values.")]
        public List<BaseAttributeEntry> BaseAttributes = new List<BaseAttributeEntry>();

        [Header("Innate Traits")]
        [Tooltip("Tags that are inherently applied to this entity upon creation (e.g., 'Undead', 'Weapon').")]
        public List<SemanticKey> InnateTags = new List<SemanticKey>();

        [Header("Predefined Link Groups")]
        [Tooltip("Empty groups that should be initialized when the entity spawns (e.g., 'Inventory', 'Party').")]
        public List<SemanticKey> LinkGroups = new List<SemanticKey>();

        [Header("Innate Stat Blocks")]
        [Tooltip("Passives or buffs applied immediately upon creation (e.g., 'Racial Passive', 'Heavy Armor Penalty').")]
        public List<StatBlock> InnateStatBlocks = new List<StatBlock>();

        [Header("Nested Entities")]
        [Tooltip("Child entities generated and owned completely by this profile (registered as External Providers).")]
        public List<NestedEntityEntry> NestedEntities = new List<NestedEntityEntry>();

        [Header("Link Group Members")]
        [Tooltip("Maps nested entities (by ProviderKey) to LinkGroups (by GroupKey) upon initialization.")]
        public List<LinkGroupMemberEntry> LinkGroupMembers = new List<LinkGroupMemberEntry>();

        [Header("Attribute Pointers")]
        [Tooltip("Map local attribute aliases to other attributes (local or remote).")]
        public List<PointerEntry> Pointers = new List<PointerEntry>();
    }
}