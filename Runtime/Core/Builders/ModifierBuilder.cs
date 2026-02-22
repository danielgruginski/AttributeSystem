using ReactiveSolutions.AttributeSystem.Core.Data;
using SemanticKeys;
using System;
using System.Collections.Generic;
using UnityEngine;
using sk;

namespace ReactiveSolutions.AttributeSystem.Core.Builders
{

    /// <summary>
    /// A fluent API for building complex AttributeModifierSpecs.
    /// Includes fast-path helpers for common 90% use cases (Flat, Multiplier, Linear).
    /// </summary>
    public class ModifierBuilder
    {
        private AttributeModifierSpec _spec;

        private ModifierBuilder()
        {
            _spec = new AttributeModifierSpec
            {
                Arguments = new List<ValueSource>(),
                TargetPath = new List<SemanticKey>(),
                Priority = 10 // Default priority
            };
        }

        public static ModifierBuilder Create() => new ModifierBuilder();

        public ModifierBuilder SetTarget(SemanticKey targetAttr, params SemanticKey[] remotePath)
        {
            _spec.TargetAttribute = targetAttr;
            if (remotePath != null && remotePath.Length > 0)
            {
                _spec.TargetPath = new List<SemanticKey>(remotePath);
            }
            return this;
        }

        public ModifierBuilder SetSourceId(SemanticKey sourceId)
        {
            _spec.SourceId = sourceId;
            return this;
        }

        public ModifierBuilder SetLogic(SemanticKey logicType, ModifierType modType, int priority = 10)
        {
            _spec.LogicType = logicType;
            _spec.Type = modType;
            _spec.Priority = priority;
            return this;
        }

        public ModifierBuilder AddConstantArg(float value)
        {
            _spec.Arguments.Add(new ValueSource { Mode = ValueSource.SourceMode.Constant, ConstantValue = value });
            return this;
        }

        public ModifierBuilder AddAttributeArg(SemanticKey targetAttr, params SemanticKey[] path)
        {
            var listPath = path != null ? new List<SemanticKey>(path) : new List<SemanticKey>();
            _spec.Arguments.Add(new ValueSource
            {
                Mode = ValueSource.SourceMode.Attribute,
                AttributeRef = new AttributeReference { Name = targetAttr, Path = listPath }
            });
            return this;
        }

        // --- 90% Helper Methods ---

        /// <summary>
        /// Quickly configures this modifier as a Flat Additive modifier (e.g., +10 Health).
        /// </summary>
        public ModifierBuilder MakeFlat(SemanticKey targetAttr, float value, params SemanticKey[] targetPath)
        {
            return SetTarget(targetAttr, targetPath)
                  .SetLogic(sk.Modifiers.Static, ModifierType.Additive)
                  .AddConstantArg(value);
        }

        /// <summary>
        /// Quickly configures this modifier as a Multiplier (e.g., 0.5f for +50% scaling).
        /// </summary>
        public ModifierBuilder MakeMultiplier(SemanticKey targetAttr, float percentage, params SemanticKey[] targetPath)
        {
            return SetTarget(targetAttr, targetPath)
                  .SetLogic(sk.Modifiers.Static, ModifierType.Multiplicative)
                  .AddConstantArg(percentage);
        }

        /// <summary>
        /// Quickly configures this modifier to linearly scale based on another attribute.
        /// (e.g., Add [Source * Multiplier] to Target).
        /// </summary>
        /// <param name="targetAttr">The attribute to modify (e.g., "Damage")</param>
        /// <param name="sourceAttr">The attribute to scale from (e.g., "Strength")</param>
        /// <param name="multiplier">How much of the source to use (e.g., 0.5f)</param>
        /// <param name="sourcePath">Optional path to the source (e.g., "Owner")</param>
        public ModifierBuilder MakeLinearScaling(SemanticKey targetAttr, SemanticKey sourceAttr, float multiplier, params SemanticKey[] sourcePath)
        {
            return SetTarget(targetAttr)
                  .SetLogic(sk.Modifiers.Linear, ModifierType.Additive) // Assuming "Linear" is registered in your factory
                  .AddAttributeArg(sourceAttr, sourcePath) // Arg 0: The attribute reference
                  .AddConstantArg(multiplier);             // Arg 1: The coefficient
        }

        /// <summary>
        /// Quickly configures this modifier to linearly scale based on another attribute, and applies it to a remote target.
        /// </summary>
        public ModifierBuilder MakeRemoteLinearScaling(SemanticKey targetAttr, SemanticKey[] targetPath, SemanticKey sourceAttr, float multiplier, params SemanticKey[] sourcePath)
        {
            return SetTarget(targetAttr, targetPath)
                  .SetLogic(sk.Modifiers.Linear, ModifierType.Additive)
                  .AddAttributeArg(sourceAttr, sourcePath)
                  .AddConstantArg(multiplier);
        }

        public AttributeModifierSpec Build() => _spec;
    }
}