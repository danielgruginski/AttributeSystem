using ReactiveSolutions.AttributeSystem.Core.Data;
using SemanticKeys;
using System.Collections.Generic;

namespace ReactiveSolutions.AttributeSystem.Core
{
    /// <summary>Whether an effect happened, or why not.</summary>
    public enum EffectStatus
    {
        /// <summary>The effect paid its costs and ran its actions.</summary>
        Applied,

        /// <summary>The effect's condition didn't hold: nothing changed.</summary>
        ConditionNotMet,

        /// <summary>A cost couldn't be paid (e.g. not enough Mana): nothing changed.</summary>
        CannotPay
    }

    /// <summary>A change an effect made to an attribute: a cost it paid, or one of its actions.</summary>
    public readonly struct EffectChange
    {
        public EffectChange(Entity entity, SemanticKey attribute, float before, float after, bool isCost)
        {
            Entity = entity;
            Attribute = attribute;
            Before = before;
            After = after;
            IsCost = isCost;
        }

        /// <summary>The entity changed.</summary>
        public Entity Entity { get; }

        /// <summary>The attribute changed (e.g. Health).</summary>
        public SemanticKey Attribute { get; }

        /// <summary>The attribute's base value (a pool's amount) before the change.</summary>
        public float Before { get; }

        /// <summary>The attribute's base value (a pool's amount) after the change.</summary>
        public float After { get; }

        /// <summary>After - Before: negative for damage and costs. A pool's limits count: 30 damage to 10 Health is -10.</summary>
        public float Amount => After - Before;

        /// <summary>Whether this is a cost the effect paid.</summary>
        public bool IsCost { get; }

        public override string ToString() =>
            $"{Entity?.Name ?? "?"}.{Attribute}: {Before:0.###} -> {After:0.###}{(IsCost ? " (cost)" : "")}";
    }

    /// <summary>What applying an effect did: whether it happened, and every change it made, in order.</summary>
    public sealed class EffectResult
    {
        private static readonly EffectChange[] NoChanges = new EffectChange[0];

        private static readonly ActiveStatusEffect[] NoStatuses = new ActiveStatusEffect[0];

        internal EffectResult(Effect effect, Entity source, Entity target, EffectStatus status,
            IReadOnlyList<EffectChange> changes = null, AttributeReference unpaidCost = default,
            IReadOnlyList<ActiveStatusEffect> statuses = null, int statusesRemoved = 0)
        {
            Effect = effect;
            Source = source;
            Target = target;
            Status = status;
            Changes = changes ?? NoChanges;
            UnpaidCost = unpaidCost;
            Statuses = statuses ?? NoStatuses;
            StatusesRemoved = statusesRemoved;
        }

        /// <summary>The effect applied.</summary>
        public Effect Effect { get; }

        /// <summary>The entity the effect came from (may be null).</summary>
        public Entity Source { get; }

        /// <summary>The entity the effect was applied to.</summary>
        public Entity Target { get; }

        /// <summary>Whether the effect happened, or why not.</summary>
        public EffectStatus Status { get; }

        /// <summary>Whether the effect happened (its condition held and its costs were paid).</summary>
        public bool Applied => Status == EffectStatus.Applied;

        /// <summary>
        /// The cost that couldn't be paid when <see cref="Status"/> is CannotPay (e.g. Source/Mana, for a "Not enough
        /// Mana" message).
        /// </summary>
        public AttributeReference UnpaidCost { get; }

        /// <summary>The changes, in order: the costs, then the actions that happened.</summary>
        public IReadOnlyList<EffectChange> Changes { get; }

        /// <summary>The status effects it applied, as their entities now have them (new, refreshed or stacked).</summary>
        public IReadOnlyList<ActiveStatusEffect> Statuses { get; }

        /// <summary>How many status effects it removed from the target (see Effect.RemoveStatusCategories).</summary>
        public int StatusesRemoved { get; }

        /// <summary>
        /// How much <paramref name="attribute"/> of <paramref name="entity"/> changed in total: <c>-result.ChangeOf(target,
        /// Stats.Health)</c> is the damage dealt.
        /// </summary>
        public float ChangeOf(Entity entity, SemanticKey attribute)
        {
            float total = 0f;
            foreach (var change in Changes)
            {
                if (change.Entity == entity && change.Attribute == attribute) total += change.Amount;
            }
            return total;
        }
    }
}
