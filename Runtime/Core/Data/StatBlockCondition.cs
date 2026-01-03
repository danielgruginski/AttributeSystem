using SemanticKeys;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Core.Data
{
    [Serializable]
    public class StatBlockCondition
    {
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
        public float Tolerance = 0.001f;

        // --- Composite Settings ---
        [Tooltip("Logical operator for combining sub-conditions.")]
        public Operator GroupOp;
        [SerializeReference]
        [Tooltip("List of sub-conditions.")]
        public List<StatBlockCondition> SubConditions = new List<StatBlockCondition>();
    }
}