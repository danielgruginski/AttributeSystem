using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Core
{
    public class SegmentedMultiplierAttributeModifier : BaseAttributeModifier
    {
        private readonly string _inputAttr;
        private readonly List<Vector2> _breakpoints;

        public SegmentedMultiplierAttributeModifier(string inputAttr, List<Vector2> breakpoints, AttributeMergeMode mode, string sourceId, int priority)
            : base(sourceId, priority, mode)
        {
            _inputAttr = inputAttr;
            _breakpoints = breakpoints;
        }

        public override void OnAttach(Attribute target, AttributeProcessor controller)
        {
            base.OnAttach(target, controller);
            WatchDependency(controller, _inputAttr);
        }

        protected override float CalculateMagnitude(AttributeProcessor controller)
        {
            if (_breakpoints == null || _breakpoints.Count == 0) return 0f; // Or 1f depending on logic, keeping 0 to respect merge

            float input = controller.GetOrCreateAttribute(_inputAttr)?.Value ?? 0f;
            return EvaluatePiecewise(input);
        }

        private float EvaluatePiecewise(float input)
        {
            if (input <= _breakpoints[0].x) return _breakpoints[0].y;
            if (input >= _breakpoints[^1].x) return _breakpoints[^1].y;

            for (int i = 0; i < _breakpoints.Count - 1; i++)
            {
                Vector2 p1 = _breakpoints[i];
                Vector2 p2 = _breakpoints[i + 1];

                if (input >= p1.x && input <= p2.x)
                {
                    float t = (input - p1.x) / (p2.x - p1.x);
                    return Mathf.Lerp(p1.y, p2.y, t);
                }
            }
            return _breakpoints[0].y;
        }
    }
}