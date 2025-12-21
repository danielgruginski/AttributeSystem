using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace algumacoisaqq.AttributeSystem
{
    public class RatioAttributeModifier : BaseAttributeModifier
    {
        private readonly string _dividend;
        private readonly string _divisor;

        public RatioAttributeModifier(string dividend, string divisor, AttributeMergeMode mode, string sourceId, int priority)
            : base(sourceId, priority, mode)
        {
            _dividend = dividend;
            _divisor = divisor;
        }

        public override void OnAttach(Attribute target, AttributeController controller)
        {
            base.OnAttach(target, controller);
            WatchDependency(controller, _dividend);
            WatchDependency(controller, _divisor);
        }

        protected override float CalculateMagnitude(AttributeController controller)
        {
            float divVal = controller.GetOrCreateAttribute(_dividend)?.Value ?? 0f;
            float sorVal = controller.GetOrCreateAttribute(_divisor)?.Value ?? 1f;
            if (sorVal == 0) sorVal = 1f;

            return divVal / sorVal;
        }
    }


    public class RatioFactory : IModifierFactory
    {
        public ModifierSchema GetSchema() => new ModifierSchema { RequiredParams = new[] { "Dividend", "Divisor" } };

        public IAttributeModifier Create(string src, int prio, AttributeMergeMode mode, Dictionary<string, ValueSource> p)
        {
            string d1 = p.ContainsKey("dividend") ? p["dividend"].AttributeName : "";
            string d2 = p.ContainsKey("divisor") ? p["divisor"].AttributeName : "";
            return new RatioAttributeModifier(d1, d2, mode, src, prio);
        }
    }

}