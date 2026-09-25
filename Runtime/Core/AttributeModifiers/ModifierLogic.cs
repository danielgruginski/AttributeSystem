using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UniRx;

namespace ReactiveSolutions.AttributeSystem.Core.Modifiers
{
    /// <summary>
    /// Computes a modifier's value. The modifier's Type decides what the value does to the attribute (add to it,
    /// multiply it, replace it, clamp it) and its Priority decides when, so the same logic works for any of them.
    /// <para>
    /// To add your own logic, write a [Serializable] class that derives from <see cref="FormulaLogic"/> (a formula
    /// over inputs) or from ModifierLogic itself. Its public fields are its settings; ValueSource fields are inputs
    /// that hold a constant or read an attribute. There is nothing to register: the class shows up in the Logic
    /// dropdown of the Inspector and the Stat Block Editor, and is saved with the StatBlock.
    /// </para>
    /// </summary>
    [Serializable]
    public abstract class ModifierLogic
    {
        /// <summary>
        /// The value as a stream: the current value on subscribe, then every change.
        /// Inputs that read attributes resolve relative to <paramref name="context"/>.
        /// </summary>
        public abstract IObservable<float> Observe(Entity context);

        /// <summary>
        /// A copy of this logic. Shallow by default; override it if your logic holds objects that must not be shared.
        /// </summary>
        public virtual ModifierLogic Clone() => (ModifierLogic)MemberwiseClone();

        /// <summary>The name shown in the Inspector and in logs: the class name without a "Logic" suffix.</summary>
        public static string GetDisplayName(Type logicType)
        {
            if (logicType == null) return "None";
            string name = logicType.Name;
            return name.EndsWith("Logic") && name.Length > "Logic".Length
                ? name.Substring(0, name.Length - "Logic".Length)
                : name;
        }

        /// <summary>
        /// Gives each spec its own logic object. A list entry duplicated in the Inspector can point to the same
        /// object as the entry it was copied from, so editing one would change both.
        /// </summary>
        internal static void Unshare(IEnumerable<AttributeModifierSpec> specs)
        {
            var seen = new HashSet<ModifierLogic>(ReferenceComparer.Instance);
            foreach (var spec in specs)
            {
                if (spec?.Logic == null) continue;
                if (!seen.Add(spec.Logic)) spec.Logic = spec.Logic.Clone();
            }
        }

        private sealed class ReferenceComparer : IEqualityComparer<ModifierLogic>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();
            public bool Equals(ModifierLogic x, ModifierLogic y) => ReferenceEquals(x, y);
            public int GetHashCode(ModifierLogic obj) => RuntimeHelpers.GetHashCode(obj);
        }
    }

    /// <summary>
    /// Logic computed from a fixed list of inputs, and recomputed whenever one of them changes.
    /// Derive from it, return your ValueSource fields from <see cref="Inputs"/>, and compute the value from their
    /// current values in <see cref="Compute"/>.
    /// </summary>
    [Serializable]
    public abstract class FormulaLogic : ModifierLogic
    {
        /// <summary>The inputs, in the order their values are passed to <see cref="Compute"/>. A null input reads as 0.</summary>
        protected abstract IEnumerable<ValueSource> Inputs { get; }

        /// <summary>The value, from the current values of <see cref="Inputs"/> (in the same order).</summary>
        protected abstract float Compute(IList<float> inputs);

        public override IObservable<float> Observe(Entity context)
        {
            var streams = Inputs
                .Select(input => input != null ? input.GetObservable(context) : Observable.Return(0f))
                .ToList();

            if (streams.Count == 0) return Observable.Return(Compute(Array.Empty<float>()));

            // Emits once every input has a value, then whenever any of them changes.
            return Observable.CombineLatest(streams).Select(Compute);
        }
    }
}