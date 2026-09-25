using ReactiveSolutions.AttributeSystem.Core.Modifiers;
using SemanticKeys;
using System;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Core.Data
{
    /// <summary>What an effect action does to its attribute. A pool stays between 0 and its maximum whatever it is.</summary>
    public enum EffectActionType
    {
        /// <summary>Adds the amount: a heal on a pool (never above its maximum), or a lasting bonus such as +1 Level.</summary>
        Add,

        /// <summary>Removes the amount: damage on a pool (never below 0).</summary>
        Reduce,

        /// <summary>Sets the attribute to the amount.</summary>
        Set
    }

    /// <summary>
    /// One change an effect makes: to an attribute of its source or its target (e.g. Target / Health), what it does
    /// (Add, Reduce or Set), and the amount, computed by a logic when the effect is applied.
    /// </summary>
    [Serializable]
    public class EffectAction
    {
        [Tooltip("The attribute changed, on the Source or the Target: e.g. Health, with the path Target.")]
        public AttributeReference Target;

        [Tooltip("Add (e.g. a heal), Reduce (e.g. damage) or Set. A pool stays between 0 and its maximum.")]
        public EffectActionType Type = EffectActionType.Add;

        [Tooltip("Computes the amount when the effect is applied. Attributes are read through Source or Target.")]
        [SerializeReference]
        public ModifierLogic Logic = new ValueLogic();

        [Tooltip("The action only happens if this holds when its turn comes. Paths start with Source or Target.")]
        public StatBlockCondition Condition = new StatBlockCondition();

        [Tooltip("The probability that the action happens, from 0 to 1 (e.g. Source / CritChance). 1 is always.")]
        public ValueSource Chance = ValueSource.Const(1f);
    }

    /// <summary>A cost of an effect: an amount of a resource (e.g. 15 of the Source's Mana), paid before anything else.</summary>
    [Serializable]
    public class EffectCost
    {
        [Tooltip("The resource spent, usually the Source's: e.g. Mana, with the path Source.")]
        public AttributeReference Resource;

        [Tooltip("How much is spent.")]
        public ValueSource Amount = ValueSource.Const(0f);
    }

    /// <summary>
    /// A one-off change from a source entity to a target: a sword hit, a fireball, a healing potion, a level up.
    /// Unlike a StatBlock, which changes attributes while it is applied, an effect changes base values (and pools)
    /// once, with amounts computed when it is applied, from the attributes of both entities.
    /// <para>
    /// Everything an effect reads or changes is reached through its <see cref="EffectRoles"/>: "Source/AttackPower",
    /// "Target/Health". Applying it checks its <see cref="Condition"/>, pays its <see cref="Costs"/> (all of them, or
    /// nothing happens), then runs its <see cref="Actions"/> in order. The effect itself is never modified, so one
    /// effect can be applied any number of times, to any entities.
    /// </para>
    /// </summary>
    [Serializable]
    public class Effect : ISerializationCallbackReceiver
    {
        private static readonly System.Random SharedRandom = new System.Random();

        public string EffectName = "New Effect";

        [Tooltip("The effect only happens if this holds when it is applied (e.g. the Source isn't Silenced). Paths start with Source or Target.")]
        public StatBlockCondition Condition = new StatBlockCondition();

        [Tooltip("Paid first, usually by the Source (e.g. Mana). If they can't all be paid, nothing happens.")]
        public List<EffectCost> Costs = new List<EffectCost>();

        [Tooltip("What the effect does, in order: each action sees the changes made by the ones before it.")]
        public List<EffectAction> Actions = new List<EffectAction>();

        /// <summary>
        /// An attribute of the effect's source, e.g. <c>Effect.Source(Stats.AttackPower)</c>, or of an entity reached
        /// from it: <c>Effect.Source(Stats.Damage, Links.MainHand)</c> is the Damage of the source's MainHand.
        /// </summary>
        public static AttributeReference Source(SemanticKey attribute, params SemanticKey[] path) => Of(EffectRoles.Source, attribute, path);

        /// <summary>An attribute of the effect's target, e.g. <c>Effect.Target(Stats.Health)</c>, or of an entity reached from it.</summary>
        public static AttributeReference Target(SemanticKey attribute, params SemanticKey[] path) => Of(EffectRoles.Target, attribute, path);

        /// <summary>
        /// Applies the effect from <paramref name="source"/> to <paramref name="target"/> (the same entity for a potion
        /// drunk by its owner; the source may be null, e.g. for a trap). Returns what it did.
        /// </summary>
        /// <param name="random">Rolls the actions' chances; a shared one if null. Pass your own to make rolls repeatable.</param>
        public EffectResult Apply(Entity source, Entity target, System.Random random = null)
        {
            // The roles are the providers of a temporary entity, so paths, formulas and conditions work as anywhere else.
            var context = new Entity { Name = EffectName };
            try
            {
                if (source != null && !source.IsDisposed) context.RegisterExternalProvider(EffectRoles.Source, source);
                if (target != null && !target.IsDisposed) context.RegisterExternalProvider(EffectRoles.Target, target);

                if (!Holds(Condition, context)) return new EffectResult(this, source, target, EffectStatus.ConditionNotMet);

                // 1. Costs: all of them, or nothing happens. Costs of the same resource are checked together.
                var costs = new List<(Entity payer, EffectCost cost, float amount)>();
                foreach (var cost in Costs ?? new List<EffectCost>())
                {
                    if (cost == null || cost.Resource.Name == SemanticKey.None) continue;
                    if (!IsReached(cost.Resource, "cost")) continue;

                    float amount = Read(cost.Amount, context);
                    if (!(amount > 0f) || float.IsInfinity(amount)) continue;

                    var payer = Reach(context, cost.Resource.Path);
                    if (payer == null) return new EffectResult(this, source, target, EffectStatus.CannotPay, unpaidCost: cost.Resource);
                    costs.Add((payer, cost, amount));
                }

                foreach (var resource in costs.GroupBy(entry => (entry.payer, entry.cost.Resource.Name)))
                {
                    if (Held(resource.Key.payer, resource.Key.Name) < resource.Sum(entry => entry.amount))
                    {
                        return new EffectResult(this, source, target, EffectStatus.CannotPay, unpaidCost: resource.First().cost.Resource);
                    }
                }

                var changes = new List<EffectChange>();
                foreach (var (payer, cost, amount) in costs)
                {
                    changes.Add(Change(payer, cost.Resource.Name, EffectActionType.Reduce, amount, isCost: true));
                }

                // 2. Actions, in order.
                random = random ?? SharedRandom;
                foreach (var action in Actions ?? new List<EffectAction>())
                {
                    if (action == null) continue;
                    if (action.Target.Name == SemanticKey.None)
                    {
                        Debug.LogWarning($"[Effect] '{EffectName}': skipped an action with no target attribute.");
                        continue;
                    }
                    if (action.Logic == null)
                    {
                        Debug.LogWarning($"[Effect] '{EffectName}': skipped an action on '{action.Target.Name}' with no Logic.");
                        continue;
                    }
                    if (!IsReached(action.Target, "action")) continue;

                    if (!Holds(action.Condition, context)) continue;
                    if (!Rolls(action.Chance, context, random)) continue;

                    var entity = Reach(context, action.Target.Path);
                    if (entity == null) continue;

                    float amount = ReadNow(action.Logic.Observe(context));
                    changes.Add(Change(entity, action.Target.Name, action.Type, amount, isCost: false));
                }

                return new EffectResult(this, source, target, EffectStatus.Applied, changes);
            }
            finally
            {
                context.Dispose();
            }
        }

        public void OnBeforeSerialize() { }

        // An action or cost duplicated in the Inspector can share its logic (and the formulas in it) with the original.
        public void OnAfterDeserialize() => SerializedGraph.Unshare(this);

        // ---------------------------------------------------------------- Applying

        private static AttributeReference Of(SemanticKey role, SemanticKey attribute, SemanticKey[] path)
        {
            var steps = new List<SemanticKey> { role };
            if (path != null) steps.AddRange(path);
            return new AttributeReference(attribute, steps);
        }

        /// <summary>Whether the reference starts with Source or Target; logs a warning if not.</summary>
        private bool IsReached(AttributeReference reference, string what)
        {
            if (reference.Path != null && reference.Path.Count > 0 && EffectRoles.IsRole(reference.Path[0])) return true;

            Debug.LogWarning($"[Effect] '{EffectName}': skipped the {what} on '{reference.Name}': its path must start with Source or Target " +
                             "(the entity it is on).");
            return false;
        }

        /// <summary>The entity at the end of <paramref name="path"/>, or null if a step is missing.</summary>
        private static Entity Reach(Entity context, List<SemanticKey> path)
        {
            var entity = context;
            foreach (var step in path)
            {
                entity = entity.GetProvider(step);
                if (entity == null || entity.IsDisposed) return null;
            }
            return entity;
        }

        private static bool Holds(StatBlockCondition condition, Entity context)
        {
            if (condition == null || condition.Type == StatBlockCondition.Mode.Always) return true;

            bool holds = false;
            ConditionEvaluator.Observe(condition, context).Take(1).Subscribe(value => holds = value).Dispose();
            return holds;
        }

        private static bool Rolls(ValueSource chance, Entity context, System.Random random)
        {
            if (chance == null) return true;

            float probability = Read(chance, context);
            if (probability >= 1f) return true;
            if (!(probability > 0f)) return false;
            return random.NextDouble() < probability;
        }

        private static float Read(ValueSource value, Entity context) => value == null ? 0f : ReadNow(value.GetObservable(context));

        /// <summary>The current value of a stream that emits it on subscribe (as attributes and logic do).</summary>
        private static float ReadNow(IObservable<float> stream)
        {
            float value = 0f;
            stream.Take(1).Subscribe(v => value = v).Dispose();
            return value;
        }

        /// <summary>The amount held: a pool's amount, or the attribute's base value (0 if the entity doesn't have it).</summary>
        private static float Held(Entity entity, SemanticKey attribute)
        {
            var pool = entity.GetPool(attribute);
            if (pool != null) return pool.Current;
            return entity.GetAttribute(attribute)?.BaseValue ?? 0f;
        }

        private static EffectChange Change(Entity entity, SemanticKey attribute, EffectActionType type, float amount, bool isCost)
        {
            float before = Held(entity, attribute);

            // An amount that isn't a finite number (e.g. from a formula that divides by 0) does nothing, and Add and
            // Reduce only go their own way: damage that works out negative doesn't heal.
            bool usable = !float.IsNaN(amount) && !float.IsInfinity(amount) && (type == EffectActionType.Set || amount > 0f);
            if (usable)
            {
                var pool = entity.GetPool(attribute);
                switch (type)
                {
                    case EffectActionType.Add:
                        if (pool != null) pool.Restore(amount);
                        else entity.SetOrUpdateBaseValue(attribute, before + amount);
                        break;
                    case EffectActionType.Reduce:
                        if (pool != null) pool.Reduce(amount);
                        else entity.SetOrUpdateBaseValue(attribute, before - amount);
                        break;
                    default:
                        if (pool != null) pool.Set(amount);
                        else entity.SetOrUpdateBaseValue(attribute, amount);
                        break;
                }
            }

            return new EffectChange(entity, attribute, before, Held(entity, attribute), isCost);
        }
    }
}
