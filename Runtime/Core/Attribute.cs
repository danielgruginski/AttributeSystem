using ReactiveSolutions.AttributeSystem.Core.Data;
using SemanticKeys;
using System;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Core
{
    public class Attribute : IAttribute
    {
        public SemanticKey Name { get; }

        protected readonly ReactiveProperty<float> _baseValue;
        public virtual float BaseValue => _baseValue.Value;

        public virtual IReadOnlyReactiveProperty<float> Value => _finalValue;
        public bool IsDisposed { get; private set; }

        protected readonly ReactiveCollection<IAttributeModifier> _modifiers = new();
        protected readonly ReactiveProperty<float> _finalValue = new();

        protected readonly ReactiveCollection<AttributeReference> _pointerStack = new();

        protected readonly AttributeProcessor _processor;
        protected readonly CompositeDisposable _calculationDisposable = new();
        private IDisposable _currentChainSubscription;

        public IEnumerable<IAttributeModifier> Modifiers => _modifiers;

        public AttributeReference? ActivePointerTarget
        {
            get
            {
                if (_pointerStack.Count > 0)
                    return _pointerStack[_pointerStack.Count - 1];
                return null;
            }
        }

        public Attribute(SemanticKey name, float initialBase, AttributeProcessor processor)
        {
            Name = name;
            _processor = processor;
            _baseValue = new ReactiveProperty<float>(initialBase);

            _modifiers.ObserveCountChanged()
                .StartWith(_modifiers.Count)
                .Subscribe(_ => RebuildCalculationChain())
                .AddTo(_calculationDisposable);

            _pointerStack.ObserveCountChanged()
                .Subscribe(_ => RebuildCalculationChain())
                .AddTo(_calculationDisposable);

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

        public IDisposable AddPointer(SemanticKey targetName, List<SemanticKey> path = null)
        {
            var pointerRef = new AttributeReference { Name = targetName, Path = path };
            _pointerStack.Add(pointerRef);
            return Disposable.Create(() => _pointerStack.Remove(pointerRef));
        }

        protected virtual void RebuildCalculationChain()
        {
            _currentChainSubscription?.Dispose();

            IObservable<float> sourceStream;

            if (_pointerStack.Count > 0)
            {
                var ptr = _pointerStack[_pointerStack.Count - 1];

                sourceStream = _processor.GetAttributeObservable(ptr.Name, ptr.Path)
                    .Select(attr =>
                    {
                        if (attr == null) return Observable.Return(0f);
                        return attr.Value;
                    })
                    .Switch();
            }
            else
            {
                sourceStream = _baseValue;
            }

            var mods = _modifiers.ToList();
            if (mods.Count == 0)
            {
                _currentChainSubscription = sourceStream.Subscribe(val => _finalValue.Value = val);
                return;
            }

            var magnitudeStreams = mods.Select(m => m.GetMagnitude(_processor)).ToList();

            _currentChainSubscription = Observable.CombineLatest(
                magnitudeStreams.Prepend(sourceStream)
            )
            .Subscribe(latest =>
            {
                float baseVal = latest[0];
                float[] modifierValues = latest.Skip(1).ToArray();
                _finalValue.Value = CalculatePipeline(baseVal, mods, modifierValues);
            });
        }

        private float CalculatePipeline(float b, List<IAttributeModifier> mods, float[] values)
        {
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
            IsDisposed = true;
            _currentChainSubscription?.Dispose();
            _calculationDisposable.Dispose();
            _baseValue.Dispose();
            _finalValue.Dispose();
            _pointerStack.Dispose();
            _modifiers.Dispose();
        }
    }
}