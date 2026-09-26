using ReactiveSolutions.AttributeSystem.Core.Data;
using SemanticKeys;
using System;

namespace ReactiveSolutions.AttributeSystem.Core.Builders
{
    /// <summary>
    /// Builds a <see cref="StatusEffect"/> in code:
    /// <code>
    /// var poison = StatusEffectBuilder.Create("Poison")
    ///     .AddCategory(Tags.Debuff)
    ///     .SetDuration(5f)
    ///     .SetStacking(StatusStacking.Stack, maxStacks: 3)
    ///     .SetStatBlock("Debuffs/Poison")
    ///     .SetTick(1f, "Debuffs/PoisonTick")
    ///     .Build();
    /// </code>
    /// A status built without SetDuration lasts until it is removed, as in files.
    /// </summary>
    public class StatusEffectBuilder
    {
        private readonly StatusEffect _status;

        private StatusEffectBuilder(string name)
        {
            _status = new StatusEffect { StatusName = name, LastsUntilRemoved = true };
        }

        public static StatusEffectBuilder Create(string name = "New Status") => new StatusEffectBuilder(name);

        /// <summary>A label to find and remove it by, e.g. Debuff (see Entity.RemoveStatusEffects).</summary>
        public StatusEffectBuilder AddCategory(SemanticKey category)
        {
            _status.Categories.Add(category);
            return this;
        }

        /// <summary>
        /// It is only applied if <paramref name="condition"/> holds, and ends as soon as it stops holding, e.g.
        /// <c>StatBlockCondition.Compare(Effect.Target(Stats.Health), StatBlockCondition.Comparison.Greater, 0f)</c>.
        /// Null means always.
        /// </summary>
        public StatusEffectBuilder SetCondition(StatBlockCondition condition)
        {
            _status.Condition = condition ?? StatBlockCondition.Always();
            return this;
        }

        /// <summary>
        /// How long it lasts, computed each time it is applied: a number, an attribute (<c>Effect.Source(Stats.Level)</c>)
        /// or a formula.
        /// </summary>
        public StatusEffectBuilder SetDuration(ValueSource duration)
        {
            _status.LastsUntilRemoved = false;
            _status.Duration = duration ?? ValueSource.Const(0f);
            return this;
        }

        /// <summary>It lasts until it is removed (the default, until SetDuration is called).</summary>
        public StatusEffectBuilder LastsUntilRemoved()
        {
            _status.LastsUntilRemoved = true;
            return this;
        }

        /// <summary>What applying it again does while the entity has it; <paramref name="maxStacks"/> 0 is no limit.</summary>
        public StatusEffectBuilder SetStacking(StatusStacking stacking, int maxStacks = 0)
        {
            if (maxStacks < 0) throw new ArgumentOutOfRangeException(nameof(maxStacks), "The most stacks can't be negative (0 is no limit).");
            _status.Stacking = stacking;
            _status.MaxStacks = maxStacks;
            return this;
        }

        /// <summary>The StatBlock applied to the entity while it lasts (once per stack): a StatBlock file, by ID.</summary>
        public StatusEffectBuilder SetStatBlock(string statBlockId)
        {
            _status.StatBlockId = statBlockId;
            return this;
        }

        /// <summary>The StatBlock applied to the entity while it lasts (once per stack). Its paths are the entity's own.</summary>
        public StatusEffectBuilder SetStatBlock(StatBlock statBlock)
        {
            _status.StatBlockId = null;
            _status.StatBlock = statBlock ?? new StatBlock { BlockName = "" };
            return this;
        }

        /// <summary>Every <paramref name="interval"/>, applies an effect file (by ID) from the source to the entity, once per stack.</summary>
        public StatusEffectBuilder SetTick(float interval, string effectId) => SetTick(interval, new EffectEntry { EffectId = effectId });

        /// <summary>Every <paramref name="interval"/>, applies <paramref name="effect"/> from the source to the entity, once per stack.</summary>
        public StatusEffectBuilder SetTick(float interval, Effect effect) => SetTick(interval, new EffectEntry { Effect = effect });

        /// <summary>Applies an effect file (by ID) from the source to the entity each time the status is applied.</summary>
        public StatusEffectBuilder AddOnApply(string effectId) => Add(_status.OnApply, new EffectEntry { EffectId = effectId });

        /// <summary>Applies <paramref name="effect"/> from the source to the entity each time the status is applied.</summary>
        public StatusEffectBuilder AddOnApply(Effect effect) => Add(_status.OnApply, new EffectEntry { Effect = effect });

        /// <summary>Applies an effect file (by ID) from the source to the entity when the status runs out.</summary>
        public StatusEffectBuilder AddOnExpire(string effectId) => Add(_status.OnExpire, new EffectEntry { EffectId = effectId });

        /// <summary>Applies <paramref name="effect"/> from the source to the entity when the status runs out.</summary>
        public StatusEffectBuilder AddOnExpire(Effect effect) => Add(_status.OnExpire, new EffectEntry { Effect = effect });

        public StatusEffect Build() => _status;

        internal StatusEffectBuilder AddOnApply(EffectEntry entry) => Add(_status.OnApply, entry);

        internal StatusEffectBuilder AddOnExpire(EffectEntry entry) => Add(_status.OnExpire, entry);

        internal StatusEffectBuilder SetTick(float interval, EffectEntry effect)
        {
            if (!(interval > 0f)) throw new ArgumentOutOfRangeException(nameof(interval), "The time between ticks must be more than 0.");
            _status.TickInterval = interval;
            _status.TickEffect = effect;
            return this;
        }

        private StatusEffectBuilder Add(System.Collections.Generic.List<EffectEntry> list, EffectEntry entry)
        {
            list.Add(entry);
            return this;
        }
    }
}
