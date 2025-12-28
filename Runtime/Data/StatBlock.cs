using SemanticKeys;
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
            public SemanticKey Name;
            public float Value;
        }

        public string BlockName = "New Block"; // Helper for editor naming
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
                // We pass 'processor' here so the modifier knows who created it (The Context).
                // This ensures that if the modifier target is remote (e.g. "Owner.Strength"),
                // the modifier source (e.g. "ItemLevel") is still resolved against THIS processor.
                var modifier = spec.CreateModifier(processor);
                processor.AddModifier(spec.SourceId, modifier, spec.TargetAttribute, spec.TargetPath);
            }
        }
    }
}