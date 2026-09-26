using UnityEditor;
using ReactiveSolutions.AttributeSystem.Core.Data;

namespace ReactiveSolutions.AttributeSystem.Editor
{
    /// <summary>
    /// Draws [StatusEffectID] strings as a dropdown of the status effect JSON files (in every Resources/Data/StatusEffects
    /// folder), with Edit to open the picked one in the Status Effect Editor.
    /// </summary>
    [CustomPropertyDrawer(typeof(StatusEffectIDAttribute))]
    public class StatusEffectIDDrawer : JsonIdDrawer
    {
        internal override DataKind Kind => DataKind.StatusEffect;
    }
}
