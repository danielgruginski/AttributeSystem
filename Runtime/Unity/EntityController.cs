using ReactiveSolutions.AttributeSystem.Core;
using ReactiveSolutions.AttributeSystem.Core.Data;
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
        [Tooltip("Optional. The ScriptableObject wrapper containing the entity's blueprint. If empty, you must initialize via code using a POCO.")]
        private EntityProfileSO _profileSO;

        private Entity _entity;
        public Entity Instance => _entity;

        private IModifierFactory _modifierFactory;

        private void Awake()
        {
            // Create the shell to prevent NullReferenceExceptions.
            // Only auto-initialize profile if an SO is assigned. Otherwise, wait for manual POCO injection.
            if (_entity == null)
            {
                _entity = new Entity();
                _modifierFactory = new ModifierFactory();

                if (_profileSO != null)
                {
                    InitializeEntity();
                }
            }
        }


        /// <summary>
        /// Initializes the entity engine. If a POCO profile is provided, it uses that.
        /// Otherwise, it falls back to the assigned ScriptableObject.
        /// </summary>
        public void InitializeEntity(EntityProfile explicitProfile = null)
        {
            if (_entity == null)
            {
                _entity = new Entity();
                _modifierFactory = new ModifierFactory();
            }

            // Prioritize the explicitly passed POCO, then fallback to the SO
            var profileToApply = explicitProfile ?? _profileSO?.Profile;

            if (profileToApply != null)
            {
                // We pass the pure POCO data down into the core engine!
                _entity.ApplyProfile(profileToApply, _modifierFactory);
            }
            else
            {
                Debug.LogWarning($"[EntityController] No EntityProfile provided or SO assigned on {gameObject.name}. Entity initialized completely empty.");
            }
        }
    }
}