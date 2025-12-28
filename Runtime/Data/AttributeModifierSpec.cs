using ReactiveSolutions.AttributeSystem.Core.Modifiers;
using SemanticKeys;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Core.Data
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
        public SemanticKey LogicType;

        [Header("Unified Arguments")]
        [Tooltip("Define all inputs here. Use Mode=Constant for static values, Mode=Attribute for dynamic ones.")]
        public List<ValueSource> Arguments = new List<ValueSource>();

        // --- LEGACY FIELDS ---
        // Kept to ensure Unity doesn't delete data on deserialization.
        // The CreateModifier method migrates these into 'Arguments' at runtime.
        [HideInInspector] public List<ValueSource> Inputs;
        [HideInInspector] public List<float> Constants;
        [HideInInspector] public ModifierCategory Category;
        [HideInInspector] public float Coeff = 1f;
        [HideInInspector] public float Addend = 0f;
        [HideInInspector] public float ConstantValue;
        [HideInInspector] public ValueSource.SourceMode SourceMode;
        // ---------------------

        /// <summary>
        /// Creates the modifier using the provided Factory service.
        /// </summary>
        public IAttributeModifier CreateModifier(IModifierFactory factory, AttributeProcessor context = null)
        {
            // 1. Migrate (Legacy support)
            var finalArgs = new List<ValueSource>();
            if (Arguments != null && Arguments.Count > 0) finalArgs.AddRange(Arguments);
            else MigrateLegacyData(finalArgs);

            // 2. Bake Context
            if (context != null)
            {
                foreach (var arg in finalArgs) arg.BakeContext(context);
            }

            // 3. Create via INJECTED Factory
            var args = new ModifierArgs(SourceId, Type, Priority, finalArgs);
            return factory.Create(LogicType, args);
        }

        private void MigrateLegacyData(List<ValueSource> targetList)
        {
            if (Inputs != null && Inputs.Count > 0) targetList.AddRange(Inputs);
            if (Constants != null && Constants.Count > 0)
                targetList.AddRange(Constants.Select(c => new ValueSource { Mode = ValueSource.SourceMode.Constant, ConstantValue = c }));

            if (targetList.Count == 0)
            {
                if (Category == ModifierCategory.Linear)
                {
                    LogicType = sk.Modifiers.Linear;
                    targetList.Add(new ValueSource { Mode = SourceMode, ConstantValue = ConstantValue });
                    targetList.Add(new ValueSource { Mode = ValueSource.SourceMode.Constant, ConstantValue = Coeff });
                    targetList.Add(new ValueSource { Mode = ValueSource.SourceMode.Constant, ConstantValue = Addend });
                }
                else
                {
                    LogicType = sk.Modifiers.Static;
                    targetList.Add(new ValueSource { Mode = ValueSource.SourceMode.Constant, ConstantValue = ConstantValue });
                }
            }
        }
    }

    public enum ModifierCategory { Linear, SegmentedMultiplier }
}