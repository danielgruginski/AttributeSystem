using Mono.Cecil;
using ReactiveSolutions.AttributeSystem.Core.Modifiers;
using System;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Core.Data
{
    /// <summary>
    /// A serializable DTO (Data Transfer Object) used in StatBlocks or JSON.
    /// This acts as the "Blueprint" for a modifier.
    /// </summary>
    [Serializable]
    public class AttributeModifierSpec
    {
        public enum ModifierCategory { Linear, SegmentedMultiplier }

        [Header("Identity")]
        [AttributeName] public string TargetAttribute;
        public string SourceId;

        [Header("Pipeline Settings")]
        public ModifierCategory Category = ModifierCategory.Linear;
        public ModifierType Type = ModifierType.Additive;
        public int Priority = 0;

        [Header("Value Source Data")]
        public ValueSource.SourceMode SourceMode = ValueSource.SourceMode.Constant;
        public float ConstantValue;
        public string AttributePath;

        [Header("Linear Math Parameters")]
        public float Coeff = 1f;
        public float Addend = 0f;

        /// <summary>
        /// Converts this data spec into a functional reactive modifier.
        /// This replaces the need for a separate Factory class.
        /// </summary>
        public IAttributeModifier CreateModifier()
        {
            // Build the ValueSource from the flat fields
            var source = new ValueSource
            {
                Mode = SourceMode,
                ConstantValue = ConstantValue,
                AttributePath = AttributePath
            };

            switch (Category)
            {
                case ModifierCategory.Linear:
                    return new LinearAttributeModifier(SourceId, Type, Priority, source, Coeff, Addend);

                // Note: If you add more complex modifiers (like Segmented), 
                // you would map them here or use [SerializeReference] for even cleaner code.

                default:
                    throw new NotImplementedException($"[ModifierSpec] Category {Category} is not implemented.");
            }
        }

        /// <summary>
        /// Helper to create a default spec for the JSON Authoring window.
        /// </summary>
        public static AttributeModifierSpec CreateDefault()
        {
            return new AttributeModifierSpec
            {
                TargetAttribute = "",
                SourceId = "NewSource",
                Category = ModifierCategory.Linear,
                Type = ModifierType.Additive,
                ConstantValue = 1.0f
            };
        }
    }
}