using System;
using System.Collections.Generic;
using ReactiveSolutions.AttributeSystem.Core.Modifiers;
using SemanticKeys;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Core
{
    public class ModifierFactory : IModifierFactory
    {
        private readonly Dictionary<SemanticKey, ModifierBuilder> _registry = new();

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
        }

        public void Register(SemanticKey id, ModifierBuilder builder, params string[] paramNames)
        {
            if (_registry.ContainsKey(id))
            {
                Debug.LogWarning($"[ModifierFactory] Overwriting modifier: {id}");
            }
            _registry[id] = builder;
            _parameterMetadata[id] = paramNames;
        }
        // Keep the interface implementation
        public void Register(SemanticKey id, ModifierBuilder builder) => Register(id, builder, "Value"); // Default fallback

        public IAttributeModifier Create(SemanticKey id, ModifierArgs args)
        {
            if (string.IsNullOrEmpty(id) || !_registry.TryGetValue(id, out var builder))
            {
                // Fallback / Null Object Pattern
                return new StaticAttributeModifier(args);
            }
            return builder(args);
        }

        public IEnumerable<SemanticKey> GetAvailableTypes() => _registry.Keys;

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