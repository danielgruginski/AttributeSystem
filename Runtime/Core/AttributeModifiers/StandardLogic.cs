using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

namespace ReactiveSolutions.AttributeSystem.Core.Modifiers
{
    // The built-in logic types. Each input is a ValueSource: a constant or an attribute's value.
    // [Preserve] keeps code stripping from removing the classes that only JSON files name.

    /// <summary>The value itself: "+5 Damage" (a constant) or "+ Strength" (an attribute).</summary>
    [Serializable, Preserve]
    public class ValueLogic : FormulaLogic
    {
        [Tooltip("A constant, or an attribute's value.")]
        public ValueSource Value = ValueSource.Const(0f);

        public ValueLogic() { }
        public ValueLogic(ValueSource value) { Value = value; }

        protected override IEnumerable<ValueSource> Inputs => new[] { Value };
        protected override float Compute(IList<float> inputs) => inputs[0];
    }

    /// <summary>Input * Coefficient + Addend.</summary>
    [Serializable, Preserve]
    public class LinearLogic : FormulaLogic
    {
        public ValueSource Input = ValueSource.Const(0f);
        public ValueSource Coefficient = ValueSource.Const(1f);
        public ValueSource Addend = ValueSource.Const(0f);

        protected override IEnumerable<ValueSource> Inputs => new[] { Input, Coefficient, Addend };
        protected override float Compute(IList<float> inputs) => inputs[0] * inputs[1] + inputs[2];
    }

    /// <summary>Input ^ Power * Scale + Flat.</summary>
    [Serializable, Preserve]
    public class PolynomialLogic : FormulaLogic
    {
        public ValueSource Input = ValueSource.Const(0f);
        public ValueSource Power = ValueSource.Const(1f);
        public ValueSource Scale = ValueSource.Const(1f);
        public ValueSource Flat = ValueSource.Const(0f);

        protected override IEnumerable<ValueSource> Inputs => new[] { Input, Power, Scale, Flat };
        protected override float Compute(IList<float> inputs) => Mathf.Pow(inputs[0], inputs[1]) * inputs[2] + inputs[3];
    }

    /// <summary>
    /// Input limited to [Min, Max]. To limit the attribute itself (e.g. Health to MaxHealth), use a modifier of
    /// Type Clamp Max instead.
    /// </summary>
    [Serializable, Preserve]
    public class ClampLogic : FormulaLogic
    {
        public ValueSource Input = ValueSource.Const(0f);
        public ValueSource Min = ValueSource.Const(0f);
        public ValueSource Max = ValueSource.Const(0f);

        protected override IEnumerable<ValueSource> Inputs => new[] { Input, Min, Max };
        protected override float Compute(IList<float> inputs) => Mathf.Clamp(inputs[0], inputs[1], inputs[2]);
    }

    /// <summary>The smaller of A and B.</summary>
    [Serializable, Preserve]
    public class MinLogic : FormulaLogic
    {
        public ValueSource A = ValueSource.Const(0f);
        public ValueSource B = ValueSource.Const(0f);

        protected override IEnumerable<ValueSource> Inputs => new[] { A, B };
        protected override float Compute(IList<float> inputs) => Mathf.Min(inputs[0], inputs[1]);
    }

    /// <summary>The larger of A and B.</summary>
    [Serializable, Preserve]
    public class MaxLogic : FormulaLogic
    {
        public ValueSource A = ValueSource.Const(0f);
        public ValueSource B = ValueSource.Const(0f);

        protected override IEnumerable<ValueSource> Inputs => new[] { A, B };
        protected override float Compute(IList<float> inputs) => Mathf.Max(inputs[0], inputs[1]);
    }

    /// <summary>Input rounded down to a whole number.</summary>
    [Serializable, Preserve]
    public class FloorLogic : FormulaLogic
    {
        public ValueSource Input = ValueSource.Const(0f);

        protected override IEnumerable<ValueSource> Inputs => new[] { Input };
        protected override float Compute(IList<float> inputs) => Mathf.Floor(inputs[0]);
    }

    /// <summary>1 if Input is at least Threshold, otherwise 0.</summary>
    [Serializable, Preserve]
    public class StepLogic : FormulaLogic
    {
        public ValueSource Input = ValueSource.Const(0f);
        public ValueSource Threshold = ValueSource.Const(0f);

