using System;

namespace ReactiveSolutions.AttributeSystem.Core
{
    /// <summary>
    /// What a modifier's value does to the attribute. Modifiers apply one after another (see Attribute), each to
    /// the value the previous ones produced.
    /// </summary>
    public enum ModifierType
    {
        Additive,       // Adds the value
        Multiplicative, // Multiplies by the value (each multiplier applies on its own: +10% and +10% make x1.21)
        Override,       // Replaces the value
        ClampMin,       // Keeps the value at least this high
        ClampMax        // Keeps the value at most this high (e.g. Health at most MaxHealth)
    }

    /// <summary>
    /// The contract for any logic that modifies an attribute.
    /// </summary>
    public interface IAttributeModifier
    {
        ModifierType Type { get; }
        int Priority { get; }
        public string SourceId { get; }

        /// <summary>
        /// The live magnitude of the modifier.
        /// This observable fires whenever the modifier's internal source changes.
        /// </summary>
        IObservable<float> GetMagnitude(Entity processor);
    }
}