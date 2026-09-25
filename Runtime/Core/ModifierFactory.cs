using ReactiveSolutions.AttributeSystem.Core.Data;
using ReactiveSolutions.AttributeSystem.Core.Modifiers;
using SemanticKeys;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Core
{
    public class ModifierFactory : IModifierFactory
    {
        private readonly Dictionary<string, ModifierBuilder> _registry = new();

        // --- METADATA REGISTRY (Static for Editor Access) ---
        // Maps "LogicType" -> ["Arg Name 1", "Arg Name 2", ...]
        // The built-ins are registered by the static constructor, so the editor drawers can read them
        // without a factory instance (e.g. right after a domain reload).
        private static readonly Dictionary<string, string[]> _parameterMetadata = new();

        private readonly struct Definition
        {
            public readonly string Id;
            public readonly ModifierBuilder Builder;
            public readonly string[] ParameterNames;

            public Definition(string id, ModifierBuilder builder, params string[] parameterNames)
            {
                Id = id;
                Builder = builder;
                ParameterNames = parameterNames;
            }
        }

        // Registry keys are the SemanticKey's string value (e.g. "Linear"), which is what spec.LogicType converts to.
        private static readonly Definition[] _defaults =
        {
            // --- CLASS BASED ---
            new Definition(sk.Modifiers.Static, args => new StaticAttributeModifier(args), "Value"),
            new Definition(sk.Modifiers.Linear, args => new LinearModifier(args), "Input", "Coefficient", "Addend"),
            new Definition(sk.Modifiers.Polynomial, args => new PolynomialModifier(args), "Input", "Power", "Scale", "Flat"),

            // --- FUNCTIONAL (Mathf Wrappers) ---
            new Definition(sk.Modifiers.Clamp, args => new FunctionalModifier(args, vals =>
                Mathf.Clamp(Val(vals, 0), Val(vals, 1), Val(vals, 2))),
                "Input", "Min", "Max"),

            new Definition(sk.Modifiers.Min, args => new FunctionalModifier(args, vals =>
                Mathf.Min(Val(vals, 0), Val(vals, 1))),
                "Value A", "Value B"),

            new Definition(sk.Modifiers.Max, args => new FunctionalModifier(args, vals =>
                Mathf.Max(Val(vals, 0), Val(vals, 1))),
                "Value A", "Value B"),

            new Definition(sk.Modifiers.Floor, args => new FunctionalModifier(args, vals =>
                Mathf.Floor(Val(vals, 0))),
                "Input"),

            new Definition(sk.Modifiers.Step, args => new FunctionalModifier(args, vals =>
                (Val(vals, 1) >= Val(vals, 0)) ? 1f : 0f),
                "Edge Threshold", "Input Value"),

            // --- ADVANCED GAMEPLAY MODIFIERS ---

            // Ratio: A / B
            new Definition(sk.Modifiers.Ratio, args => new FunctionalModifier(args, vals =>
            {
                float d = Val(vals, 1);
                return (Mathf.Abs(d) < 0.0001f) ? Val(vals, 0) : Val(vals, 0) / d;
            }), "Dividend", "Divisor"),

            // Exponential: Base ^ Exponent
            new Definition(sk.Modifiers.Exponential, args => new FunctionalModifier(args, vals =>
                Mathf.Pow(Val(vals, 1), Val(vals, 0))),
                "Exponent", "Base"),

            // DiminishingReturns: Max * (Input / (Input + SoftCap))
            new Definition(sk.Modifiers.DiminishingReturns, args => new FunctionalModifier(args, vals =>
            {
                float inp = Mathf.Max(0, Val(vals, 0));
                float max = Val(vals, 1);
                float cap = Val(vals, 2);
                float denominator = inp + cap;
                // Input and Soft Cap both 0 (e.g. unset arguments or a missing input): no bonus instead of NaN.
                return denominator > 0f ? max * (inp / denominator) : 0f;
            }), "Input", "Max Bonus", "Soft Cap"),

            // ScaledTriangular: Scale * 0.5 * (sqrt(1 + 8 * Input / Scale) - 1)
            // Safety: Result <= Input
            new Definition(sk.Modifiers.ScaledTriangular, args => new FunctionalModifier(args, vals =>
            {
                float inp = Mathf.Max(0, Val(vals, 0));
                float s = Mathf.Max(0.0001f, Val(vals, 1));
                float curve = s * (Mathf.Sqrt(1 + (8 * inp) / s) - 1) * 0.5f;
                return Mathf.Min(inp, curve);
            }), "Input", "Scale"),
        };

        static ModifierFactory()
        {
            foreach (var definition in _defaults)
            {
                _parameterMetadata[definition.Id] = definition.ParameterNames;
            }
        }

        public ModifierFactory()
        {
            foreach (var definition in _defaults)
            {
                _registry[definition.Id] = definition.Builder;
            }
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
                // Lookup is by the key's string value, so a renamed key whose cached value is stale
                // (see SemanticKeys "Update All References") also ends up here.
                if (!string.IsNullOrEmpty(id))
                    Debug.LogWarning($"[ModifierFactory] Unknown modifier logic type '{id}'. Falling back to Static.");

                // Fallback / Null Object Pattern
                return new StaticAttributeModifier(spec);
            }
            return builder(spec);
        }

        /// <summary>
        /// Creates the modifier using the provided Factory service.
        /// The spec is usually shared asset data (StatBlockSO, EntityProfile, LinkGroup), so the context is
        /// baked into per-application copies of its arguments; the spec itself is never modified.
        /// </summary>
        public IAttributeModifier Create(AttributeModifierSpec spec, Entity context = null)
        {
            // 1. Prepare Arguments
            var finalArgs = new List<ValueSource>();
            if (spec.Arguments != null && spec.Arguments.Count > 0)
            {
                foreach (var arg in spec.Arguments)
                {
                    var copy = arg != null ? arg.Clone() : ValueSource.Const(0f);
                    // 2. Bake Context
                    if (context != null) copy.BakeContext(context);
                    finalArgs.Add(copy);
                }
            }
            else Debug.LogWarning($"[ModifierFactory] ModifierSpec '{spec.LogicType}' has no arguments defined.");

            var perApplicationSpec = new AttributeModifierSpec
            {
                TargetAttribute = spec.TargetAttribute,
                TargetPath = spec.TargetPath,
                SourceId = spec.SourceId,
                Type = spec.Type,
                Priority = spec.Priority,
                LogicType = spec.LogicType,
                Arguments = finalArgs
            };

            // 3. Create
            return Create(spec.LogicType, perApplicationSpec);
        }




        public IEnumerable<string> GetAvailableTypes() => _registry.Keys;

        public static string[] GetParameterNames(string id)
        {
            if (TryGetParameterNames(id, out var names)) return names;
            return new string[] { "Value" }; // Default
        }

        /// <summary>
        /// False when the logic type is not registered in this session (e.g. a custom modifier that is only
        /// registered at runtime), so editors can leave its arguments untouched.
        /// </summary>
        public static bool TryGetParameterNames(string id, out string[] names)
        {
            if (!string.IsNullOrEmpty(id) && _parameterMetadata.TryGetValue(id, out names)) return true;
            names = null;
            return false;
        }

        public static IEnumerable<string> GetAllKeys() => _parameterMetadata.Keys;

        // Helper
        private static float Val(IList<float> list, int index) =>
            list != null && index < list.Count ? list[index] : 0f;
    }
}