using ReactiveSolutions.AttributeSystem.Core.Data;
using SemanticKeys;
using System;
using System.Collections.Generic;
using UnityEngine;
using sk; // Included to match your test files (e.g., Modifiers.Static)

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

        public StatBlockBuilder AddModifier(SemanticKey targetAttr, SemanticKey logicType, ModifierType type, params ValueSource[] args)
        {
            _statBlock.Modifiers.Add(new AttributeModifierSpec
            {
                TargetAttribute = targetAttr,
                LogicType = logicType,
                Type = type,
                Arguments = new List<ValueSource>(args)
            });
            return this;
        }

        /// <summary>
        /// Quick helper to add a basic static additive modifier (+10 Health).
        /// </summary>
        public StatBlockBuilder AddFlatModifier(SemanticKey targetAttr, float value)
        {
            return AddModifier(targetAttr, sk.Modifiers.Static, ModifierType.Additive, Const(value));
        }

        /// <summary>
        /// Quick helper to add a multiplier modifier (+50% Damage = 0.5f).
        /// </summary>
        public StatBlockBuilder AddMultiplierModifier(SemanticKey targetAttr, float percentage)
        {
            return AddModifier(targetAttr, sk.Modifiers.Static, ModifierType.Multiplicative, Const(percentage));
        }

        public StatBlockBuilder AddTag(SemanticKey tag)
        {
            _statBlock.Tags.Add(tag);
            return this;
        }

        public StatBlock Build() => _statBlock;
    }
}