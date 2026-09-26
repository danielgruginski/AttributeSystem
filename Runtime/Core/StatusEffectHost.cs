using ReactiveSolutions.AttributeSystem.Core.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using UniRx;

namespace ReactiveSolutions.AttributeSystem.Core
{
    /// <summary>
    /// An entity's status effects: applying them (with their stacking), their time, and removing them. Entity exposes it
    /// as StatusEffects, TickStatusEffects and RemoveStatusEffect(s).
    /// </summary>
    internal sealed class StatusEffectHost : IDisposable
    {
        private readonly Entity _entity;
        private readonly ReactiveCollection<ActiveStatusEffect> _active = new ReactiveCollection<ActiveStatusEffect>();

        // The statuses whose on-apply effects are running.
        private readonly List<StatusEffect> _onApply = new List<StatusEffect>();

        public StatusEffectHost(Entity entity)
        {
            _entity = entity;
        }

        public IReadOnlyReactiveCollection<ActiveStatusEffect> Active => _active;

        public ActiveStatusEffect Apply(StatusEffect status, Entity source, System.Random random)
        {
            if (status == null || _entity.IsDisposed) return null;

            var existing = _active.Where(active => active.Status.IsSameAs(status)).ToList();

            // Applied again by its own on-apply effects, even through other statuses: nothing happens, or it would never end.
            if (_onApply.Any(applying => applying.IsSameAs(status))) return existing.FirstOrDefault();

            if (existing.Count > 0)
            {
                switch (status.Stacking)
                {
                    case StatusStacking.Ignore:
                        return existing[0];

                    case StatusStacking.Independent:
                        if (status.MaxStacks > 0 && existing.Count >= status.MaxStacks)
                        {
                            // As many instances as it can have: the one that would end first starts over.
                            var soonest = existing.OrderBy(active => active.TimeLeft).First();
                            soonest.Reapply(source, StatusStacking.Refresh, random);
                            return soonest;
                        }
                        break; // A new instance, below.

                    default:
                        existing[0].Reapply(source, status.Stacking, random);
                        return existing[0];
                }
            }

            var context = RoleContext.Create(status.StatusName, source, _entity);
            if (!RoleContext.Holds(status.Condition, context))
            {
                context.Dispose();
                return null;
            }

            var instance = new ActiveStatusEffect(this, status, _entity, source, context, DurationOf(status, context));
            _active.Add(instance);
            instance.Start(random);
            return instance;
        }

        /// <summary>The duration of an application, read through its source and target (infinity if it lasts until removed).</summary>
        internal static float DurationOf(StatusEffect status, Entity context) =>
            status.LastsUntilRemoved ? float.PositiveInfinity : RoleContext.Read(status.Duration, context);

        public void Tick(float deltaTime, System.Random random)
        {
            if (!(deltaTime > 0f) || _active.Count == 0) return;

            // A copy: ticks can end statuses, and their effects can apply new ones.
            foreach (var status in _active.ToArray())
            {
                status.Tick(deltaTime, random);
            }
        }

        /// <summary>Removes the statuses that <paramref name="match"/> accepts, and returns how many.</summary>
        public int Remove(Func<ActiveStatusEffect, bool> match)
        {
            var matches = _active.Where(match).ToList();
            foreach (var status in matches) status.Remove();
            return matches.Count;
        }

        internal void Forget(ActiveStatusEffect status) => _active.Remove(status);

        internal void BeginOnApply(StatusEffect status) => _onApply.Add(status);

        internal void EndOnApply(StatusEffect status) => _onApply.Remove(status);

        public void Dispose()
        {
            Remove(_ => true);
            _active.Dispose();
        }
    }
}
