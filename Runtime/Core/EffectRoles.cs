using SemanticKeys;
using System;

namespace ReactiveSolutions.AttributeSystem.Core
{
    /// <summary>
    /// The two entities of an effect. Everything an effect reads or changes is reached through one of them, as the
    /// first step of a path: "Source/AttackPower" is the source's AttackPower, "Target/Health" the target's Health.
    /// The package's "Effect Roles" KeyDomain has the same keys, so the Inspector's key dropdowns can pick them.
    /// </summary>
    public static class EffectRoles
    {
        /// <summary>The GUID of the "Effect Roles" KeyDomain.</summary>
        public const string DomainGuid = "6deb057c-36ea-4382-860a-14006f138443";

        /// <summary>The entity the effect comes from: the attacker, the caster, the one who drinks the potion.</summary>
        public static readonly SemanticKey Source = new SemanticKey("00ad666d-e0de-4b82-bda8-034cd7de8f46", "Source", DomainGuid);

        /// <summary>The entity the effect is applied to.</summary>
        public static readonly SemanticKey Target = new SemanticKey("ae486b69-3a61-473f-9530-1fc6497b681e", "Target", DomainGuid);

        /// <summary>Whether <paramref name="key"/> is Source or Target.</summary>
        public static bool IsRole(SemanticKey key) => key == Source || key == Target;

        /// <summary>The role named <paramref name="name"/> ("Source" or "Target", ignoring case), or SemanticKey.None.</summary>
        internal static SemanticKey Find(string name)
        {
            if (string.Equals(name, Source.Value, StringComparison.OrdinalIgnoreCase)) return Source;
            if (string.Equals(name, Target.Value, StringComparison.OrdinalIgnoreCase)) return Target;
            return SemanticKey.None;
        }
    }
}
