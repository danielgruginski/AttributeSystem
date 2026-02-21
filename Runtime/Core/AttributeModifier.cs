using System.Collections.Generic;
using System;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Core
{

    public enum ModifierType
    {
        Additive,        // Added to the base value or other additives
        Multiplicative,   // Multiplies the sum of base + additives
        Override         // Replaces the value (Highest priority wins)
    }

    /// <summary>
    /// The contract for any logic that modifies an attribute.
    /// Now includes lifecycle hooks for self-managed reactivity.
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
