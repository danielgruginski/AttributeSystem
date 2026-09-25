using ReactiveSolutions.AttributeSystem.Core.Data;
using System;
using System.Collections.Generic;
using UniRx;

namespace ReactiveSolutions.AttributeSystem.Core
{
    /// <summary>Why a status effect ended.</summary>
    public enum StatusEndReason
    {
        /// <summary>Its duration ran out, and its on-expire effects were applied.</summary>
        Expired,

        /// <summary>It was removed: by Remove, by a cleanse (RemoveStatusEffects), or because its entity was disposed.</summary>
        Removed,

        /// <summary>Its condition stopped holding.</summary>
        ConditionFailed
    }

    /// <summary>
    /// A status effect on an entity (Entity.StatusEffects lists them): its stacks, its time left and its source.
    /// Remove (or Dispose) removes it early, without its on-expire effects.
    /// </summary>
    public sealed class ActiveStatusEffect : IDisposable
    {
        // How close to the next tick counts as reaching it, as a fraction of the interval (time adds up in steps).
        private const double TickTolerance = 1e-6;

        private readonly StatusEffectHost _host;
        private readonly Entity _context;
        private readonly StatBlock _statBlock;
        private readonly List<ActiveStatBlock> _stackBlocks = new List<ActiveStatBlock>();
        private readonly ReactiveProperty<int> _stacks = new ReactiveProperty<int>(0);
        private readonly ReactiveProperty<float> _timeLeft = new ReactiveProperty<float>(0f);
        private readonly AsyncSubject<StatusEndReason> _ended = new AsyncSubject<StatusEndReason>();
        private IDisposable _conditionWatch;
        private double _exactTimeLeft;
        private double _untilTick;

        /// <summary>The status effect's data.</summary>
        public StatusEffect Status { get; }

        /// <summary>Its name, for logs and UI.</summary>
        public string Name => Status.StatusName;

        /// <summary>The entity that has it.</summary>
        public Entity Target { get; }

        /// <summary>The entity that applied it last (null if none): its ticks and other effects come from it.</summary>
        public Entity Source { get; private set; }

        /// <summary>The number of stacks: 1, or more for a status that stacks.</summary>
        public int Stacks => _stacks.Value;

        public IReadOnlyReactiveProperty<int> ObservableStacks => _stacks;

        /// <summary>The duration it had when last applied (infinity if it lasts until removed), e.g. for a timer bar.</summary>
        public float Duration { get; private set; }

        /// <summary>The time left (infinity if it lasts until removed).</summary>
        public float TimeLeft => _timeLeft.Value;

        public IReadOnlyReactiveProperty<float> ObservableTimeLeft => _timeLeft;

        /// <summary>Whether it lasts until removed.</summary>
        public bool LastsUntilRemoved => Status.LastsUntilRemoved;

        /// <summary>False once it has ended.</summary>
        public bool IsActive { get; private set; } = true;

        /// <summary>Emits why it ended, once, when it ends (at once for a subscriber that comes later).</summary>
        public IObservable<StatusEndReason> Ended => _ended;

        internal ActiveStatusEffect(StatusEffectHost host, StatusEffect status, Entity target, Entity source, Entity context, float duration)
        {
            _host = host;
            Status = status;
            Target = target;
            Source = source;
            _context = context;
            _statBlock = status.ResolveStatBlock();
            SetTime(duration);
            _untilTick = status.TickInterval;
        }

        /// <summary>Removes it now. Its on-expire effects aren't applied.</summary>
        public void Remove() => End(StatusEndReason.Removed, null);

        /// <summary>Same as <see cref="Remove"/>.</summary>
        public void Dispose() => Remove();

