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

        /// <summary>
        /// Resolves the value into a reactive stream.
        /// </summary>
        public IObservable<float> GetObservable(AttributeProcessor localProcessor)
        {
            if (Mode == SourceMode.Constant)
                return Observable.Return(ConstantValue);

            return localProcessor.GetAttributeObservable(AttributePath)
                .SelectMany(attr => attr.ReactivePropertyAccess);
        }
    }
}