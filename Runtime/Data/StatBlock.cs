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
        /// Populates a processor and returns an ActiveStatBlock handle to manage the lifecycle of applied modifiers.
        /// </summary>
        public ActiveStatBlock ApplyToProcessor(AttributeProcessor processor, IModifierFactory factory)
        {
            factory ??= new ModifierFactory();
            var activeBlock = new ActiveStatBlock();

            // 1. Set Base Values (Permanent for the session, generally not reverted by ActiveStatBlock)
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
                var modifier = factory.Create(spec, processor);
                if (modifier != null)
                {
                    // Capture the handle!
                    var handle = processor.AddModifier(spec.SourceId, modifier, spec.TargetAttribute, spec.TargetPath);
                    activeBlock.AddHandle(handle);
                }
                else
                {
                    Debug.LogWarning($"StatBlock.ApplyToProcessor: Could not create modifier of type '{spec.LogicType}'");
                }
            }

            return activeBlock;
        }
    }
}