        protected override IEnumerable<ValueSource> Inputs => new[] { Input, Threshold };
        protected override float Compute(IList<float> inputs) => inputs[0] >= inputs[1] ? 1f : 0f;
    }

    /// <summary>Dividend / Divisor, or Dividend when the divisor is (almost) 0.</summary>
    [Serializable, Preserve]
    public class RatioLogic : FormulaLogic
    {
        public ValueSource Dividend = ValueSource.Const(0f);
        public ValueSource Divisor = ValueSource.Const(1f);

        protected override IEnumerable<ValueSource> Inputs => new[] { Dividend, Divisor };
        protected override float Compute(IList<float> inputs) =>
            Mathf.Abs(inputs[1]) < 0.0001f ? inputs[0] : inputs[0] / inputs[1];
    }

    /// <summary>Base ^ Exponent.</summary>
    [Serializable, Preserve]
    public class ExponentialLogic : FormulaLogic
    {
        public ValueSource Base = ValueSource.Const(1f);
        public ValueSource Exponent = ValueSource.Const(1f);

        protected override IEnumerable<ValueSource> Inputs => new[] { Base, Exponent };
        protected override float Compute(IList<float> inputs) => Mathf.Pow(inputs[0], inputs[1]);
    }

    /// <summary>
    /// Max Bonus * Input / (Input + Soft Cap): grows quickly at first and approaches Max Bonus.
    /// A negative input counts as 0, and the result is 0 when Input + Soft Cap is 0 or less.
    /// </summary>
    [Serializable, Preserve]
    public class DiminishingReturnsLogic : FormulaLogic
    {
        public ValueSource Input = ValueSource.Const(0f);
        public ValueSource MaxBonus = ValueSource.Const(0f);
        [Tooltip("The input at which the bonus reaches half of Max Bonus.")]
        public ValueSource SoftCap = ValueSource.Const(0f);

        protected override IEnumerable<ValueSource> Inputs => new[] { Input, MaxBonus, SoftCap };

        protected override float Compute(IList<float> inputs)
        {
            float input = Mathf.Max(0f, inputs[0]);
            float denominator = input + inputs[2];
            return denominator > 0f ? inputs[1] * (input / denominator) : 0f;
        }
    }

    /// <summary>
    /// Scale * 0.5 * (sqrt(1 + 8 * Input / Scale) - 1), never more than Input: like "1 + 2 + 3 + ..." points per level,
    /// stretched by Scale. A negative input counts as 0.
    /// </summary>
    [Serializable, Preserve]
    public class ScaledTriangularLogic : FormulaLogic
    {
        public ValueSource Input = ValueSource.Const(0f);
        public ValueSource Scale = ValueSource.Const(1f);

        protected override IEnumerable<ValueSource> Inputs => new[] { Input, Scale };

        protected override float Compute(IList<float> inputs)
        {
            float input = Mathf.Max(0f, inputs[0]);
            float scale = Mathf.Max(0.0001f, inputs[1]);
            float curve = scale * (Mathf.Sqrt(1f + 8f * input / scale) - 1f) * 0.5f;
            return Mathf.Min(input, curve);
        }
    }

    /// <summary>
    /// Breakpoints: the Value of the highest Threshold that Input reaches, or Default below all of them
    /// (e.g. *1.5 damage from 10 Strength, *2 from 20).
    /// </summary>
    [Serializable, Preserve]
    public class SegmentedLogic : FormulaLogic
    {
        [Serializable]
        public struct Segment
        {
            [Tooltip("The minimum input for this segment.")]
            public float Threshold;
            [Tooltip("The value while the input is in this segment.")]
            public float Value;
        }

        public ValueSource Input = ValueSource.Const(0f);
        [Tooltip("The value while the input is below every threshold.")]
        public float Default = 1f;
        public List<Segment> Segments = new List<Segment>();

        protected override IEnumerable<ValueSource> Inputs => new[] { Input };

        protected override float Compute(IList<float> inputs)
        {
            float value = Default;
            float bestThreshold = float.NegativeInfinity;
            foreach (var segment in Segments)
            {
                if (inputs[0] >= segment.Threshold && segment.Threshold >= bestThreshold)
                {
                    bestThreshold = segment.Threshold;
                    value = segment.Value;
                }
            }
            return value;
        }

        public override ModifierLogic Clone()
        {
            var copy = (SegmentedLogic)base.Clone();
            copy.Segments = new List<Segment>(Segments);
            return copy;
        }
    }
}