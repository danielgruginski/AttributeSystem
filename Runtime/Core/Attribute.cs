
using SemanticKeys;
using System;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Core
{
    /// <summary>
    /// The core math engine for a single attribute.
    /// Calculates value through a sequential pipeline sorted by priority.
    /// </summary>
    public class Attribute : IAttribute
    {
        public SemanticKey Name { get; }

        protected readonly ReactiveProperty<float> _baseValue; 
        public virtual float BaseValue => _baseValue.Value;

        public virtual IReadOnlyReactiveProperty<float> Value => _finalValue;
        public bool IsDisposed { get; private set; } // Implement Interface

        protected readonly ReactiveCollection<IAttributeModifier> _modifiers = new();
        protected readonly ReactiveProperty<float> _finalValue = new();

        protected readonly AttributeProcessor _processor;
        protected readonly CompositeDisposable _calculationDisposable = new();
        private IDisposable _currentChainSubscription;

        
       

        /// <summary>
        /// Exposes the current modifiers for queries (like removal by SourceId).
        /// </summary>
        public IEnumerable<IAttributeModifier> Modifiers => _modifiers;


        public Attribute(SemanticKey name, float initialBase, AttributeProcessor processor)
        {
            Name = name;
            _processor = processor;
            _baseValue = new ReactiveProperty<float>(initialBase);

            // Rebuild the math chain whenever the list of modifiers changes
            _modifiers.ObserveCountChanged()
                .StartWith(_modifiers.Count)
                .Subscribe(_ => RebuildCalculationChain())
                .AddTo(_calculationDisposable);

            // Rebuild if the base value changes
            _baseValue.Subscribe(_ => RebuildCalculationChain()).AddTo(_calculationDisposable);
        }

        public virtual void SetBaseValue(float value) => _baseValue.Value = value;

        public virtual IDisposable AddModifier(IAttributeModifier modifier)
        {
            Debug.Assert(modifier != null, $"[Attribute] Attempted to add null modifier to {Name}");
            _modifiers.Add(modifier);

            return Disposable.Create(() => RemoveModifier(modifier));
        }

        public virtual void RemoveModifier(IAttributeModifier modifier)
        {
            _modifiers.Remove(modifier);
        }

        /// <summary>
        /// Rebuilds the calculation pipeline. 
        /// Virtual so PointerAttribute can override it to do nothing (since it delegates math).
        /// </summary>
        protected virtual void RebuildCalculationChain()
        {
            _currentChainSubscription?.Dispose();

            if (_modifiers.Count == 0)
            {
                _finalValue.Value = _baseValue.Value;
                return;
            }

            // Capture the current list of modifiers and their magnitudes
            var mods = _modifiers.ToList();
            var magnitudeStreams = mods.Select(m => m.GetMagnitude(_processor)).ToList();

            // The pipeline: BaseValue + all individual modifier streams
            _currentChainSubscription = Observable.CombineLatest(magnitudeStreams.Prepend(_baseValue))
                .Subscribe(latest =>
                {
                    float b = latest[0];
                    float[] values = latest.Skip(1).ToArray();
                    _finalValue.Value = CalculatePipeline(b, mods, values);
                });
        }

        private float CalculatePipeline(float b, List<IAttributeModifier> mods, float[] values)
        {
            // Zip and sort by priority for the sequential flow
            var pipeline = mods.Select((m, i) => new { Modifier = m, Val = values[i] })
                               .OrderBy(x => x.Modifier.Priority);

            float result = b;

            foreach (var step in pipeline)
            {
                switch (step.Modifier.Type)
                {
                    case ModifierType.Additive:
                        result += step.Val;
                        break;
                    case ModifierType.Multiplicative:
                        result *= step.Val;
                        break;
                    case ModifierType.Override:
                        result = step.Val;
                        break;
                }
            }
            return result;
        }

        public virtual void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true; // Mark as dead
            _currentChainSubscription?.Dispose();
            _calculationDisposable.Dispose();
            _baseValue.Dispose();
            _finalValue.Dispose();
        }


    }
}