using ReactiveSolutions.AttributeSystem.Core;
using ReactiveSolutions.AttributeSystem.Core.Data;
using ReactiveSolutions.AttributeSystem.Core.Modifiers;
using SemanticKeys;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Unity
{
    /// <summary>
    /// Extension methods providing a developer-friendly API for adding modifiers.
    /// Acts as a facade over the Core AttributeProcessor.
    /// </summary>
    public static class AttributeControllerExtensions
    {
        // Helper to convert string to transient SemanticKey
        private static SemanticKey Key(string k) => new SemanticKey(k, k, "Legacy");
        private static SemanticKey Key(SemanticKey k) => k;

        /// <summary>
        /// Adds a simple constant modification.
        /// </summary>
        public static void AddFlatMod(this AttributeController controller, string sourceId, string targetAttr, float value, ModifierType type = ModifierType.Additive, int priority = 10)
        {
            var args = new ModifierArgs(sourceId, type, priority, new List<ValueSource> { ValueSource.Const(value) });
            var mod = new StaticAttributeModifier(args);
            controller.Processor.AddModifier(sourceId, mod, Key(targetAttr));
        }

        /// <summary>
        /// Logic: (Source * Coefficient) + Addend
        /// </summary>
        public static void AddLinearScaling(this AttributeController controller, string sourceId, string targetAttr, string sourceAttr, float coefficient, ModifierType type = ModifierType.Additive, int priority = 10, float addend = 0f)
        {
            var source = new ValueSource { Mode = ValueSource.SourceMode.Attribute, AttributeRef = new AttributeReference(Key(sourceAttr)) };

            var args = new ModifierArgs(sourceId, type, priority, new List<ValueSource>
            {
                source,
                ValueSource.Const(coefficient),
                ValueSource.Const(addend)
            });

            var mod = new LinearModifier(args);
            controller.Processor.AddModifier(sourceId, mod, Key(targetAttr));
        }

        /// <summary>
        /// Logic: Base ^ ExponentSource
        /// </summary>
        public static void AddExponentialScaling(this AttributeController controller, string sourceId, string targetAttr, string exponentAttr, float baseK, ModifierType type = ModifierType.Multiplicative, int priority = 100)
        {
            // We use the Factory's logic but instantiated manually for speed/safety
            var source = new ValueSource { Mode = ValueSource.SourceMode.Attribute, AttributeRef = new AttributeReference(Key(exponentAttr)) };

            var args = new ModifierArgs(sourceId, type, priority, new List<ValueSource>
            {
                source,
                ValueSource.Const(baseK)
            });

            // Replicating "Exponential" logic: Pow(Base, Exponent) -> Pow(args[1], args[0])
            var mod = new FunctionalModifier(args, vals => Mathf.Pow(vals[1], vals[0]));
            controller.Processor.AddModifier(sourceId, mod, Key(targetAttr));
        }

        /// <summary>
        /// Logic: Dividend / Divisor
        /// </summary>
        public static void AddRatio(this AttributeController controller, string sourceId, string targetAttr, string dividendAttr, string divisorAttr, ModifierType type = ModifierType.Multiplicative, int priority = 100)
        {
            var divSource = new ValueSource { Mode = ValueSource.SourceMode.Attribute, AttributeRef = new AttributeReference(Key(dividendAttr)) };
            var sorSource = new ValueSource { Mode = ValueSource.SourceMode.Attribute, AttributeRef = new AttributeReference(Key(divisorAttr)) };

            var args = new ModifierArgs(sourceId, type, priority, new List<ValueSource> { divSource, sorSource });

            var mod = new FunctionalModifier(args, vals =>
            {
                float d = vals[1];
                return (Mathf.Abs(d) < 0.0001f) ? vals[0] : vals[0] / d;
            });

            controller.Processor.AddModifier(sourceId, mod, Key(targetAttr));
        }

        /// <summary>
        /// Logic: MaxBonus * (Input / (Input + SoftCap))
        /// </summary>
        public static void AddDiminishingReturns(this AttributeController controller, string sourceId, string targetAttr, string inputAttr, float maxBonus, float softCapN, ModifierType type = ModifierType.Additive, int priority = 500)
        {
            var input = new ValueSource { Mode = ValueSource.SourceMode.Attribute, AttributeRef = new AttributeReference(Key(inputAttr)) };

            var args = new ModifierArgs(sourceId, type, priority, new List<ValueSource>
            {
                input,
                ValueSource.Const(maxBonus),
                ValueSource.Const(softCapN)
            });

            var mod = new FunctionalModifier(args, vals =>
            {
                float inp = Mathf.Max(0, vals[0]);
                float max = vals[1];
                float cap = vals[2];
                return max * (inp / (inp + cap));
            });

            controller.Processor.AddModifier(sourceId, mod, Key(targetAttr));
        }

        public static void AddSegmentedMultiplier(this AttributeController controller, string sourceId, string targetAttr, string inputAttr, Dictionary<float, float> breakpoints, ModifierType type = ModifierType.Multiplicative, int priority = 400)
        {
            var input = new ValueSource { Mode = ValueSource.SourceMode.Attribute, AttributeRef = new AttributeReference(Key(inputAttr)) };

            // Map Dict to List<Vector2> for sorting
            var sortedPoints = breakpoints.Select(kvp => new Vector2(kvp.Key, kvp.Value)).OrderByDescending(v => v.x).ToList();

            var args = new ModifierArgs(sourceId, type, priority, new List<ValueSource> { input });

            var mod = new FunctionalModifier(args, vals =>
            {
                float inp = vals[0];
                // Piecewise evaluation: descending check
                foreach (var point in sortedPoints)
                {
                    if (inp >= point.x) return point.y;
                }
                // Return value of lowest point if below all? Or 1? 
                // Legacy implementation returned lowest point if below all.
                if (sortedPoints.Count > 0) return sortedPoints.Last().y;
                return 1f;
            });

            controller.Processor.AddModifier(sourceId, mod, Key(targetAttr));
        }

        /// <summary>
        /// Logic: Scale * 0.5 * (sqrt(1 + 8 * Input / Scale) - 1)
        /// Creates a new attribute "{attributeName}Bonus" and adds the modifier to it.
        /// </summary>
        public static void SetupTriangularBonus(this AttributeController controller, string sourceID, string attributeName)
        {
            // 1. Target Bonus Attribute
            SemanticKey bonusAttr = Key(attributeName + "Bonus");

            // 2. Ensure Base exists
            controller.Processor.SetOrUpdateBaseValue(bonusAttr, 0f);

            // 3. Input Source
            var inputSource = new ValueSource { Mode = ValueSource.SourceMode.Attribute, AttributeRef = new AttributeReference(Key(attributeName)) };
            float scale = 1f;

            var args = new ModifierArgs(sourceID, ModifierType.Additive, 10, new List<ValueSource>
            {
                inputSource,
                ValueSource.Const(scale)
            });



            var mod = new FunctionalModifier(args, vals =>
            {
                float input = Mathf.Max(0, vals[0]);
                float s = Mathf.Max(0.0001f, vals[1]);
                float curve = s * (Mathf.Sqrt(1 + (8 * input) / s) - 1) * 0.5f;
                return Mathf.Min(input, curve);
            });

            controller.Processor.AddModifier(sourceID, mod, bonusAttr);
        }

        // Placeholder Clamp
        public static void AddClamp(this AttributeController controller, string sourceId, string targetAttr, float min, float max, int priority = 1000)
        {
            // Not implemented - legacy stub
            // You can add logic here similar to others using FunctionalModifier with sk.Modifiers.Clamp logic
        }
    }
}