using SemanticKeys;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Core.Data
{
    [Serializable]
    public class StatBlockCondition
    {
        /// <summary>The default error margin of Equal and NotEqual comparisons.</summary>
        public const float DefaultTolerance = 0.001f;

        public enum Mode
        {
            Always = 0,
            Tag = 1,
            ValueComparison = 2,
            Composite = 3
        }

        public enum Operator
        {
            And = 0,
            Or = 1
        }

        public enum Comparison
        {
            Equal = 0,
            NotEqual = 1,
            Greater = 2,
            Less = 3,
            GreaterOrEqual = 4,
            LessOrEqual = 5
        }

        [Tooltip("The logic mode of this condition.")]
        public Mode Type = Mode.Always;

        // --- Tag Settings ---
        [Tooltip("The tag to check for.")]
        public SemanticKey Tag;

        [Tooltip("Target to check the tag on. Leave Path empty for Self.")]
        //public AttributeReference TagTarget;
        public List<SemanticKey> TagTarget;

        [Tooltip("If true, returns true when the tag is MISSING.")]
        public bool InvertTag;

        // --- Value Settings ---
        [Tooltip("Left operand.")]
        public ValueSource ValueA;
        [Tooltip("Comparison operator.")]
        public Comparison CompareOp;
        [Tooltip("Right operand.")]
        public ValueSource ValueB;

        [Tooltip("Error margin for equality checks.")]
        public float Tolerance = DefaultTolerance;

        // --- Composite Settings ---
        [Tooltip("Logical operator for combining sub-conditions.")]
        public Operator GroupOp;
        [SerializeReference]
        [Tooltip("List of sub-conditions.")]
        public List<StatBlockCondition> SubConditions = new List<StatBlockCondition>();

        /// <summary>Always true (the default).</summary>
        public static StatBlockCondition Always() => new StatBlockCondition { Type = Mode.Always };

        /// <summary>
        /// True while the entity has <paramref name="tag"/>. With a <paramref name="path"/> (e.g. Links.Owner), the tag
        /// is checked on the entity at the end of it instead.
        /// </summary>
        public static StatBlockCondition HasTag(SemanticKey tag, params SemanticKey[] path) => TagCondition(tag, path, invert: false);

        /// <summary>True while the entity (or the one at the end of <paramref name="path"/>) doesn't have <paramref name="tag"/>.</summary>
        public static StatBlockCondition LacksTag(SemanticKey tag, params SemanticKey[] path) => TagCondition(tag, path, invert: true);

        /// <summary>
        /// True while "<paramref name="a"/> <paramref name="op"/> <paramref name="b"/>" holds, e.g.
        /// <c>Compare(ValueSource.FromAttribute(Stats.Health), Comparison.Less, 50)</c>. Equal and NotEqual allow a
        /// difference of up to <paramref name="tolerance"/>.
        /// </summary>
        public static StatBlockCondition Compare(ValueSource a, Comparison op, ValueSource b, float tolerance = DefaultTolerance) =>
            new StatBlockCondition { Type = Mode.ValueComparison, ValueA = a, CompareOp = op, ValueB = b, Tolerance = tolerance };

        /// <summary>True while all of <paramref name="conditions"/> are (true if there are none).</summary>
        public static StatBlockCondition All(params StatBlockCondition[] conditions) => Composite(Operator.And, conditions);

        /// <summary>True while any of <paramref name="conditions"/> is (true if there are none).</summary>
        public static StatBlockCondition Any(params StatBlockCondition[] conditions) => Composite(Operator.Or, conditions);

        private static StatBlockCondition TagCondition(SemanticKey tag, SemanticKey[] path, bool invert) => new StatBlockCondition
        {
            Type = Mode.Tag,
            Tag = tag,
            TagTarget = new List<SemanticKey>(path ?? new SemanticKey[0]),
            InvertTag = invert
        };

        private static StatBlockCondition Composite(Operator op, StatBlockCondition[] conditions) => new StatBlockCondition
        {
            Type = Mode.Composite,
            GroupOp = op,
            SubConditions = new List<StatBlockCondition>(conditions ?? new StatBlockCondition[0])
        };
    }
}