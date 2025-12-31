using System.Collections.Generic;
using ReactiveSolutions.AttributeSystem.Core.Data;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Core
{
    /// <summary>
    /// A wrapper passed to the concrete modifier factories.
    /// Provides safe access to the generic argument list.
    /// </summary>
    public struct ModifierArgs
    {
        public string SourceId;
        public ModifierType Type;
        public int Priority;
        public List<ValueSource> Arguments;

        public ModifierArgs(string sourceId, ModifierType type, int priority, List<ValueSource> arguments)
        {
            SourceId = sourceId;
            Type = type;
            Priority = priority;
            Arguments = arguments ?? new List<ValueSource>();
        }

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