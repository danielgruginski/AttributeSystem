using ReactiveSolutions.AttributeSystem.Core.Data;
using ReactiveSolutions.AttributeSystem.Core.Modifiers;
using SemanticKeys;
using System.Collections.Generic;

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
        /// Sets when the block's content applies, e.g. <c>StatBlockCondition.HasTag(Tags.Equipped)</c> or
        /// <c>StatBlockCondition.All(...)</c>. Null means always.
        /// </summary>
        public StatBlockBuilder SetCondition(StatBlockCondition condition)
        {
            _statBlock.ActivationCondition = condition ?? StatBlockCondition.Always();
            return this;
        }

        /// <summary>Sets the base value of <paramref name="attribute"/> when the block is applied (whatever its condition).</summary>
        public StatBlockBuilder AddBaseValue(SemanticKey attribute, float value)
        {
            _statBlock.BaseValues.Add(new StatBlock.BaseValueEntry { Name = attribute, Value = value });
            return this;
        }

        /// <summary>
        /// Adds a modifier to <paramref name="targetAttr"/>: <paramref name="logic"/> computes the value, and
        /// <paramref name="type"/> says what it does to the attribute (e.g. new LinearLogic { ... }, ModifierType.Additive).
        /// <paramref name="sourceId"/> names the modifier in the Attribute Debugger.
        /// </summary>
        public StatBlockBuilder AddModifier(SemanticKey targetAttr, ModifierLogic logic, ModifierType type = ModifierType.Additive,
            int priority = 0, string sourceId = null)
            => AddModifier(new AttributeReference(targetAttr), logic, type, priority, sourceId);

        /// <summary>
        /// Adds a modifier to an attribute that can be on another entity: with <c>AttributeReference.Of(Stats.Strength, Links.Owner)</c>
        /// it modifies the Strength of the Owner of the entity the block is applied to. The logic's inputs are still
        /// read from the entity the block is applied to.
        /// </summary>
        public StatBlockBuilder AddModifier(AttributeReference target, ModifierLogic logic, ModifierType type = ModifierType.Additive,
            int priority = 0, string sourceId = null)
        {
            _statBlock.Modifiers.Add(new AttributeModifierSpec
            {
                TargetAttribute = target.Name,
                TargetPath = target.Path != null ? new List<SemanticKey>(target.Path) : new List<SemanticKey>(),
                SourceId = sourceId,
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

        /// <summary>Adds <paramref name="tag"/> to the entity at <paramref name="targetPath"/> (e.g. Links.Owner) while the block is active.</summary>
        public StatBlockBuilder AddRemoteTag(SemanticKey tag, params SemanticKey[] targetPath)
        {
            _statBlock.RemoteTags.Add(new TagModifierSpec { Tag = tag, TargetPath = new List<SemanticKey>(targetPath ?? new SemanticKey[0]) });
            return this;
        }

        /// <summary>
        /// While the block is active, <paramref name="alias"/> points to <paramref name="target"/>: a local attribute, or
        /// one at the end of <paramref name="providerPath"/> (e.g. Links.RightHand).
        /// </summary>
        public StatBlockBuilder AddPointer(SemanticKey alias, SemanticKey target, params SemanticKey[] providerPath)
        {
            _statBlock.Pointers.Add(new StatBlock.PointerSpec
            {
                Alias = alias,
                Target = new AttributeReference(target, new List<SemanticKey>(providerPath ?? new SemanticKey[0]))
            });
            return this;
        }

        public StatBlock Build() => _statBlock;
    }
}