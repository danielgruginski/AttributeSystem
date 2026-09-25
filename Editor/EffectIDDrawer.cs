using UnityEditor;
using ReactiveSolutions.AttributeSystem.Core.Data;

namespace ReactiveSolutions.AttributeSystem.Editor
{
    /// <summary>
    /// Draws [EffectID] strings as a dropdown of the effect JSON files under Resources/Data/Effects.
    /// </summary>
    [CustomPropertyDrawer(typeof(EffectIDAttribute))]
    public class EffectIDDrawer : JsonIdDrawer
    {
        protected override string Folder => "Resources/" + EffectJsonLoader.ResourcesPath;
    }
}
