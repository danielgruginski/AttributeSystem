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

        [Header("Attribute Reference")]
        [Tooltip("The name of the attribute to read (e.g. 'Strength').")]
        public SemanticKey AttributeName;

        [Tooltip("The path to the provider. Empty = Local. Example: ['Owner', 'EquippedWeapon']")]
        public List<SemanticKey> ProviderPath = new List<SemanticKey>();

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

            // Use the new structured lookup
            return contextToUse.GetAttributeObservable(AttributeName, ProviderPath)
                .SelectMany(attr => attr.ReactivePropertyAccess);
        }
    }
}