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
        /// Populates a processor. Requires a ModifierFactory to resolve logic types.
        /// </summary>
        public void ApplyToProcessor(AttributeProcessor processor, IModifierFactory factory)
        {
            // Fallback for convenience/tests if you don't want to pass it every time
            factory ??= new ModifierFactory();

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
                // Pass the factory down to the spec
                var modifier = factory.Create(spec, processor);
                if(modifier != null)
                {
                    processor.AddModifier(spec.SourceId, modifier, spec.TargetAttribute, spec.TargetPath);
                }
                else {
                    Debug.LogWarning($"StatBlock.ApplyToProcessor: Could not create modifier of type '{spec.LogicType}' for attribute '{spec.TargetAttribute}'. Check that the type is registered in the factory.");
                }

            }
        }
    }
}