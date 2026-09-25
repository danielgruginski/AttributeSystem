using ReactiveSolutions.AttributeSystem.Core.Data;
using ReactiveSolutions.AttributeSystem.Core.Modifiers;
using SemanticKeys;

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

        public StatBlockBuilder SetCondition(StatBlockCondition.Mode mode, SemanticKey tag, bool invert = false)
        {
            _statBlock.ActivationCondition = new StatBlockCondition
            {
                Type = mode,
                Tag = tag,
                InvertTag = invert
            };
            return this;
        }

        /// <summary>
        /// Adds a modifier to <paramref name="targetAttr"/>: <paramref name="logic"/> computes the value, and
        /// <paramref name="type"/> says what it does to the attribute (e.g. new LinearLogic { ... }, ModifierType.Additive).
        /// </summary>
        public StatBlockBuilder AddModifier(SemanticKey targetAttr, ModifierLogic logic, ModifierType type = ModifierType.Additive, int priority = 0)
        {
            _statBlock.Modifiers.Add(new AttributeModifierSpec
            {
                TargetAttribute = targetAttr,
                Logic = logic,
                Type = type,
                Priority = priority
            });
            return this;
        }

        /// <summary>
        /// Quick helper to add a basic static additive modifier (+10 Health).
        /// </summary>
        public StatBlockBuilder AddFlatModifier(SemanticKey targetAttr, float value)
        {
            return AddModifier(targetAttr, new ValueLogic(value));
        }

        /// <summary>
        /// Quick helper to add a percentage multiplier modifier (+50% Damage = 0.5f, -25% = -0.25f).
        /// The attribute is multiplied by (1 + percentage).
        /// </summary>
        public StatBlockBuilder AddMultiplierModifier(SemanticKey targetAttr, float percentage)
        {
            return AddModifier(targetAttr, new ValueLogic(1f + percentage), ModifierType.Multiplicative);
        }

        public StatBlockBuilder AddTag(SemanticKey tag)
        {
            _statBlock.Tags.Add(tag);
            return this;
        }

        public StatBlock Build() => _statBlock;
    }
}