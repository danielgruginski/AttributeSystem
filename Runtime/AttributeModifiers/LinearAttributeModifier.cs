using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem
{
    public sealed class LinearAttributeModifier : BaseAttributeModifier
    {
        private readonly ValueSource _source;
        private readonly float _coeff;
        private readonly float _addend;

        public LinearAttributeModifier(ValueSource source, float coeff, float addend, AttributeMergeMode mode, string sourceId, int priority)
            : base(sourceId, priority, mode)
        {
            _source = source;
            _coeff = coeff;
            _addend = addend;
        }

        public override void OnAttach(Attribute target, AttributeController controller)
        {
            base.OnAttach(target, controller);
            // AUTOMATIC BINDING HERE
            WatchDependency(controller, _source);
        }

        protected override float CalculateMagnitude(AttributeController controller)
        {
            float input = _source.GetValue(controller);
            return (input * _coeff) + _addend;
        }
    }

    public class LinearFactory : IModifierFactory
    {
        public ModifierSchema GetSchema() => new ModifierSchema { RequiredParams = new[] { "Input", "Coefficient", "Addend" } };

        public IAttributeModifier Create(string src, int prio, AttributeMergeMode mode, Dictionary<string, ValueSource> p)
        {
            var input = p.ContainsKey("input") ? p["input"] : new ValueSource();
            float coeff = p.ContainsKey("coefficient") ? p["coefficient"].ConstantValue : 1f;
            float addend = p.ContainsKey("addend") ? p["addend"].ConstantValue : 0f;
            return new LinearAttributeModifier(input, coeff, addend, mode, src, prio);
        }
    }


    public class ProductAsLinearFactory : IModifierFactory
    {
        public ModifierSchema GetSchema() => new ModifierSchema { RequiredParams = new[] { "Multiplier" } };

        public IAttributeModifier Create(string src, int prio, AttributeMergeMode mode, Dictionary<string, ValueSource> p)
        {
            var input = p.ContainsKey("multiplier") ? p["multiplier"] : new ValueSource();
            return new LinearAttributeModifier(input, 1f, 0f, mode, src, prio);
        }
    }

}