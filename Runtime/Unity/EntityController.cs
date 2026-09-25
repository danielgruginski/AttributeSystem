using ReactiveSolutions.AttributeSystem.Core;
using ReactiveSolutions.AttributeSystem.Core.Data;
using SemanticKeys;
using System;
using UniRx;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Unity
{
    public class EntityController : MonoBehaviour
    {
        [Tooltip("A profile saved as JSON under Resources/Data/EntityProfiles (see Window > Attribute System > Entity Profile Editor). Applied first.")]
        [EntityProfileID]
        public string ProfileId;

        [Tooltip("A profile authored here. Applied after the Profile Id's, so its base values win. Leave it empty to use only the Profile Id.")]
        public EntityProfile Profile = new EntityProfile();

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

            if (!string.IsNullOrEmpty(ProfileId))
            {
                // The loader logs an error if the JSON can't be loaded; the entity then starts from the inline profile.
                _entity.ApplyProfile(EntityProfileJsonLoader.Load(ProfileId), _modifierFactory);
            }

            _entity.ApplyProfile(Profile, _modifierFactory);
        }
    }
}