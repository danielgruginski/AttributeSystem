using System;
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

        [Tooltip("Supports dot notation for external providers, e.g., 'Owner.Strength' or just 'Agility'")]
        public string AttributePath;

        // --- NEW: Context Baking ---
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

            // If we have a baked context (creator), use it. Otherwise, use the local processor (host).
            var contextToUse = _bakedContext ?? localProcessor;

            // Safety check: if context is somehow null (shouldn't happen in valid flow), fallback or error
            if (contextToUse == null) return Observable.Return(0f);

            return contextToUse.GetAttributeObservable(AttributePath)
                .SelectMany(attr => attr.ReactivePropertyAccess);
        }
    }
}