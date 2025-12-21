using System;
using System.Collections.Generic;
using UnityEngine;


namespace algumacoisaqq.AttributeSystem
{
    // --- CONTRACTS ---

    public struct ModifierSchema
    {
        public string Description;
        public string[] RequiredParams;
    }

    public interface IModifierFactory
    {
        ModifierSchema GetSchema();
        IAttributeModifier Create(string sourceId, int priority, AttributeMergeMode mode, Dictionary<string, ValueSource> paramsDict);
    }

    // --- REGISTRY ---

    public static class ModifierFactoryRegistry
    {
        private static readonly Dictionary<string, IModifierFactory> _factories = new();

        public static string NormalizeKey(string key)
        {
            // We allow empty strings to pass through as empty here so they trigger the warning in Get()
            if (string.IsNullOrEmpty(key)) return "";
            return key.ToLowerInvariant().Replace(" ", "").Replace("_", "");
        }

        static ModifierFactoryRegistry()
        {
            RegisterDefaults();
        }

        public static void Register(string key, IModifierFactory factory)
        {
            _factories[NormalizeKey(key)] = factory;
        }

        /// <summary>
        /// Retrieves a factory.
        /// </summary>
        /// <param name="key">The operation name (e.g. "Linear")</param>
        /// <param name="context">Debug info about who is asking (e.g. "Sword.json - Damage")</param>
        public static IModifierFactory Get(string key, string context = "Unknown Source")
        {
            string normKey = NormalizeKey(key);

            if (!string.IsNullOrEmpty(normKey) && _factories.TryGetValue(normKey, out var factory))
                return factory;

            Debug.LogWarning($"[ModifierFactoryRegistry] Operation '{key}' not found. Context: {context}. Defaulting to Constant.");


            // Fallback
            if (_factories.TryGetValue("constant", out var defaultFactory))
                return defaultFactory;

            return new ConstantFactory();
        }

        public static IEnumerable<string> GetAvailableKeys() => _factories.Keys;

        private static void RegisterDefaults()
        {
            Register("Constant", new ConstantFactory());
            Register("Linear", new LinearFactory());
            Register("Exponential", new ExponentialFactory());
            Register("Ratio", new RatioFactory());
            Register("DiminishingReturns", new DiminishingReturnsFactory());
            Register("Clamp", new ClampFactory());
            Register("Product", new ProductAsLinearFactory());
            Register("TriangularBonus", new TriangularBonusFactory());
        }
    }

}