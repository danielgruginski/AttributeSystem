using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace algumacoisaqq.AttributeSystem
{
    public sealed class ExponentialAttributeModifier : BaseAttributeModifier
    {
        private readonly ValueSource _exponentSource;
        private readonly float _baseValue;

        public ExponentialAttributeModifier(ValueSource exponentSource, float baseValue, AttributeMergeMode mode, string sourceId, int priority)
            : base(sourceId, priority, mode)
        {
            _exponentSource = exponentSource;
            _baseValue = baseValue;
        }

        public override void OnAttach(Attribute target, AttributeController controller)
        {
            base.OnAttach(target, controller);
            WatchDependency(controller, _exponentSource);
        }

        protected override float CalculateMagnitude(AttributeController controller)
        {
            float exponent = _exponentSource.GetValue(controller);
            return Mathf.Pow(_baseValue, exponent);
        }
    }
    public class ExponentialFactory : IModifierFactory
    {
        public ModifierSchema GetSchema() => new ModifierSchema { RequiredParams = new[] { "Base", "Exponent" } };

        public IAttributeModifier Create(string src, int prio, AttributeMergeMode mode, Dictionary<string, ValueSource> p)
        {
            var exp = p.ContainsKey("exponent") ? p["exponent"] : new ValueSource();
            float b = p.ContainsKey("base") ? p["base"].ConstantValue : 2f;
            return new ExponentialAttributeModifier(exp, b, mode, src, prio);
        }
    }
}