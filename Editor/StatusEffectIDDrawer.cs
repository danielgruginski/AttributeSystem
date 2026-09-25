using UnityEditor;
using ReactiveSolutions.AttributeSystem.Core.Data;

namespace ReactiveSolutions.AttributeSystem.Editor
{
    /// <summary>
    /// Draws [StatusEffectID] strings as a dropdown of the status effect JSON files under Resources/Data/StatusEffects.
    /// </summary>
    [CustomPropertyDrawer(typeof(StatusEffectIDAttribute))]
    public class StatusEffectIDDrawer : JsonIdDrawer
    {
        protected override string Folder => "Resources/" + StatusEffectJsonLoader.ResourcesPath;
    }
}
