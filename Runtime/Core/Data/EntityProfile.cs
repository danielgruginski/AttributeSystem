using ReactiveSolutions.AttributeSystem.Core.Modifiers;
using SemanticKeys;
using System;
using System.Collections.Generic;
using System.Linq;
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

        [Tooltip("The profile JSON (under Resources/Data/EntityProfiles) used to generate this nested entity.")]
        [EntityProfileID]
        public string ProfileId;

        /// <summary>
        /// A profile built in code (e.g. by ProfileBuilder), used instead of ProfileId. Not serialized:
        /// saved profiles reference their nested profiles by ID.
        /// </summary>
        [NonSerialized]
        public EntityProfile Profile;
    }


    /// <summary>
    /// A profile that another profile builds on (e.g. "Templates/Character"): it is applied first, and only once per
    /// entity, however many of the entity's profiles and templates build on it.
    /// </summary>
    [Serializable]
    public struct TemplateEntry
    {
        [Tooltip("The template's profile JSON (under Resources/Data/EntityProfiles).")]
        [EntityProfileID]
        public string ProfileId;

        /// <summary>
        /// A template built in code (e.g. by ProfileBuilder), used instead of ProfileId. Not serialized: the Inspector
        /// shows templates by ID, and EntityProfileJson writes this profile in full.
        /// </summary>
        [NonSerialized]
        public EntityProfile Profile;
    }

    /// <summary>
    /// A pure C# POCO blueprint for initializing an Entity.
    /// Fully serializable to JSON/YAML for saving, loading, or modding: save it as JSON with the
    /// Entity Profile Editor and load it with EntityProfileJsonLoader, or author it inline on an EntityController.
    /// </summary>
    [Serializable]
    public class EntityProfile : ISerializationCallbackReceiver
    {
        public string ProfileName;

        /// <summary>The ID of the JSON file this profile was loaded from (set by EntityProfileJsonLoader), or null.</summary>
        [NonSerialized]
        internal string JsonId;

        [Header("Templates")]
        [Tooltip("Profiles this one builds on (e.g. 'Templates/Character'). They are applied first, and only once per entity, " +
                 "even when several profiles build on the same template; this profile's own values override theirs.")]
        public List<TemplateEntry> Templates = new List<TemplateEntry>();

        [Header("As a Nested Entity")]
        [Tooltip("When an entity created from this profile is nested in another (e.g. a sword in a 'RightHand'), the key under " +
                 "which it reaches that entity (e.g. 'Owner'). Leave empty for no link back.")]
        public SemanticKey ParentKey;

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
        [Tooltip("StatBlock JSON files (by ID) applied immediately upon creation (e.g., 'Passives/Undead').")]
        public List<StatBlockID> InnateStatBlockIds = new List<StatBlockID>();

        [Tooltip("Passives or buffs applied immediately upon creation (e.g., 'Racial Passive', 'Heavy Armor Penalty').")]
        public List<StatBlock> InnateStatBlocks = new List<StatBlock>();

        [Header("Nested Entities")]
        [Tooltip("Child entities generated and owned completely by this profile (registered as External Providers).")]
        public List<NestedEntityEntry> NestedEntities = new List<NestedEntityEntry>();

        [Header("Attribute Pointers")]
        [Tooltip("Map local attribute aliases to other attributes (local or remote).")]
        public List<PointerEntry> Pointers = new List<PointerEntry>();

        public void OnBeforeSerialize() { }

        // An inline StatBlock duplicated in the Inspector can share modifier logic objects with the original.
        public void OnAfterDeserialize() =>
            ModifierLogic.Unshare((InnateStatBlocks ?? new List<StatBlock>())
                .Where(block => block?.Modifiers != null)
                .SelectMany(block => block.Modifiers));
    }
}