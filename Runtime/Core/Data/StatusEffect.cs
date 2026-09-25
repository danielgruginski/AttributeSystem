using SemanticKeys;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Core.Data
{
    /// <summary>What applying a status effect again does while the entity still has it.</summary>
    public enum StatusStacking
    {
        /// <summary>Restarts its duration (the default).</summary>
        Refresh,

        /// <summary>Adds its duration to the time left.</summary>
        Extend,

        /// <summary>
        /// Adds a stack, up to MaxStacks, and restarts its duration. Each stack applies the StatBlock and the ticks once
        /// more: 3 stacks of a poison with -10% MoveSpeed and 2 damage per tick are 0.9 x 0.9 x 0.9 MoveSpeed and 6 damage.
        /// </summary>
        Stack,

        /// <summary>Applies a separate instance, with its own duration, up to MaxStacks instances.</summary>
        Independent,

        /// <summary>Does nothing: it can't be applied again until it ends.</summary>
        Ignore
    }

    /// <summary>An effect that a status effect applies: an effect file, by ID, or an effect written in the status.</summary>
    [Serializable]
    public class EffectEntry
    {
        [Tooltip("An effect JSON file (under Resources/Data/Effects). Leave empty to use the effect below.")]
        [EffectID]
        public string EffectId;

        [Tooltip("The effect, when no Effect Id is set.")]
        public Effect Effect = new Effect { EffectName = "" };

        [NonSerialized] private Effect _loaded;
        [NonSerialized] private string _loadedId;

        /// <summary>The effect to apply: the file's (loaded once, null if it can't be), or the one written here.</summary>
        internal Effect Resolve()
        {
            if (string.IsNullOrEmpty(EffectId)) return Effect;

            if (_loadedId != EffectId)
            {
                // The loader logs an error if the file can't be loaded.
                _loaded = EffectJsonLoader.TryLoad(EffectId, out var effect) ? effect : null;
                _loadedId = EffectId;
            }
            return _loaded;
        }
    }

    /// <summary>
    /// A condition that lasts on an entity: poisoned, blessed, stunned, regenerating. While the entity has it, its
    /// StatBlock is applied to the entity (once per stack), and every TickInterval its tick effect is applied from the
    /// status's source to the entity. It ends when its duration runs out, when its condition stops holding, or when it
    /// is removed.
    /// <para>
    /// Apply it with <see cref="Apply"/>, and advance time with Entity.TickStatusEffects (an EntityController does that
    /// every frame). The status itself is never modified, so one status can be applied to any number of entities.
    /// Paths in its Condition, Duration and effects start with Source or Target, as in an effect; the StatBlock's paths
    /// are the entity's own, as when a StatBlock is applied to it.
    /// </para>
    /// </summary>
    [Serializable]
    public class StatusEffect : ISerializationCallbackReceiver
    {
        public string StatusName = "New Status";

        /// <summary>The ID of the JSON file this status was loaded from (set by StatusEffectJsonLoader), or null.</summary>
        [NonSerialized]
        internal string JsonId;

        [Tooltip("Labels to find and remove it by: RemoveStatusEffects(Tags.Debuff) removes every Debuff. Unlike the StatBlock's tags, they aren't added to the entity.")]
        public List<SemanticKey> Categories = new List<SemanticKey>();

        [Tooltip("It is only applied if this holds, and it ends as soon as this stops holding (e.g. Target / Health > 0). Paths start with Source or Target.")]
        public StatBlockCondition Condition = new StatBlockCondition();

        [Tooltip("Lasts until it is removed: Duration is ignored.")]
        public bool LastsUntilRemoved;

        [Tooltip("How long it lasts, in the time units given to TickStatusEffects (seconds, turns...), computed each time it is applied. Paths start with Source or Target.")]
        public ValueSource Duration = ValueSource.Const(10f);

        [Tooltip("What applying it again does while the entity still has it.")]
        public StatusStacking Stacking = StatusStacking.Refresh;

        [Tooltip("The most stacks (Stack) or instances (Independent) an entity can have. 0 is no limit.")]
        public int MaxStacks;

        [Tooltip("The StatBlock applied to the entity while the status lasts (once per stack), by ID. Leave empty to use the StatBlock below.")]
        [StatBlockID]
        public string StatBlockId;

        [Tooltip("The StatBlock applied to the entity while the status lasts (once per stack), when no StatBlock Id is set. Its paths are the entity's own.")]
        public StatBlock StatBlock = new StatBlock { BlockName = "" };

        [Tooltip("The time between ticks, in the units of Duration. 0 is no ticks.")]
        public float TickInterval;

        [Tooltip("Applied from the status's source to the entity at each tick, once per stack (e.g. 3 poison damage).")]
        public EffectEntry TickEffect = new EffectEntry();

        [Tooltip("Applied from the source to the entity each time the status is applied, including to refresh or stack it.")]
        public List<EffectEntry> OnApply = new List<EffectEntry>();

        [Tooltip("Applied from the source to the entity when the status runs out: not when it is removed, or when its condition stops holding.")]
        public List<EffectEntry> OnExpire = new List<EffectEntry>();

        [NonSerialized] private StatBlock _loadedStatBlock;
        [NonSerialized] private string _loadedStatBlockId;

        /// <summary>
        /// Applies the status from <paramref name="source"/> (may be null) to <paramref name="target"/>. If the target
        /// already has it, <see cref="Stacking"/> decides what happens. Returns the status as the target now has it (a
        /// new instance, or the one it already had), or null if it wasn't applied (its condition doesn't hold).
        /// </summary>
        /// <param name="random">Rolls the chances of its on-apply effects; a shared one if null.</param>
        public ActiveStatusEffect Apply(Entity source, Entity target, System.Random random = null)
        {
            if (target == null || target.IsDisposed) return null;
            return target.StatusEffectHost.Apply(this, source, random);
        }

        /// <summary>Whether <paramref name="other"/> is this status: the same object, or loaded from the same file.</summary>
        public bool IsSameAs(StatusEffect other) =>
            other != null && (ReferenceEquals(this, other) || (JsonId != null && JsonId == other.JsonId));

        /// <summary>The StatBlock to apply: the file's (loaded once, null if it can't be), or the one written here.</summary>
        internal StatBlock ResolveStatBlock()
        {
            if (string.IsNullOrEmpty(StatBlockId)) return StatBlock;

            if (_loadedStatBlockId != StatBlockId)
            {
                // The loader logs an error if the file can't be loaded.
                _loadedStatBlock = StatBlockJsonLoader.TryLoad(StatBlockId, out var block) ? block : null;
                _loadedStatBlockId = StatBlockId;
            }
            return _loadedStatBlock;
        }

        public void OnBeforeSerialize() { }

        // An entry duplicated in the Inspector can share logic objects with the original; give it its own.
        public void OnAfterDeserialize() => SerializedGraph.Unshare(this);
    }
}
