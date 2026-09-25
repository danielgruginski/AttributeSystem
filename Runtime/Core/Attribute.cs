using ReactiveSolutions.AttributeSystem.Core.Data;
using SemanticKeys;
using System;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Core
{
    public class Attribute : IAttribute
    {
        /// <summary>
        /// How many times a single update may re-enter this attribute's own calculation before it is
        /// treated as a circular dependency (e.g. a modifier whose input is this attribute's final value).
        /// </summary>
        public const int MaxRecalculationDepth = 32;

        /// <summary>
        /// A modifier applied to this attribute, together with its live magnitude.
        /// </summary>
        private sealed class ModifierSlot
        {
            public IAttributeModifier Modifier;
            public long Sequence; // Insertion order, keeps ties stable.
            public IDisposable Subscription;
            public float Magnitude;
            public bool HasMagnitude;
            public bool Removed;
        }

        private sealed class PointerEntry
        {
            public AttributeReference Target;
        }

        public SemanticKey Name { get; }

        protected readonly ReactiveProperty<float> _baseValue;
        public virtual float BaseValue => _baseValue.Value;

        /// <summary>The base value as it changes (a pool's current amount).</summary>
        internal IReadOnlyReactiveProperty<float> ObservableBaseValue => _baseValue;

        /// <summary>Set by a ResourcePool: keeps every new base value (the pool's amount) within the pool's bounds.</summary>
        internal Func<float, float> BaseValueFilter;

        public virtual IReadOnlyReactiveProperty<float> ObservableValue => _finalValue;
        public bool IsDisposed { get; private set; }

        protected readonly ReactiveProperty<float> _finalValue = new();
        protected readonly Entity _processor;

        // Sorted by (Priority, Type, Sequence), i.e. the order the pipeline applies them in.
        private readonly List<ModifierSlot> _slots = new();
        private readonly List<PointerEntry> _pointerStack = new();
        private readonly SerialDisposable _sourceSubscription = new();

        private float _sourceValue;
        private bool _hasSourceValue;
        private long _nextSequence;
        private int _recalculationDepth;
        private bool _circularDependencyReported;

        /// <summary>
        /// The applied modifiers, in the order they are evaluated.
        /// </summary>
        public IEnumerable<IAttributeModifier> Modifiers => _slots.ConvertAll(s => s.Modifier);

        public AttributeReference? ActivePointerTarget
        {
            get
            {
                if (_pointerStack.Count > 0)
                    return _pointerStack[_pointerStack.Count - 1].Target;
                return null;
            }
        }

        public Attribute(SemanticKey name, float initialBase, Entity processor)
        {
            Name = name;
            _processor = processor;
            _baseValue = new ReactiveProperty<float>(initialBase);
            SubscribeToSource();
        }

        public virtual void SetBaseValue(float value)
        {
            if (IsDisposed) return;
            if (BaseValueFilter != null) value = BaseValueFilter(value);
            _baseValue.Value = value;
        }

        /// <summary>
        /// Adds a modifier. Its Type and Priority are read once, here, to place it in the pipeline.
        /// Disposing the returned handle removes exactly this application of the modifier.
        /// </summary>
        public virtual IDisposable AddModifier(IAttributeModifier modifier)
        {
            if (modifier == null)
            {
                Debug.LogError($"[Attribute] Attempted to add null modifier to {Name}");
                return Disposable.Empty;
            }
            if (IsDisposed) return Disposable.Empty;

            var slot = new ModifierSlot { Modifier = modifier, Sequence = _nextSequence++ };
            _slots.Insert(FindInsertIndex(slot), slot);

            IDisposable subscription;
            try
            {
                subscription = modifier.GetMagnitude(_processor).Subscribe(value =>
                {
                    if (slot.Removed) return;
                    slot.Magnitude = value;
                    slot.HasMagnitude = true;
                    Recalculate();
                });
            }
            catch
            {
                RemoveSlot(slot);
                throw;
            }

            // The magnitude callback can remove this slot re-entrantly before Subscribe returns.
            if (slot.Removed) subscription.Dispose();
            else slot.Subscription = subscription;

            return Disposable.Create(() => RemoveSlot(slot));
        }

        /// <summary>
        /// Removes one application of the given modifier (the earliest added, if applied more than once).
        /// </summary>
        public virtual void RemoveModifier(IAttributeModifier modifier)
        {
            var slot = _slots.Find(s => s.Modifier == modifier);
            if (slot != null) RemoveSlot(slot);
        }

        public IDisposable AddPointer(SemanticKey targetName, List<SemanticKey> path = null)
        {
            if (IsDisposed) return Disposable.Empty;

            var entry = new PointerEntry { Target = new AttributeReference { Name = targetName, Path = path } };
            _pointerStack.Add(entry);
            SubscribeToSource();
            return Disposable.Create(() => RemovePointer(entry));
        }

        private void RemovePointer(PointerEntry entry)
        {
            if (IsDisposed) return;

            int index = _pointerStack.IndexOf(entry);
            if (index < 0) return;

            bool wasActive = index == _pointerStack.Count - 1;
            _pointerStack.RemoveAt(index);
            if (wasActive) SubscribeToSource();
        }

        private void RemoveSlot(ModifierSlot slot)
        {
            if (slot.Removed) return;
            slot.Removed = true;
            _slots.Remove(slot);
            slot.Subscription?.Dispose();

            if (slot.HasMagnitude) Recalculate();
        }

        /// <summary>
        /// Follows the value the pipeline starts from: the base value, or the active pointer's target.
        /// A missing pointer target reads as 0.
        /// </summary>
        private void SubscribeToSource()
        {
            _sourceSubscription.Disposable = null;
            _hasSourceValue = false;

            IObservable<float> source;
            if (_pointerStack.Count > 0)
            {
                var target = _pointerStack[_pointerStack.Count - 1].Target;
                source = _processor.ObserveAttribute(target.Name, target.Path, emitNullIfMissing: true)
                    .Select(attr => attr == null ? Observable.Return(0f) : (IObservable<float>)attr.ObservableValue)
                    .Switch();
            }
            else
            {
                source = _baseValue;
            }

            _sourceSubscription.Disposable = source.Subscribe(value =>
            {
                _sourceValue = value;
                _hasSourceValue = true;
                Recalculate();
            });
        }

        private int FindInsertIndex(ModifierSlot slot)
        {
            int index = _slots.Count;
            while (index > 0 && Compare(_slots[index - 1], slot) > 0) index--;
            return index;
        }

        /// <summary>
        /// Pipeline order: lower Priority first; within a Priority, additive, then multiplicative, then override,
        /// then clamp modifiers (so multipliers scale base + additives, and clamps limit the result); then insertion order.
        /// </summary>
        private static int Compare(ModifierSlot a, ModifierSlot b)
        {
            int byPriority = a.Modifier.Priority.CompareTo(b.Modifier.Priority);
            if (byPriority != 0) return byPriority;

            int byType = ((int)a.Modifier.Type).CompareTo((int)b.Modifier.Type);
            if (byType != 0) return byType;

            return a.Sequence.CompareTo(b.Sequence);
        }

        protected virtual void Recalculate()
        {
            if (IsDisposed || !_hasSourceValue) return;

            if (_recalculationDepth >= MaxRecalculationDepth)
            {
                if (!_circularDependencyReported)
                {
                    _circularDependencyReported = true;
                    Debug.LogError($"[Attribute] Circular dependency on '{Name}': updating it re-triggered its own calculation {MaxRecalculationDepth} times. " +
                                   "A modifier, pointer or condition probably depends on this attribute's own value.");
                }
                return;
            }

            _recalculationDepth++;
            try
            {
                _finalValue.Value = CalculatePipeline();
            }
            finally
            {
                _recalculationDepth--;
                if (_recalculationDepth == 0) _circularDependencyReported = false;
            }
        }

        private float CalculatePipeline()
        {
            float result = _sourceValue;

            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (!slot.HasMagnitude) continue; // Not resolved yet: contributes nothing.

                switch (slot.Modifier.Type)
                {
                    case ModifierType.Additive:
                        result += slot.Magnitude;
                        break;
                    case ModifierType.Multiplicative:
                        result *= slot.Magnitude;
                        break;
                    case ModifierType.Override:
                        result = slot.Magnitude;
                        break;
                    case ModifierType.ClampMin:
                        result = Math.Max(result, slot.Magnitude);
                        break;
                    case ModifierType.ClampMax:
                        result = Math.Min(result, slot.Magnitude);
                        break;
                }
            }
            return result;
        }

        public virtual void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;

            _sourceSubscription.Dispose();
            foreach (var slot in _slots)
            {
                slot.Removed = true;
                slot.Subscription?.Dispose();
            }
            _slots.Clear();
            _pointerStack.Clear();
            _baseValue.Dispose();
            _finalValue.Dispose();
        }
    }
}
