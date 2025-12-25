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

    /// <summary>
    /// The contract for any logic that modifies an attribute.
    /// Now includes lifecycle hooks for self-managed reactivity.
    /// </summary>
    public interface IAttributeModifier : IDisposable
    {
        int Priority { get; }
        string SourceId { get; }

        /// <summary>
        /// Applies the modification to the current value.
        /// </summary>
        float Apply(float currentValue, AttributeProcessor controller);

        /// <summary>
        /// Called when the modifier is added to an Attribute.
        /// Use this to subscribe to other attributes (dependencies).
        /// </summary>
        void OnAttach(Attribute targetAttribute, AttributeProcessor controller);

        /// <summary>
        /// Called when the modifier is removed. 
        /// (Dispose handles cleanup, this is for explicit logic if needed).
        /// </summary>
        void OnDetach();
    }


    [Serializable]
    public struct ValueSource
    {
        public enum SourceType { Constant, Attribute }
        public SourceType Type;
        public float ConstantValue;
        public string AttributeName;

        public float GetValue(AttributeProcessor controller)
        {
            return Type switch
            {
                SourceType.Constant => ConstantValue,
                SourceType.Attribute => controller.GetOrCreateAttribute(AttributeName)?.Value ?? 0f,
                _ => 0f,
            };
        }

        public string GetAttributeName() => Type == SourceType.Attribute ? AttributeName : null;
    }
}