        public override string ToString() =>
            $"{Name}{(Stacks > 1 ? $" x{Stacks}" : "")}{(LastsUntilRemoved ? "" : $" ({TimeLeft:0.#} left)")}";

        // ---------------------------------------------------------------- Applying

        /// <summary>The first application: its first stack, its condition watch, its on-apply effects.</summary>
        internal void Start(System.Random random)
        {
            AddStack();

            // It ends as soon as its condition stops holding (e.g. when its entity's Health reaches 0).
            var watch = ConditionEvaluator.Observe(Status.Condition, _context)
                .Subscribe(holds =>
                {
                    if (!holds) End(StatusEndReason.ConditionFailed, null);
                });

            // Its own StatBlock can already have made the condition fail, ending it before the watch was stored.
            if (IsActive) _conditionWatch = watch;
            else watch.Dispose();

            ApplyOnApplyEffects(random);
        }

        /// <summary>Applying it again while it lasts: Refresh, Extend or Stack. Nothing happens if its condition doesn't hold.</summary>
        internal void Reapply(Entity source, StatusStacking stacking, System.Random random)
        {
            if (!IsActive) return;

            // Checked with the new source, so an application that fails doesn't end the status the entity has.
            float duration;
            var check = RoleContext.Create(Name, source, Target);
            try
            {
                if (!RoleContext.Holds(Status.Condition, check)) return;
                duration = StatusEffectHost.DurationOf(Status, check);
            }
            finally
            {
                check.Dispose();
            }

            SetSource(source);
            switch (stacking)
            {
                case StatusStacking.Extend:
                    SetTime(_exactTimeLeft + duration);
                    break;
                case StatusStacking.Stack:
                    if (Status.MaxStacks <= 0 || Stacks < Status.MaxStacks) AddStack();
                    SetTime(duration);
                    break;
                default:
                    SetTime(duration);
                    break;
            }

            ApplyOnApplyEffects(random);
        }

        private void ApplyOnApplyEffects(System.Random random)
        {
            // Meanwhile, applying this status to the entity again does nothing (see StatusEffectHost.Apply).
            _host.BeginOnApply(Status);
            try
            {
                ApplyEffects(Status.OnApply, random);
            }
            finally
            {
                _host.EndOnApply(Status);
            }
        }

        private void AddStack()
        {
            if (_statBlock != null) _stackBlocks.Add(_statBlock.ApplyToEntity(Target));
            _stacks.Value++;
        }

        private void SetSource(Entity source)
        {
            Source = source;
            if (source != null && !source.IsDisposed) _context.RegisterExternalProvider(EffectRoles.Source, source);
            else _context.UnregisterExternalProvider(EffectRoles.Source);
        }

        private void SetTime(double duration)
        {
            if (LastsUntilRemoved) duration = double.PositiveInfinity;
            else if (double.IsNaN(duration)) duration = 0;

            Duration = (float)duration;
            _exactTimeLeft = duration;
            _timeLeft.Value = (float)duration;
        }

        // ---------------------------------------------------------------- Time

        /// <summary>
        /// Advances its time: a tick each TickInterval (as many as fit, and none past its end), then its end if its
        /// duration runs out.
        /// </summary>
        internal void Tick(double deltaTime, System.Random random)
        {
            if (!IsActive || !(deltaTime > 0)) return;

            double elapsed = LastsUntilRemoved ? deltaTime : Math.Min(deltaTime, _exactTimeLeft);

            float interval = Status.TickInterval;
            if (interval > 0f)
            {
                _untilTick -= elapsed;
                while (IsActive && _untilTick <= interval * TickTolerance)
                {
                    _untilTick += interval;
                    ApplyTick(random);
                }
            }

            if (!IsActive || LastsUntilRemoved) return;

            _exactTimeLeft -= elapsed;
            _timeLeft.Value = (float)Math.Max(0.0, _exactTimeLeft);
            if (_exactTimeLeft <= 0) End(StatusEndReason.Expired, random);
        }

        private void ApplyTick(System.Random random)
        {
            var effect = Status.TickEffect?.Resolve();
            if (effect == null) return;

            // Once per stack, for as long as it lasts (a tick can end it, e.g. by emptying its entity's Health).
            int stacks = Stacks;
            for (int i = 0; i < stacks && IsActive; i++)
            {
                effect.Apply(Source, Target, random);
            }
        }

        private void ApplyEffects(List<EffectEntry> entries, System.Random random)
        {
            if (entries == null) return;
            foreach (var entry in entries)
            {
                entry?.Resolve()?.Apply(Source, Target, random);
            }
        }

        // ---------------------------------------------------------------- Ending

        internal void End(StatusEndReason reason, System.Random random)
        {
            if (!IsActive) return;
            IsActive = false;

            _conditionWatch?.Dispose();
            foreach (var block in _stackBlocks) block.Dispose();
            _stackBlocks.Clear();
            _host.Forget(this);

            if (reason == StatusEndReason.Expired) ApplyEffects(Status.OnExpire, random);

            _context.Dispose();
            _ended.OnNext(reason);
            _ended.OnCompleted();
        }
    }
}
