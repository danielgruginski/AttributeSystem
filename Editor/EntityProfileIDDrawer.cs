using UnityEditor;
using ReactiveSolutions.AttributeSystem.Core.Data;

namespace ReactiveSolutions.AttributeSystem.Editor
{
    /// <summary>
    /// Draws [EntityProfileID] strings as a dropdown of the profile JSON files under Resources/Data/EntityProfiles.
    /// </summary>
    [CustomPropertyDrawer(typeof(EntityProfileIDAttribute))]
    public class EntityProfileIDDrawer : JsonIdDrawer
    {
        protected override string Folder => "Resources/" + EntityProfileJsonLoader.ResourcesPath;
    }
}