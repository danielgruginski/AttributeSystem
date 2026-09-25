using ReactiveSolutions.AttributeSystem.Core.Data;
using ReactiveSolutions.AttributeSystem.Core.Modifiers;
using SemanticKeys;
using System;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Core
{
    /// <summary>
    /// A number read by a logic, a condition or a pool: a constant, an attribute's value, or a formula (a logic with
    /// inputs of its own, e.g. "Defense + 100").
    /// </summary>
    [Serializable]
    public class ValueSource
    {
        public enum SourceMode { Constant, Attribute, Formula }

        public SourceMode Mode;
        public float ConstantValue;

        [Tooltip("The attribute to read from.")]
        public AttributeReference AttributeRef;

        [Tooltip("The formula that computes the value.")]
        [SerializeReference]
        public ModifierLogic Formula;

        /// <summary>
        /// Resolves the value into a reactive stream. An attribute is read from <paramref name="context"/> (or through
        /// the reference's provider path from it), and so are a formula's inputs; a missing context, provider or
        /// attribute reads as 0, and so does a formula that isn't set.
        /// </summary>
        public IObservable<float> GetObservable(Entity context)
        {
            if (Mode == SourceMode.Constant)
                return Observable.Return(ConstantValue);

            if (Mode == SourceMode.Formula)
                return Formula != null ? Formula.Observe(context) : Observable.Return(0f);

            if (context == null) return Observable.Return(0f);

            // .Switch() is used to hot-swap if the attribute instance changes (e.g. pointer).
            return context.ObserveAttribute(AttributeRef.Name, AttributeRef.Path, emitNullIfMissing: true)
                .Select(attr =>
                {
                    // A missing attribute, provider or pointer target reads as 0.
                    if (attr == null) return Observable.Return(0f);
                    return attr.ObservableValue;
                })
                .Switch();
        }

        /// <summary>A copy that can be changed without changing this one (its path and formula are copied too).</summary>
        public ValueSource Clone() => (ValueSource)SerializedGraph.Copy(this);

        public static ValueSource Const(float val) => new ValueSource { Mode = ValueSource.SourceMode.Constant, ConstantValue = val };

        /// <summary>An attribute's value: <paramref name="name"/>, local or at the end of the provider <paramref name="path"/>.</summary>
        public static ValueSource FromAttribute(SemanticKey name, params SemanticKey[] path) => new ValueSource
        {
            Mode = SourceMode.Attribute,
            AttributeRef = new AttributeReference(name, new List<SemanticKey>(path))
        };

        /// <summary>
        /// A formula's value, e.g. <c>ValueSource.From(new LinearLogic { Input = ..., Addend = 100 })</c>. A logic also
        /// converts to a ValueSource implicitly, so code can write <c>Divisor = new LinearLogic { ... }</c>.
        /// </summary>
        public static ValueSource From(ModifierLogic formula) => new ValueSource { Mode = SourceMode.Formula, Formula = formula };

        /// <summary>A constant, so code can write <c>Coefficient = 0.5f</c>.</summary>
        public static implicit operator ValueSource(float value) => Const(value);

        /// <summary>An attribute, so code can write <c>Input = AttributeReference.Of(Stats.Strength, Links.Owner)</c>.</summary>
        public static implicit operator ValueSource(AttributeReference attribute) =>
            new ValueSource { Mode = SourceMode.Attribute, AttributeRef = attribute };

        /// <summary>A formula, so code can write <c>Divisor = new LinearLogic { ... }</c>.</summary>
        public static implicit operator ValueSource(ModifierLogic formula) => formula == null ? null : From(formula);
    }
}