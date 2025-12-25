using System;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Core.Modifiers
{
    /// <summary>
    /// A modifier that returns a different multiplier based on which threshold a source value meets.
    /// Useful for RPG 'Breakpoints' or tiered bonuses.
    /// </summary>
    [Serializable]
    public class SegmentedMultiplierAttributeModifier : IAttributeModifier
    {
        [Serializable]
        public struct MultiplierSegment
        {
            [Tooltip("The minimum value required to use this multiplier.")]
            public float Threshold;
            [Tooltip("The multiplier value (e.g., 1.5 for +50%).")]
            public float Multiplier;
        }

        [SerializeField] private ModifierType _type = ModifierType.Multiplicative;
        [SerializeField] private int _priority = 10;
        [SerializeField] private string _sourceID;

        [Header("Source & Segments")]
        [SerializeField] private ValueSource _source;
        [SerializeField] private float _defaultMultiplier = 1.0f;
        [SerializeField] private List<MultiplierSegment> _segments = new();

        public ModifierType Type => _type;
        public int Priority => _priority;

        public string SourceId => _sourceID;

        public IObservable<float> GetMagnitude(AttributeProcessor processor)
        {
            // Sort segments by threshold descending so we find the highest met threshold first
            var sortedSegments = _segments.OrderByDescending(s => s.Threshold).ToList();

            return _source.GetObservable(processor)
                .Select(input =>
                {
                    foreach (var segment in sortedSegments)
                    {
                        if (input >= segment.Threshold)
                        {
                            return segment.Multiplier;
                        }
                    }
                    return _defaultMultiplier;
                });
        }
    }
}