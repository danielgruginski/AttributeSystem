using ReactiveSolutions.AttributeSystem.Core;
using ReactiveSolutions.AttributeSystem.Unity.Data; // Import the SO wrapper
using SemanticKeys;
using System;
using UniRx;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Unity
{
    public class AttributeController : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The ScriptableObject wrapper containing the entity's blueprint.")]
        private EntityProfileSO _profileSO;

        private AttributeProcessor _processor;
        public AttributeProcessor Processor => _processor;

        private IModifierFactory _modifierFactory;

        private void Awake()
        {
            InitializeProcessor();
        }

        public void InitializeProcessor()
        {
            if (_processor != null) return;

            _processor = new AttributeProcessor();
            _modifierFactory = new ModifierFactory();

            if (_profileSO != null && _profileSO.Profile != null)
            {
                // We pass the pure POCO data down into the core engine!
                _processor.ApplyProfile(_profileSO.Profile, _modifierFactory);
            }
            else
            {
                Debug.LogWarning($"[AttributeController] No EntityProfileSO assigned on {gameObject.name}. Processor initialized completely empty.");
            }
        }

        public void AddAttribute(SemanticKey key, float v)
        => Processor.GetOrCreateAttribute(key, v);
    }
}