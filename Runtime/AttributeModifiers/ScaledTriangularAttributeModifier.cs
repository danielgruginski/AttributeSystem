using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem
{
    /// <summary>
    /// Calculates a scaled triangular root.
    /// Formula: Result = Scale * 0.5 * (sqrt(1 + 8 * Input / Scale) - 1).
    /// Used for Armor Diminishing Returns where 'Scale' is the SoftCap parameter.
    /// </summary>
    public class ScaledTriangularAttributeModifier : BaseAttributeModifier
    {
        private readonly string _inputAttr;
        private readonly float _scale;

        public ScaledTriangularAttributeModifier(string inputAttr, float scale, AttributeMergeMode mode, string sourceId, int priority)
            : base(sourceId, priority, mode)
        {
            _inputAttr = inputAttr;
            _scale = Mathf.Max(0.0001f, scale); // Prevent division by zero
        }

        public override void OnAttach(Attribute target, AttributeController controller)
        {
            base.OnAttach(target, controller);
            WatchDependency(controller, _inputAttr);
        }

        protected override float CalculateMagnitude(AttributeController controller)
        {
            float input = Mathf.Max(0, controller.GetOrCreateAttribute(_inputAttr)?.Value ?? 0f);

            // The Formula: Inverse of Triangular Number logic with a scaling factor N
            float curve = _scale * (Mathf.Sqrt(1 + (8 * input) / _scale) - 1) * 0.5f;

            // Safety cap: Result should never exceed input (which would imply gaining armor > raw value)
            return Mathf.Min(input, curve);
        }
    }

    public class ScaledTriangularFactory : IModifierFactory
    {
        public ModifierSchema GetSchema() => new ModifierSchema { RequiredParams = new[] { "Input", "Scale" } };

        public IAttributeModifier Create(string src, int prio, AttributeMergeMode mode, Dictionary<string, ValueSource> p)
        {
            string inAttr = p.ContainsKey("input") ? p["input"].AttributeName : "";
            float scale = p.ContainsKey("scale") ? p["scale"].ConstantValue : 1f;
            return new ScaledTriangularAttributeModifier(inAttr, scale, mode, src, prio);
        }
    }
}