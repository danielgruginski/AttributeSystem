using System.Collections.Generic;
using System;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Core
{
    // 1. HOW applied: How does this modifier combine with the previous value?
    public enum AttributeMergeMode
    {
        Add,        // Value += Result
        Multiply,   // Value *= Result
        Override    // Value = Result
    }

    // 2. WHAT applied: What is the math logic?
    public enum AttributeLogicType
    {
        // Basic
        Constant,       // Just a raw number

        // Formulas
        Linear,         // (Coeff * Attribute) + Addend
        Exponential,    // Base ^ Attribute

        // Advanced
        Ratio,          // A / B
        Product,        // A * B
        Curve,          // AnimationCurve(Attribute)

        // Complex Curves
        DiminishingReturns, // MaxBonus * (Input / (Input + SoftCap))
        Segmented,          // Piecewise linear interpolation based on breakpoints
        TriangularBonus,    // Floor((sqrt(8 * Input + 1) - 1) / 2)

        // Transformers
        Clamp,
    }
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
        IObservable<float> GetMagnitude(AttributeProcessor processor);
    }

}
