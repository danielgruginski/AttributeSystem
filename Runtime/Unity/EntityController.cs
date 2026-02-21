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
        private EntityProfileSO _profileSO;

        private Entity _entity;
        public Entity Instance => _entity;

        private IModifierFactory _modifierFactory;

        private void Awake()
        {
            InitializeEntity();
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