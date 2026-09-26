using UnityEditor;
using ReactiveSolutions.AttributeSystem.Core.Data;

namespace ReactiveSolutions.AttributeSystem.Editor
{
    /// <summary>
    /// Draws [EntityProfileID] strings as a dropdown of the profile JSON files (in every Resources/Data/EntityProfiles
    /// folder), with Edit to open the picked one in the Entity Profile Editor: a template, a nested entity's profile, or
    /// an EntityController's profile.
    /// </summary>
    [CustomPropertyDrawer(typeof(EntityProfileIDAttribute))]
    public class EntityProfileIDDrawer : JsonIdDrawer
    {
        internal override DataKind Kind => DataKind.EntityProfile;
    }
}