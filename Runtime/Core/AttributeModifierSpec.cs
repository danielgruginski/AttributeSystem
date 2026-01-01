using ReactiveSolutions.AttributeSystem.Core.Modifiers;
using SemanticKeys;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Core
{
    [Serializable]
    public class AttributeModifierSpec
    {
        [Header("Target")]
        public SemanticKey TargetAttribute;
        public List<SemanticKey> TargetPath = new List<SemanticKey>();
        public string SourceId;

        [Header("Pipeline")]
        public ModifierType Type = ModifierType.Additive;
        public int Priority = 0;

        [Header("Logic")]
        //[SemanticKeyFilter("Modifiers")]
        public SemanticKey LogicType;

        [Header("Unified Arguments")]
        [Tooltip("Define all inputs here. Use Mode=Constant for static values, Mode=Attribute for dynamic ones.")]
        public List<ValueSource> Arguments = new List<ValueSource>();

        /// <summary>
        /// Safely retrieves an argument at the specified index.
        /// Returns a default Constant(0) ValueSource if the index is missing.
        /// </summary>
        public ValueSource GetSafe(int index, float defaultConstant = 0f)
        {
            if (Arguments != null && index >= 0 && index < Arguments.Count)
            {
                return Arguments[index];
            }

            // Return a safe fallback to prevent crashes
            return new ValueSource
            {
                Mode = ValueSource.SourceMode.Constant,
                ConstantValue = defaultConstant
            };
        }

        /// <summary>
        /// Validates if the required number of arguments exists.
        /// </summary>
        public bool ValidateArgCount(int requiredCount, string logicType)
        {
            if (Arguments.Count < requiredCount)
            {
                Debug.LogWarning($"[ModifierFactory] '{logicType}' expects {requiredCount} arguments, but found {Arguments.Count}. Using defaults for missing args.");
                return false;
            }
            return true;
        }

    }
}