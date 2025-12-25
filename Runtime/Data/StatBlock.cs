using System.Collections.Generic;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Core.Data
{
    /// <summary>
    /// A pure data class representing an entity's statistics.
    /// Because this is a plain class (POCO), it is garbage collected automatically
    /// and perfect for deserializing from JSON at runtime.
    /// </summary>
    [System.Serializable]
    public class StatBlock
    {
        [System.Serializable]
        public struct BaseValueEntry
        {
            [AttributeName] public string Name;
            public float Value;
        }

        public List<BaseValueEntry> BaseValues = new List<BaseValueEntry>();
        public List<AttributeModifierSpec> Modifiers = new List<AttributeModifierSpec>();

        /// <summary>
        /// Populates a processor with the data from this block.
        /// </summary>
        public void ApplyToProcessor(AttributeProcessor processor)
        {
            // 1. Set Base Values
            foreach (var entry in BaseValues)
            {
                if (!string.IsNullOrEmpty(entry.Name))
                {
                    processor.SetOrUpdateBaseValue(entry.Name, entry.Value);
                }
            }

            // 2. Apply Modifiers
            foreach (var spec in Modifiers)
            {
                var modifier = spec.CreateModifier();
                processor.AddModifier(spec.SourceId, modifier, spec.TargetAttribute);
            }
        }
    }
}