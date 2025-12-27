using System;
using UniRx;

namespace ReactiveSolutions.AttributeSystem.Core
{

    /// <summary>
    /// A generic implementation that uses a lambda function to define its mathematical operation.
    /// This avoids "Class Explosion" for custom logic.
    /// </summary>
    public class FunctionalAttributeModifier : IAttributeModifier
    {
        public ModifierType Type { get; }
        public int Priority { get; }

        string IAttributeModifier.SourceId => _sourceId;

        private readonly ValueSource _source;
        private readonly Func<float, float> _operation;
        private string _sourceId;

        /// <summary>
        /// Creates a modifier where the magnitude is defined by a lambda.
        /// Example: (val) => (val * 2) + 10
        /// </summary>
        public FunctionalAttributeModifier(
            string sourceId,
            ValueSource source,
            Func<float, float> operation,
            ModifierType type = ModifierType.Additive,
            int priority = 0)
        {   _sourceId = sourceId;
            _source = source;
            _operation = operation;
            Type = type;
            Priority = priority;
        }

        public IObservable<float> GetMagnitude(AttributeProcessor processor)
        {
            return _source.GetObservable(processor)
                .Select(value => _operation(value));
        }
    }
}