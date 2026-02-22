using ReactiveSolutions.AttributeSystem.Core.Data;
using SemanticKeys;
using sk; // Included to match your test files (e.g., Modifiers.Static)
using System;
using System.Collections.Generic;
using Unity.VisualScripting.YamlDotNet.Core.Tokens;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Core.Builders
{

    /// <summary>
    /// A fluent API for constructing StatBlocks entirely through code.
    /// </summary>
    public class StatBlockBuilder
    {
        private readonly StatBlock _statBlock;

        private StatBlockBuilder()
        {
            _statBlock = new StatBlock();
            _statBlock.ActivationCondition = new StatBlockCondition { Type = StatBlockCondition.Mode.Always };
        }

        public static StatBlockBuilder Create(string name = "NewStatBlock")
        {
            var builder = new StatBlockBuilder();
            builder._statBlock.BlockName = name;
            return builder;
        }

        //private SemanticKey Key(string name) => new SemanticKey(name, name, null);

        private ValueSource Const(float val) => new ValueSource { Mode = ValueSource.SourceMode.Constant, ConstantValue = val };

        public StatBlockBuilder SetCondition(StatBlockCondition.Mode mode, SemanticKey tag, bool invert = false)
        {
            _statBlock.ActivationCondition = new StatBlockCondition
            {
                Type = mode,
                Tag = tag != null ? tag : SemanticKey.None,
                InvertTag = invert
            };
            return this;
        }

        public StatBlockBuilder AddModifier(SemanticKey targetAttr, SemanticKey logicType, ModifierType type, List<ValueSource> args, List<SemanticKey> path = null)
        {
            _statBlock.Modifiers.Add(new AttributeModifierSpec
            {
                TargetAttribute = targetAttr,
                LogicType = logicType,
                Type = type,
                Arguments = new List<ValueSource>(args),
                TargetPath = path

            });
            return this;
        }

        /// <summary>
        /// Creates a modifier inline using the ModifierBuilder API.
        /// </summary>
        public StatBlockBuilder AddModifier(Action<ModifierBuilder> buildAction)
        {
            var modBuilder = ModifierBuilder.Create();
            buildAction?.Invoke(modBuilder);
            return AddModifier(modBuilder.Build());
        }

        /// <summary>
        /// Adds a fully built modifier specification.
        /// </summary>
        public StatBlockBuilder AddModifier(AttributeModifierSpec spec)
        {
            _statBlock.Modifiers.Add(spec);
            return this;
        }


        /// <summary>
        /// Quick helper to add a basic static additive modifier (+10 Health).
        /// </summary>
        public StatBlockBuilder AddFlatModifier(SemanticKey targetAttr, float value)
        {
            var argList = new List<ValueSource> { Const(value) };
            return AddModifier(targetAttr, sk.Modifiers.Static, ModifierType.Additive, argList);
        }

        /// <summary>
        /// Quick helper to add a multiplier modifier (+50% Damage = 0.5f).
        /// </summary>
        public StatBlockBuilder AddMultiplierModifier(SemanticKey targetAttr, float percentage)
        {
            var argList = new List<ValueSource> { Const(percentage) };
            return AddModifier(targetAttr, sk.Modifiers.Static, ModifierType.Multiplicative, argList);
        }

        public StatBlockBuilder AddTag(SemanticKey tag)
        {
            _statBlock.Tags.Add(tag);
            return this;
        }

        public StatBlock Build() => _statBlock;
    }
}