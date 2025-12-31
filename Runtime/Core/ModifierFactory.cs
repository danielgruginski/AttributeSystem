using ReactiveSolutions.AttributeSystem.Core.Data;
using ReactiveSolutions.AttributeSystem.Core.Modifiers;
using SemanticKeys;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.DedicatedServer;

namespace ReactiveSolutions.AttributeSystem.Core
{
    public class ModifierFactory : IModifierFactory
    {
        private readonly Dictionary<string, ModifierBuilder> _registry = new();

        // --- METADATA REGISTRY (Static for Editor Access) ---
        // Maps "LogicType" -> ["Arg Name 1", "Arg Name 2", ...]
        private static readonly Dictionary<string, string[]> _parameterMetadata = new();
        public ModifierFactory()
        {
            RegisterDefaults();
        }




        private void RegisterDefaults()
        {
            // --- CLASS BASED ---
            Register(sk.Modifiers.Static, args => new StaticAttributeModifier(args), "Value");
            Register(sk.Modifiers.Linear, args => new LinearModifier(args), "Input", "Coefficient", "Addend");
            Register(sk.Modifiers.Polynomial, args => new PolynomialModifier(args), "Input", "Power", "Scale", "Flat");

            // --- FUNCTIONAL (Mathf Wrappers) ---
            Register(sk.Modifiers.Clamp, args => new FunctionalModifier(args, vals =>
                Mathf.Clamp(Val(vals, 0), Val(vals, 1), Val(vals, 2))),
                "Input", "Min", "Max");

            Register(sk.Modifiers.Min, args => new FunctionalModifier(args, vals =>
                Mathf.Min(Val(vals, 0), Val(vals, 1))),
                "Value A", "Value B");

            Register(sk.Modifiers.Max, args => new FunctionalModifier(args, vals =>
                Mathf.Max(Val(vals, 0), Val(vals, 1))),
                "Value A", "Value B");

            Register(sk.Modifiers.Floor, args => new FunctionalModifier(args, vals =>
                Mathf.Floor(Val(vals, 0))),
                "Input");

            Register(sk.Modifiers.Step, args => new FunctionalModifier(args, vals =>
                (Val(vals, 1) >= Val(vals, 0)) ? 1f : 0f),
                "Edge Threshold", "Input Value");
            // --- ADVANCED GAMEPLAY MODIFIERS (New) ---

            // Ratio: A / B
            Register("Ratio", args => new FunctionalModifier(args, vals =>
            {
                float d = Val(vals, 1);
                return (Mathf.Abs(d) < 0.0001f) ? Val(vals, 0) : Val(vals, 0) / d;
            }), "Dividend", "Divisor");

            // Exponential: Base ^ Exponent
            Register("Exponential", args => new FunctionalModifier(args, vals =>
                Mathf.Pow(Val(vals, 1), Val(vals, 0))),
                "Exponent", "Base");

            // DiminishingReturns: Max * (Input / (Input + SoftCap))
            Register("DiminishingReturns", args => new FunctionalModifier(args, vals =>
            {
                float inp = Mathf.Max(0, Val(vals, 0));
                float max = Val(vals, 1);
                float cap = Val(vals, 2);
                return max * (inp / (inp + cap));
            }), "Input", "Max Bonus", "Soft Cap");

            // ScaledTriangular: Scale * 0.5 * (sqrt(1 + 8 * Input / Scale) - 1)
            // Safety: Result <= Input
            Register("ScaledTriangular", args => new FunctionalModifier(args, vals =>
            {
                float inp = Mathf.Max(0, Val(vals, 0));
                float s = Mathf.Max(0.0001f, Val(vals, 1));
                float curve = s * (Mathf.Sqrt(1 + (8 * inp) / s) - 1) * 0.5f;
                return Mathf.Min(inp, curve);
            }), "Input", "Scale");
        }

        public void Register(string id, ModifierBuilder builder, params string[] paramNames)
        {
            if (_registry.ContainsKey(id))
            {
                Debug.LogWarning($"[ModifierFactory] Overwriting modifier: {id}");
            }
            _registry[id] = builder;
            _parameterMetadata[id] = paramNames;
        }
        // Keep the interface implementation
        public void Register(string id, ModifierBuilder builder) => Register(id, builder, "Value"); // Default fallback

        public IAttributeModifier Create(string id, AttributeModifierSpec spec)
        {
            if (string.IsNullOrEmpty(id) || !_registry.TryGetValue(id, out var builder))
            {
                // Fallback / Null Object Pattern
                return new StaticAttributeModifier(spec);
            }
            return builder(spec);
        }

        /// <summary>
        /// Creates the modifier using the provided Factory service.
        /// </summary>
        public IAttributeModifier Create(AttributeModifierSpec spec, AttributeProcessor context = null)
        {
            // 1. Prepare Arguments
            var finalArgs = new List<ValueSource>();
            if (spec.Arguments != null && spec.Arguments.Count > 0) finalArgs.AddRange(spec.Arguments);
            else Debug.LogWarning($"[ModifierFactory] ModifierSpec '{spec.LogicType}' has no arguments defined.");

            // 2. Bake Context
            if (context != null)
            {
                foreach (var arg in finalArgs) arg.BakeContext(context);
            }

            // 3. Create
            return Create(spec.LogicType, spec);
        }




        public IEnumerable<string> GetAvailableTypes() => _registry.Keys;

        public static string[] GetParameterNames(string id)
        {
            if (_parameterMetadata.TryGetValue(id, out var names)) return names;
            return new string[] { "Value" }; // Default
        }

        public static IEnumerable<string> GetAllKeys() => _parameterMetadata.Keys;

        // Helper
        private static float Val(IList<float> list, int index) =>
            list != null && index < list.Count ? list[index] : 0f;
    }
}