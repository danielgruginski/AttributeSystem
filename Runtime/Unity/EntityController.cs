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
        [Tooltip("A profile saved as JSON under Resources/Data/EntityProfiles (see Tools > Attribute System > Entity Profile Editor). Applied first.")]
        [EntityProfileID]
        public string ProfileId;

        [Tooltip("A profile authored here. Applied after the Profile Id's, so its base values win. Leave it empty to use only the Profile Id.")]
        public EntityProfile Profile = new EntityProfile();

        [Tooltip("Advances the entity's status effects every frame by Time.deltaTime (their durations and ticks in seconds). " +
                 "Turn it off to advance them yourself, e.g. once per turn with Instance.TickStatusEffects(1).")]
        public bool TickStatusEffects = true;

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

        private void Awake()
        {
            InitializeEntity();
        }

        private void Update()
        {
            if (TickStatusEffects) _entity?.TickStatusEffects(Time.deltaTime);
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

            if (!string.IsNullOrEmpty(ProfileId))
            {
                // The loader logs an error if the JSON can't be loaded; the entity then starts from the inline profile.
                _entity.ApplyProfile(EntityProfileJsonLoader.Load(ProfileId));
            }

            _entity.ApplyProfile(Profile);
        }
    }
}