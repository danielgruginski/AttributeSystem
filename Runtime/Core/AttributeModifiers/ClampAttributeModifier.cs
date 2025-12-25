using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Core
{

    // Clamp acts ON the value, it doesn't merge WITH the value in a standard way.
    public sealed class ClampAttributeModifier : BaseAttributeModifier
    {
        private readonly float _min;
        private readonly float _max;

        public ClampAttributeModifier(float min, float max, string sourceId, int priority)
            : base(sourceId, priority, AttributeMergeMode.Override) // Mode irrelevant here, but we pass Override to be safe
        {
            _min = min;
            _max = max;
        }

        protected override float CalculateMagnitude(AttributeProcessor controller) => 0; // Unused

        // Override Apply completely because Clamp logic is unique (Transformation vs Combination)
        public override float Apply(float currentValue, AttributeProcessor controller)
        {
            return Mathf.Clamp(currentValue, _min, _max);
        }
    }


    public class ClampFactory : IModifierFactory
    {
        public ModifierSchema GetSchema() => new ModifierSchema { RequiredParams = new[] { "Min", "Max" } };

        public IAttributeModifier Create(string src, int prio, AttributeMergeMode mode, Dictionary<string, ValueSource> p)
        {
            float min = p.ContainsKey("min") ? p["min"].ConstantValue : 0f;
            float max = p.ContainsKey("max") ? p["max"].ConstantValue : 100f;
            return new ClampAttributeModifier(min, max, src, prio);
        }
    }
}