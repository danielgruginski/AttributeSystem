using ReactiveSolutions.AttributeSystem.Core.Data;
using ReactiveSolutions.AttributeSystem.Core.Modifiers;
using SemanticKeys;
using System;
using System.Collections.Generic;

namespace ReactiveSolutions.AttributeSystem.Core.Builders
{
    /// <summary>
    /// Builds an <see cref="Effect"/> in code. Attributes are reached through the effect's source or target:
    /// <code>
    /// var fireball = EffectBuilder.Create("Fireball")
    ///     .AddCost(Effect.Source(Stats.Mana), 15f)
    ///     .Reduce(Effect.Target(Stats.Health), new LinearLogic { Input = Effect.Source(Stats.SpellPower), Coefficient = 1.5f })
    ///     .Build();
    /// </code>
    /// The amount of Add, Reduce and Set is a number, an attribute (<c>Effect.Source(Stats.AttackPower)</c>) or a
    /// logic (a formula); AddAction takes the logic.
    /// </summary>
    public class EffectBuilder
    {
        private readonly Effect _effect;

        private EffectBuilder(string name)
        {
            _effect = new Effect { EffectName = name };
        }

        public static EffectBuilder Create(string name = "New Effect") => new EffectBuilder(name);

        /// <summary>
        /// The effect only happens if <paramref name="condition"/> holds when it is applied, e.g.
        /// <c>StatBlockCondition.LacksTag(Tags.Silenced, EffectRoles.Source)</c>. Null means always.
        /// </summary>
        public EffectBuilder SetCondition(StatBlockCondition condition)
        {
            _effect.Condition = condition ?? StatBlockCondition.Always();
            return this;
        }

        /// <summary>
        /// Adds a cost, paid before anything else: <c>AddCost(Effect.Source(Stats.Mana), 15f)</c>. If the costs can't
        /// all be paid, nothing happens.
        /// </summary>
        public EffectBuilder AddCost(AttributeReference resource, ValueSource amount)
        {
            CheckReached(resource, nameof(resource));
            _effect.Costs.Add(new EffectCost { Resource = Copy(resource), Amount = amount ?? ValueSource.Const(0f) });
            return this;
        }

        /// <summary>
        /// Adds an action: <paramref name="type"/> (Add, Reduce or Set) the amount that <paramref name="logic"/> computes
        /// to <paramref name="target"/>. With a <paramref name="condition"/>, it only happens if that holds; with a
        /// <paramref name="chance"/> (0 to 1, e.g. <c>Effect.Source(Stats.CritChance)</c>), only that often.
        /// </summary>
        public EffectBuilder AddAction(AttributeReference target, EffectActionType type, ModifierLogic logic,
            StatBlockCondition condition = null, ValueSource chance = null)
        {
            CheckReached(target, nameof(target));
            var action = new EffectAction
            {
                Target = Copy(target),
                Type = type,
                Logic = logic,
                Condition = condition ?? StatBlockCondition.Always()
            };
            if (chance != null) action.Chance = chance;
            _effect.Actions.Add(action);
            return this;
        }

        /// <summary>Adds <paramref name="amount"/> to <paramref name="target"/>: a heal on a pool, or a lasting bonus.</summary>
        public EffectBuilder Add(AttributeReference target, ValueSource amount, StatBlockCondition condition = null, ValueSource chance = null)
            => AddAction(target, EffectActionType.Add, ToLogic(amount), condition, chance);

        /// <summary>Removes <paramref name="amount"/> from <paramref name="target"/>: damage on a pool.</summary>
        public EffectBuilder Reduce(AttributeReference target, ValueSource amount, StatBlockCondition condition = null, ValueSource chance = null)
            => AddAction(target, EffectActionType.Reduce, ToLogic(amount), condition, chance);

        /// <summary>Sets <paramref name="target"/> to <paramref name="amount"/>.</summary>
        public EffectBuilder Set(AttributeReference target, ValueSource amount, StatBlockCondition condition = null, ValueSource chance = null)
            => AddAction(target, EffectActionType.Set, ToLogic(amount), condition, chance);

        public Effect Build() => _effect;

        /// <summary>A formula is used as it is; a number or an attribute becomes a Value logic.</summary>
        private static ModifierLogic ToLogic(ValueSource amount)
        {
            if (amount == null) return new ValueLogic();
            if (amount.Mode == ValueSource.SourceMode.Formula) return amount.Formula ?? new ValueLogic();
            return new ValueLogic(amount);
        }

        private static AttributeReference Copy(AttributeReference reference) =>
            new AttributeReference(reference.Name, reference.Path != null ? new List<SemanticKey>(reference.Path) : null);

        private static void CheckReached(AttributeReference reference, string parameter)
        {
            if (reference.Path != null && reference.Path.Count > 0 && EffectRoles.IsRole(reference.Path[0])) return;
            // Not set at all: allowed, as in the editor; applying the effect skips it with a warning.
            if (reference.Name == SemanticKey.None && (reference.Path == null || reference.Path.Count == 0)) return;
            throw new ArgumentException(
                $"An effect reaches '{reference.Name}' through its source or its target: use Effect.Source(...) or Effect.Target(...).",
                parameter);
        }
    }
}
