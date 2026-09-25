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

        // Stores the processor that 'owns' this source definition (e.g., the Weapon).
        // This is not serialized; it is set at runtime when the StatBlock is applied,
        // on a per-application copy (see Clone) so shared asset data is never mutated.
        private Entity _bakedContext;

        public void BakeContext(Entity context)
        {
            _bakedContext = context;
        }
        // ---------------------------

        /// <summary>
        /// Returns a copy of this source (sharing the AttributeRef data) that can be baked independently.
        /// </summary>
        public ValueSource Clone() => (ValueSource)MemberwiseClone();

        /// <summary>
        /// Resolves the value into a reactive stream. A missing attribute or provider reads as 0.
        /// </summary>
        public IObservable<float> GetObservable(Entity localProcessor)
        {
            if (Mode == SourceMode.Constant)
                return Observable.Return(ConstantValue);

            var contextToUse = _bakedContext ?? localProcessor;
            if (contextToUse == null) return Observable.Return(0f);

            // Use the structured reference.
            // .Switch() is used to hot-swap if the attribute instance changes (e.g. pointer).
            return contextToUse.ObserveAttribute(AttributeRef.Name, AttributeRef.Path, emitNullIfMissing: true)
                .Select(attr =>
                {
                    // FIX: Handle NULL attribute (e.g. missing provider or broken pointer)
                    if (attr == null) return Observable.Return(0f);
                    return attr.ObservableValue;
                })
                .Switch();
        }

        public static ValueSource Const(float val) => new ValueSource { Mode = ValueSource.SourceMode.Constant, ConstantValue = val };
    }
}