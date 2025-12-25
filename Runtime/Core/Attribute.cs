
using System;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using UnityEngine;
using UnityEngine.Assertions;

namespace ReactiveSolutions.AttributeSystem.Core
{
    public struct ModifierContribution
    {
        public string SourceId;      // "Iron Sword"
        public string Operation;     // "Linear", "Multiply"
        public float ValueBefore;    // 10
        public float ValueAfter;     // 15
        public float Contribution => ValueAfter - ValueBefore; // +5
    }
    public sealed class Attribute
    {
        public string Name { get; }
        public ReactiveProperty<float> ReactivePropertyAccess { get; }
        public float Value { get => ReactivePropertyAccess.Value; }


        private float _baseValue;
        private readonly List<IAttributeModifier> _mods = new();
        private readonly AttributeProcessor _owner; // A reference to the controller this attribute belongs to
        private bool _isDirtySort = false;

        public Attribute(string name, float baseValue, AttributeProcessor owner)
        {
            Assert.IsNotNull(owner, $"Attribute '{name}' cannot be created with a null owner controller.");
            Name = name;
            _baseValue = baseValue;
            _owner = owner;
            ReactivePropertyAccess = new ReactiveProperty<float>(_baseValue);
        }

        public float BaseValue => _baseValue;

        public void SetBaseValue(float newBase)
        {
            if (Math.Abs(_baseValue - newBase) > Mathf.Epsilon)
            {
                _baseValue = newBase;
                Recalculate();
            }
        }

        /// <summary>
        /// Directly increments the base value of this attribute.
        /// Ideal for "counter" type stats like Level or KillCount.
        /// </summary>
        public void IncrementBaseValue(float amount)
        {
            //if (amount != 0) will it be faster to just recalculate always? Most of the time it is not 0
            //{
            _baseValue += amount;
            Recalculate();
            //}
        }

        public void AddModifier(IAttributeModifier mod)
        {
            _mods.Add(mod);
            _isDirtySort = true;
            Recalculate();
        }

        /// <summary>
        /// Removes modifiers and returns them so the caller can Dispose them.
        /// </summary>
        public IEnumerable<IAttributeModifier> RemoveModifiersBySource(string sourceId)
        {
            var toRemove = new List<IAttributeModifier>();

            // Find
            for (int i = _mods.Count - 1; i >= 0; i--)
            {
                if (_mods[i].SourceId == sourceId)
                {
                    toRemove.Add(_mods[i]);
                    _mods.RemoveAt(i);
                }
            }

            if (toRemove.Count > 0)
            {
                Recalculate();
            }

            return toRemove;
        }

        private static int _recalcDepth = 0;
        private const int MAX_DEPTH = 1000;

        /// <summary>
        /// Recalculates the final value of the attribute using the Chain-of-Responsibility pattern.
        /// The base value starts the chain, and each modifier transforms the value in sequence
        /// based on its priority.
        /// Optimized to avoid LINQ allocations in the hot path.
        /// </summary>
        private void Recalculate()
        {
            if (_recalcDepth > MAX_DEPTH)
            {
                Debug.LogError($"Circular dependency detected in Attribute '{Name}', could not converge in {MAX_DEPTH} iterations ");
                return;
            }

            if (_isDirtySort)
            {
                // Insertion sort is better, but standard List.Sort is better than LINQ OrderBy
                _mods.Sort((a, b) => a.Priority.CompareTo(b.Priority));
                _isDirtySort = false;
            }

            _recalcDepth++; // Track recursion depth for circular dependency detection
            try {
                float currentValue = _baseValue;


                // No foreach allocation, using for-loop on List
                for (int i = 0; i < _mods.Count; i++)
                {
                    currentValue = _mods[i].Apply(currentValue, _owner);
                }

                ReactivePropertyAccess.Value = currentValue;
            }
            finally
            {
                _recalcDepth--;
            }


        }

        /// <summary>
        /// Forces the attribute to run through all its modifiers and update its Value.
        /// Call this when a dependency (like Strength) changes.
        /// </summary>
        public void Reevaluate()
        {
            Recalculate();
        }

        public List<ModifierContribution> GetModifierBreakdown()
        {
            var breakdown = new List<ModifierContribution>();
            float runningTotal = _baseValue;

            // Add Base Entry
            breakdown.Add(new ModifierContribution
            {
                SourceId = "Base",
                Operation = "Base",
                ValueBefore = 0,
                ValueAfter = _baseValue
            });

            if (_isDirtySort) _mods.Sort((a, b) => a.Priority.CompareTo(b.Priority));

            foreach (var mod in _mods)
            {
                float before = runningTotal;
                runningTotal = mod.Apply(runningTotal, _owner);

                breakdown.Add(new ModifierContribution
                {
                    SourceId = mod.SourceId,
                    Operation = mod.GetType().Name.Replace("AttributeModifier", ""),
                    ValueBefore = before,
                    ValueAfter = runningTotal
                });
            }

            return breakdown;
        }

        /*

        // In your TooltipUI.cs

        public void ShowTooltip(string attributeName)
        {
            var attr = _controller.GetAttribute(attributeName);
            var logs = attr.GetModifierBreakdown();

            foreach (var log in logs)
            {
                if (Mathf.Abs(log.Contribution) > 0.01f) // Hide modifiers that do nothing
                    _text.text += $"{log.SourceId}: {log.Contribution:+0.##;-0.##}\n";
            }
        }
        */

    }
}