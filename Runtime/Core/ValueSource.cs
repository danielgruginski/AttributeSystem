using ReactiveSolutions.AttributeSystem.Core.Data;
using SemanticKeys;
using System;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Core
{
    [Serializable]
    public class ValueSource
    {
        public enum SourceMode { Constant, Attribute }

        public SourceMode Mode;
        public float ConstantValue;

        [Tooltip("The attribute to read from.")]
        public AttributeReference AttributeRef;

        /// <summary>
        /// Resolves the value into a reactive stream. An attribute is read from <paramref name="context"/> (or through
        /// the reference's provider path from it); a missing context, provider or attribute reads as 0.
        /// </summary>
        public IObservable<float> GetObservable(Entity context)
        {
            if (Mode == SourceMode.Constant)
                return Observable.Return(ConstantValue);

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

        public static ValueSource Const(float val) => new ValueSource { Mode = ValueSource.SourceMode.Constant, ConstantValue = val };

        /// <summary>An attribute's value: <paramref name="name"/>, local or at the end of the provider <paramref name="path"/>.</summary>
        public static ValueSource FromAttribute(SemanticKey name, params SemanticKey[] path) => new ValueSource
        {
            Mode = SourceMode.Attribute,
            AttributeRef = new AttributeReference(name, new List<SemanticKey>(path))
        };

        /// <summary>A constant, so code can write <c>Coefficient = 0.5f</c>.</summary>
        public static implicit operator ValueSource(float value) => Const(value);
    }
}