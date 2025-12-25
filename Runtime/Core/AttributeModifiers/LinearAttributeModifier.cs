using System;
using UniRx;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Core.Modifiers
{
    /// <summary>
    /// A reactive modifier that applies a linear transformation: (Source * Coeff) + Addend.
    /// Includes a SourceId to allow for explicit removal by the AttributeProcessor.
    /// </summary>
    [Serializable]
    public class LinearAttributeModifier : IAttributeModifier
    {
        [SerializeField] private string _sourceId;
        [SerializeField] private ModifierType _type = ModifierType.Additive;
        [SerializeField] private int _priority = 0;

        [Header("Math: (Source * Coeff) + Addend")]
        [SerializeField] private ValueSource _source;
        [SerializeField] private float _coeff = 1.0f;
        [SerializeField] private float _addend = 0.0f;

        public string SourceId => _sourceId;
        public ModifierType Type => _type;
        public int Priority => _priority;

        public LinearAttributeModifier(string sourceId, ModifierType type, int priority, ValueSource source, float coeff, float addend)
        {
            _sourceId = sourceId;
            _type = type;
            _priority = priority;
            _source = source;
            _coeff = coeff;
            _addend = addend;
        }

        public IObservable<float> GetMagnitude(AttributeProcessor processor)
        {
            // We take the reactive stream from the ValueSource and 
            // apply our linear math to every emitted value.
            return _source.GetObservable(processor)
                .Select(input => (input * _coeff) + _addend);
        }
    }
}
