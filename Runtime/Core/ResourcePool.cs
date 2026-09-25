using SemanticKeys;
using System;
using UniRx;

namespace ReactiveSolutions.AttributeSystem.Core
{
    /// <summary>What a pool's amount does when the pool's maximum changes.</summary>
    public enum PoolMaxChange
    {
        /// <summary>Keeps the same percentage: 70/100 becomes 35/50, and 70/100 again when the maximum comes back.</summary>
        KeepPercent,

        /// <summary>Moves by as much as the maximum: 70/100 becomes 90/120, or 20/50.</summary>
        AddDifference,

        /// <summary>Keeps the amount, at most the new maximum: 70/100 becomes 50/50, and stays 50 when the maximum comes back.</summary>
        KeepAmount
    }

    /// <summary>
    /// A resource that is spent and restored, such as Health, Mana or Stamina. The amount is the base value of the
    /// pool's attribute, and it stays between 0 and Max whoever sets it (including SetOrUpdateBaseValue), so it can't
    /// be over-healed.
    /// A new pool is full, and follows its maximum while that settles (e.g. while a profile's formulas apply) until
    /// the pool is first used; after that, <see cref="OnMaxChange"/> decides what a change of the maximum does.
    /// Create pools with Entity.AddPool, or in a profile (Pools).
    /// </summary>
    public sealed class ResourcePool : IDisposable
    {
        private readonly Attribute _attribute;
        private readonly Func<float, float> _filter;
        private readonly ReactiveProperty<float> _max = new ReactiveProperty<float>(0f);
        private readonly IDisposable _maxSubscription;
        private bool _untouched;
        private bool _hasMax;
        private bool _writing;

        /// <summary>The attribute that holds the amount (e.g. Health).</summary>
        public SemanticKey Resource { get; }

        /// <summary>What a change of the maximum does to the amount.</summary>
        public PoolMaxChange OnMaxChange { get; }

        public float Current => _attribute.BaseValue;
        public float Max => _max.Value;

        /// <summary>Current / Max, between 0 and 1 (0 when Max is 0).</summary>
        public float Percent => _max.Value > 0f ? Current / _max.Value : 0f;

        public bool IsEmpty => Current <= 0f;
        public bool IsFull => Current >= _max.Value;

        public IReadOnlyReactiveProperty<float> ObservableCurrent => _attribute.ObservableBaseValue;
        public IReadOnlyReactiveProperty<float> ObservableMax => _max;

        /// <summary>Emits each time the amount drops to 0 (e.g. to handle a death).</summary>
        public IObservable<Unit> Depleted =>
            ObservableCurrent.Pairwise().Where(pair => pair.Previous > 0f && pair.Current <= 0f).AsUnitObservable();

        internal ResourcePool(Entity entity, SemanticKey resource, ValueSource max, PoolMaxChange onMaxChange, ResourcePool replaced)
        {
            Resource = resource;
            OnMaxChange = onMaxChange;
            _attribute = entity.GetOrCreateAttribute(resource);

            // A pool that replaces another keeps its amount, and keeps following the maximum if it was never used.
            _untouched = replaced == null || replaced._untouched;

            _filter = Clamp;
            _attribute.BaseValueFilter = _filter;
            _maxSubscription = (max ?? ValueSource.Const(0f)).GetObservable(entity).Subscribe(OnMaxChanged);
        }

        /// <summary>Removes up to <paramref name="amount"/> (never below 0), and returns how much was removed.</summary>
        public float Reduce(float amount)
        {
            if (!(amount > 0f)) return 0f;
            float before = Current;
            Set(before - amount);
            return before - Current;
        }

        /// <summary>Adds up to <paramref name="amount"/> (never above Max), and returns how much was added.</summary>
        public float Restore(float amount)
        {
            if (!(amount > 0f)) return 0f;
            float before = Current;
            Set(before + amount);
            return Current - before;
        }

        /// <summary>
        /// Removes <paramref name="amount"/> if the pool holds at least that much (e.g. a spell's Mana cost), and
        /// returns whether it did.
        /// </summary>
        public bool TrySpend(float amount)
        {
            if (!(amount >= 0f) || Current < amount) return false;
            Set(Current - amount);
            return true;
        }

        /// <summary>Sets the amount, kept between 0 and Max.</summary>
        public void Set(float amount)
        {
            _untouched = false;
            Write(amount);
        }

        public void Fill() => Set(_max.Value);

        public void Dispose()
        {
            _maxSubscription.Dispose();
            if (_attribute.BaseValueFilter == _filter) _attribute.BaseValueFilter = null;
            _max.Dispose();
        }

        private void OnMaxChanged(float newMax)
        {
            newMax = Math.Max(newMax, 0f);
            float oldMax = _max.Value;
            bool isFirst = !_hasMax;
            _hasMax = true;
            _max.Value = newMax;

            float current = Current;
            float next;
            if (_untouched)
            {
                next = newMax;
            }
            else if (isFirst)
            {
                next = current; // A replacement pool keeps its amount, within the new bounds.
            }
            else
            {
                switch (OnMaxChange)
                {
                    case PoolMaxChange.KeepPercent:
                        next = oldMax > 0f ? current / oldMax * newMax : newMax;
                        break;
                    case PoolMaxChange.AddDifference:
                        next = current + (newMax - oldMax);
                        break;
                    default:
                        next = current;
                        break;
                }
            }

            Write(next);
        }

        private void Write(float amount)
        {
            bool wasWriting = _writing;
            _writing = true;
            try
            {
                _attribute.SetBaseValue(amount);
            }
            finally
            {
                _writing = wasWriting;
            }
        }

        // Every new base value goes through here: from the pool, or written directly (e.g. SetOrUpdateBaseValue).
        private float Clamp(float amount)
        {
            if (!_writing) _untouched = false;
            if (float.IsNaN(amount)) return Current;
            return Math.Min(Math.Max(amount, 0f), _max.Value);
        }
    }
}
