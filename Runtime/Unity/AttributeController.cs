using ReactiveSolutions.AttributeSystem.Core;
using ReactiveSolutions.AttributeSystem.Core.Data;
using SemanticKeys;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Unity
{
    /// <summary>
    /// Unity-facing component that bridges the AttributeProcessor to the GameObject hierarchy.
    /// Use this for Inspector configuration and easy access from other MonoBehaviours.
    /// </summary>
    [AddComponentMenu("Attribute System/Attribute Controller")]
    public class AttributeController : MonoBehaviour
    {
        [System.Serializable]
        public struct BaseAttributeEntry
        {
            public SemanticKey Name;
            public float BaseValue;
        }

        [Header("Configuration")]
        [SerializeField] private List<BaseAttributeEntry> _initialAttributes = new List<BaseAttributeEntry>();

        private AttributeProcessor _processor;

        /// <summary>
        /// Direct access to the pure C# logic container.
        /// </summary>
        public AttributeProcessor Processor => _processor ??= new AttributeProcessor();

        private void Awake()
        {
            InitializeProcessor();
        }

        private void InitializeProcessor()
        {
            if (_processor == null) _processor = new AttributeProcessor();

            foreach (var entry in _initialAttributes)
            {
                if (!string.IsNullOrEmpty(entry.Name))
                {
                    _processor.SetOrUpdateBaseValue(entry.Name, entry.BaseValue);
                }
            }
        }

        // --- Bridge Methods to Processor ---

        public Core.Attribute GetAttribute(SemanticKey name) => Processor.GetAttribute(name);

        /// <summary>
        /// Allows external systems (like StatBlockLinker) to inject values directly.
        /// </summary>
        public void AddAttribute(SemanticKey name, float baseValue) => Processor.SetOrUpdateBaseValue(name, baseValue);

        /// <summary>
        /// Registers another controller as a provider (e.g. linking the Player as "Owner").
        /// This resolves any pending modifiers waiting for this key.
        /// </summary>
        public void LinkProvider(SemanticKey key, AttributeController provider)
        {
            if (provider != null)
            {
                Processor.RegisterExternalProvider(key, provider.Processor);
            }
        }

        /// <summary>
        /// Bridges the Processor's reactive search. 
        /// Use this to avoid race conditions during initialization.
        /// </summary>
        public IObservable<Core.Attribute> GetAttributeObservable(SemanticKey name)
            => Processor.GetAttributeObservable(name);
    }
}