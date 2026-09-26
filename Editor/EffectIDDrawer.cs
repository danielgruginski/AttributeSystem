using UnityEditor;
using ReactiveSolutions.AttributeSystem.Core.Data;

namespace ReactiveSolutions.AttributeSystem.Editor
{
    /// <summary>
    /// Draws [EffectID] strings as a dropdown of the effect JSON files (in every Resources/Data/Effects folder), with Edit
    /// to open the picked one in the Effect Editor.
    /// </summary>
    [CustomPropertyDrawer(typeof(EffectIDAttribute))]
    public class EffectIDDrawer : JsonIdDrawer
    {
        internal override DataKind Kind => DataKind.Effect;
    }
}
