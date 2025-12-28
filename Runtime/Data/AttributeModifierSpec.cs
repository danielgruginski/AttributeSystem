using Mono.Cecil;
using ReactiveSolutions.AttributeSystem.Core.Modifiers;
using SemanticKeys;
using System;
using System.Collections.Generic;
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

        [Header("Target Identity")]
        [Tooltip("The name of the attribute to modify.")]
        public SemanticKey TargetAttribute;
        [Tooltip("Path to the provider hosting the target attribute. Empty = Local.")]
        public List<SemanticKey> TargetPath = new List<SemanticKey>(); // New Field for Target Path

        public string SourceId;

        [Header("Pipeline Settings")]
        public ModifierCategory Category = ModifierCategory.Linear;
        public ModifierType Type = ModifierType.Additive;
        public int Priority = 0;

        [Header("Value Source Data")]
        public ValueSource.SourceMode SourceMode = ValueSource.SourceMode.Constant;
        public float ConstantValue;

        public SemanticKey AttributeName; // Was 'AttributePath'
        public List<SemanticKey> ProviderPath = new List<SemanticKey>(); // New field

        [Header("Linear Math Parameters")]
        public float Coeff = 1f;
        public float Addend = 0f;


        /// <summary>
        /// Converts this data spec into a functional reactive modifier.
        /// Optionally accepts a 'context' (the processor creating this modifier) to bake into the source.
        /// </summary>

        public IAttributeModifier CreateModifier(AttributeProcessor context = null)
        {
            var source = new ValueSource
            {
                Mode = SourceMode,
                ConstantValue = ConstantValue,
                AttributeName = AttributeName,
                ProviderPath = new List<SemanticKey>(ProviderPath) // Deep copy
            };

            if (context != null)
            {
                source.BakeContext(context);
            }

            switch (Category)
            {
                case ModifierCategory.Linear:
                    return new LinearAttributeModifier(SourceId, Type, Priority, source, Coeff, Addend);

                default:
                    return new LinearAttributeModifier(SourceId, Type, Priority, source, 1f, 0f);
            }
        }

        /// <summary>
        /// Helper to create a default spec for the JSON Authoring window.
        /// </summary>
        public static AttributeModifierSpec CreateDefault()
        {
            return new AttributeModifierSpec
            {
                TargetAttribute = SemanticKey.None,
                SourceId = "NewSource",
                Category = ModifierCategory.Linear,
                Type = ModifierType.Additive,
                ConstantValue = 1.0f
            };
        }
    }
}