using ReactiveSolutions.AttributeSystem;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem
{
    public sealed class ConstantAttributeModifier : BaseAttributeModifier
    {
        private readonly float _amount;

        public ConstantAttributeModifier(float amount, AttributeMergeMode mode, string sourceId, int priority)
            : base(sourceId, priority, mode)
        {
            _amount = amount;
        }

        protected override float CalculateMagnitude(AttributeController controller) => _amount;
    }

    public class ConstantFactory : IModifierFactory
    {
        public ModifierSchema GetSchema() => new ModifierSchema { RequiredParams = new[] { "Value" } };

        public IAttributeModifier Create(string src, int prio, AttributeMergeMode mode, Dictionary<string, ValueSource> p)
        {
            float val = p.ContainsKey("value") ? p["value"].ConstantValue : 0f;
            return new ConstantAttributeModifier(val, mode, src, prio);
        }
    }
}


