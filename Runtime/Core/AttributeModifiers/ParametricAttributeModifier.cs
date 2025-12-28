using System;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Core.Modifiers
{
    // ========================================================================
    // BASE CLASS (Unified Argument Resolution)
    // ========================================================================
    public abstract class ParametricAttributeModifier : IAttributeModifier
    {
        public string SourceId { get; }
        public ModifierType Type { get; }
        public int Priority { get; }

        // We no longer distinguish between "Source" and "Constant".
        // Everything is just an argument that produces a value.
        protected readonly List<ValueSource> _arguments;

        public ParametricAttributeModifier(string sourceId, ModifierType type, int priority,
                                           IList<ValueSource> arguments)
        {
            SourceId = sourceId;
            Type = type;
            Priority = priority;
            _arguments = arguments != null ? new List<ValueSource>(arguments) : new List<ValueSource>();
        }

        public IObservable<float> GetMagnitude(AttributeProcessor processor)
        {
            if (_arguments.Count == 0)
                return Observable.Return(Calculate(Array.Empty<float>()));

            // Convert all arguments (constants or attributes) into streams
            var streams = _arguments.Select(s => s.GetObservable(processor));

            // CombineLatest waits for all streams to emit at least once, 
            // then updates whenever ANY of them changes.
            return Observable.CombineLatest(streams).Select(values => Calculate(values));
        }

        /// <summary>
        /// Calculates the final modifier value based on the resolved arguments.
        /// </summary>
        /// <param name="args">The current values of the arguments, in the order they were defined.</param>
        protected abstract float Calculate(IList<float> args);
    }

    // ========================================================================
    // FUNCTIONAL MODIFIER (The "Mathf Mirror")
    // ========================================================================
    public class FunctionalModifier : ParametricAttributeModifier
    {
        private readonly Func<IList<float>, float> _operation;

        // Constructor matches the Unified ModifierArgs
        public FunctionalModifier(ModifierArgs args, Func<IList<float>, float> operation)
            : base(args.SourceId, args.Type, args.Priority, args.Arguments)
        {
            _operation = operation;
        }

        protected override float Calculate(IList<float> args) => _operation(args);

    }

    // ========================================================================
    // STANDARD IMPLEMENTATIONS (Refactored for Unification)
    // ========================================================================

    /// <summary>
    /// Logic: (Arg[0] * Arg[1]) + Arg[2]
    /// Args: [0] Input, [1] Coefficient, [2] Addend
    /// Note: Coeff and Addend can now be Attributes too! (e.g. Dynamic Scaling)
    /// </summary>
    public class LinearModifier : ParametricAttributeModifier
    {
        public LinearModifier(ModifierArgs args)
            : base(args.SourceId, args.Type, args.Priority, args.Arguments) { }

        protected override float Calculate(IList<float> args)
        {
            float input = args.Count > 0 ? args[0] : 0f;
            float coeff = args.Count > 1 ? args[1] : 1f;
            float addend = args.Count > 2 ? args[2] : 0f;
            return (input * coeff) + addend;
        }
    }

    /// <summary>
    /// Logic: (Arg[0] ^ Arg[1]) * Arg[2] + Arg[3]
    /// Args: [0] Input, [1] Power, [2] Scale, [3] Flat
    /// </summary>
    public class PolynomialModifier : ParametricAttributeModifier
    {
        public PolynomialModifier(ModifierArgs args)
            : base(args.SourceId, args.Type, args.Priority, args.Arguments) { }

        protected override float Calculate(IList<float> args)
        {
            float input = args.Count > 0 ? args[0] : 0f;
            float power = args.Count > 1 ? args[1] : 1f;
            float scale = args.Count > 2 ? args[2] : 1f;
            float flat = args.Count > 3 ? args[3] : 0f;
            return (Mathf.Pow(input, power) * scale) + flat;
        }
    }

    /// <summary>
    /// Logic: Returns Arg[0]. Useful for "Override" modifiers that just set a specific value.
    /// </summary>
    public class StaticAttributeModifier : ParametricAttributeModifier
    {
        public StaticAttributeModifier(ModifierArgs args)
            : base(args.SourceId, args.Type, args.Priority, args.Arguments) { }

        protected override float Calculate(IList<float> args)
        {
            return args.Count > 0 ? args[0] : 0f;
        }
    }
}