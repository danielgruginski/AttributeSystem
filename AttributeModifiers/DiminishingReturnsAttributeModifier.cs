using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace algumacoisaqq.AttributeSystem
{
    public class DiminishingReturnsAttributeModifier : BaseAttributeModifier
    {
        private readonly string _inputAttr;
        private readonly float _maxBonus;
        private readonly float _softCap;

        public DiminishingReturnsAttributeModifier(string inputAttr, float maxBonus, float softCap, AttributeMergeMode mode, string sourceId, int priority)
            : base(sourceId, priority, mode)
        {
            _inputAttr = inputAttr;
            _maxBonus = maxBonus;
            _softCap = softCap;
        }

        public override void OnAttach(Attribute target, AttributeController controller)
        {
            base.OnAttach(target, controller);
            WatchDependency(controller, _inputAttr);
        }

        protected override float CalculateMagnitude(AttributeController controller)
        {
            float result;
            float input = Mathf.Max(0, controller.GetOrCreateAttribute(_inputAttr)?.Value ?? 0f);
            result = _maxBonus * (input / (input + _softCap));

            //Debug.Log($"DiminishingReturnsAttributeModifier: Input={input}, MaxBonus={_maxBonus}, SoftCap={_softCap}, Result={result}");
            return result;
        }
    }

    public class DiminishingReturnsFactory : IModifierFactory
    {
        public ModifierSchema GetSchema() => new ModifierSchema { RequiredParams = new[] { "Input", "MaxBonus", "SoftCap" } };

        public IAttributeModifier Create(string src, int prio, AttributeMergeMode mode, Dictionary<string, ValueSource> p)
        {
            string inAttr = p.ContainsKey("input") ? p["input"].AttributeName : "";
            float max = p.ContainsKey("maxbonus") ? p["maxbonus"].ConstantValue : 0f;
            float cap = p.ContainsKey("softcap") ? p["softcap"].ConstantValue : 1f;
            return new DiminishingReturnsAttributeModifier(inAttr, max, cap, mode, src, prio);
        }
    }
}