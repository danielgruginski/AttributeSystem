using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace algumacoisaqq.AttributeSystem
{
    public class TriangularBonusAttributeModifier : BaseAttributeModifier
    {
        private readonly string _inputAttr;

        public TriangularBonusAttributeModifier(string inputAttr, AttributeMergeMode mode, string sourceId, int priority)
            : base(sourceId, priority, mode)
        {
            _inputAttr = inputAttr;
        }

        public override void OnAttach(Attribute target, AttributeController controller)
        {
            base.OnAttach(target, controller);
            WatchDependency(controller, _inputAttr);
        }

        protected override float CalculateMagnitude(AttributeController controller)
        {
            float input = Mathf.Max(0, controller.GetOrCreateAttribute(_inputAttr)?.Value ?? 0f);
            return Mathf.Floor((Mathf.Sqrt(8 * input + 1) - 1) / 2);
        }
    }

    public class TriangularBonusFactory : IModifierFactory
    {
        public ModifierSchema GetSchema() => new ModifierSchema { RequiredParams = new[] { "Input" } };

        public IAttributeModifier Create(string src, int prio, AttributeMergeMode mode, Dictionary<string, ValueSource> p)
        {
            string inAttr = p.ContainsKey("input") ? p["input"].AttributeName : "";
            return new TriangularBonusAttributeModifier(inAttr, mode, src, prio);
        }
    }
}