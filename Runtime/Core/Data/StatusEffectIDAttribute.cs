using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Core.Data
{
    /// <summary>
    /// Marker attribute for StatusEffect JSON ID fields (strings; on a list it applies to each element), e.g.
    /// <c>[StatusEffectID] public string Poison = "Debuffs/Poison";</c>. StatusEffectIDDrawer shows it as a dropdown of the
    /// JSON files under Resources/Data/StatusEffects; StatusEffectJsonLoader.Load(id) loads the status.
    /// </summary>
    public class StatusEffectIDAttribute : PropertyAttribute
    {
    }
}
