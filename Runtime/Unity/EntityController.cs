using ReactiveSolutions.AttributeSystem.Core;
using ReactiveSolutions.AttributeSystem.Unity.Data; // Import the SO wrapper
using SemanticKeys;
using System;
using UniRx;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Unity
{
    public class EntityController : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The ScriptableObject wrapper containing the entity's blueprint.")]
        public EntityProfileSO _profileSO;

        private Entity _entity;

        /// <summary>
        /// The entity driven by this controller. Created on first access, so other components can use it
        /// from their own Awake regardless of script execution order.
        /// </summary>
        public Entity Instance
        {
            get
            {
                if (_entity == null) InitializeEntity();
                return _entity;
            }
        }

        private IModifierFactory _modifierFactory;

        private void Awake()
        {
            InitializeEntity();
        }

        private void OnDestroy()
        {
            // Releases the entity's subscriptions to other entities (and its innate StatBlocks).
            _entity?.Dispose();
        }

        public void InitializeEntity()
        {
            if (_entity != null) return;

            _entity = new Entity();
            _modifierFactory = new ModifierFactory();

            if (_profileSO != null && _profileSO.Profile != null)
            {
                // We pass the pure POCO data down into the core engine!
                _entity.ApplyProfile(_profileSO.Profile, _modifierFactory);
            }
            else
            {
                Debug.LogWarning($"[EntityController] No EntityProfileSO assigned on {gameObject.name}. Entity initialized completely empty.");
            }
        }
    }
}