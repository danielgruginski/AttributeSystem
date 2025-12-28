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
        // This is not serialized; it is set at runtime when the StatBlock is applied.
        private AttributeProcessor _bakedContext;

        public void BakeContext(AttributeProcessor context)
        {
            _bakedContext = context;
        }
        // ---------------------------

        /// <summary>
        /// Resolves the value into a reactive stream.
        /// </summary>
        public IObservable<float> GetObservable(AttributeProcessor localProcessor)
        {
            if (Mode == SourceMode.Constant)
                return Observable.Return(ConstantValue);

            var contextToUse = _bakedContext ?? localProcessor;
            if (contextToUse == null) return Observable.Return(0f);

            // Use the structured reference
            return contextToUse.GetAttributeObservable(AttributeRef.Name, AttributeRef.Path)
                .SelectMany(attr => attr.ReactivePropertyAccess);
        }

        public static ValueSource Const(float val) => new ValueSource { Mode = ValueSource.SourceMode.Constant, ConstantValue = val };

    }
}