using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Core.Data
{
    /// <summary>
    /// Marker attribute for Effect JSON ID fields (strings; on a list it applies to each element), e.g.
    /// <c>[EffectID] public string Attack = "Combat/WeaponHit";</c>. EffectIDDrawer shows it as a dropdown of the JSON
    /// files under Resources/Data/Effects; EffectJsonLoader.Load(id) loads the effect.
    /// </summary>
    public class EffectIDAttribute : PropertyAttribute
    {
    }
}
