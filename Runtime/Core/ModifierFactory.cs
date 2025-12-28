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

        public ModifierFactory()
        {
            RegisterDefaults();
        }

        private void RegisterDefaults()
        {
            // --- CLASS BASED ---
            Register(sk.Modifiers.Static, args => new StaticAttributeModifier(args));
            Register(sk.Modifiers.Linear, args => new LinearModifier(args));
            Register(sk.Modifiers.Polynomial, args => new PolynomialModifier(args));

            // --- FUNCTIONAL (Mathf Wrappers) ---
            Register(sk.Modifiers.Clamp, args => new FunctionalModifier(args, vals =>
                Mathf.Clamp(Val(vals, 0), Val(vals, 1), Val(vals, 2))));

            Register(sk.Modifiers.Min, args => new FunctionalModifier(args, vals =>
                Mathf.Min(Val(vals, 0), Val(vals, 1))));

            Register(sk.Modifiers.Max, args => new FunctionalModifier(args, vals =>
                Mathf.Max(Val(vals, 0), Val(vals, 1))));

            Register(sk.Modifiers.Floor, args => new FunctionalModifier(args, vals =>
                Mathf.Floor(Val(vals, 0))));

            Register(sk.Modifiers.Step, args => new FunctionalModifier(args, vals =>
                (Val(vals, 1) >= Val(vals, 0)) ? 1f : 0f));
        }

        public void Register(SemanticKey id, ModifierBuilder builder)
        {
            if (_registry.ContainsKey(id))
            {
                Debug.LogWarning($"[ModifierFactory] Overwriting modifier: {id}");
            }
            _registry[id] = builder;
        }

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

        // Helper
        private static float Val(IList<float> list, int index) =>
            list != null && index < list.Count ? list[index] : 0f;
    }
}