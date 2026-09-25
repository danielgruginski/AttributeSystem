using ReactiveSolutions.AttributeSystem.Core;
using SemanticKeys;
using UniRx;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Unity
{
    /// <summary>
    /// A bridge component that registers one AttributeController as an external provider
    /// for another. This enables cross-referencing stats (e.g., "Owner.Strength").
    /// </summary>
    [AddComponentMenu("Attribute System/Attribute Context Linker")]
    public class AttributeContextLinker : MonoBehaviour
    {
        [Header("Roles")]
        [Tooltip("The controller that will RECEIVE the link (e.g., the Sword). " +
                 "Its modifiers can now use the Alias to find the Provider's stats.")]
        [SerializeField] private EntityController _receiver;

        [Tooltip("The controller that will PROVIDE the stats (e.g., the Player).")]
        [SerializeField] private EntityController _provider;

        [Header("Configuration")]
        [Tooltip("The key that will me used in the path to refer to this object (e.g. Owner, Holder, EquippedItem)")]
        [SerializeField] private SemanticKey _alias;

        [Tooltip("If true, the link is established during Awake.")]
        [SerializeField] private bool _linkOnAwake = true;

        private void Awake()
        {
            if (_linkOnAwake)
            {
                LinkContext();
            }
        }

        // The registration this component made, so it can be undone exactly even after the fields change.
        private Entity _linkedReceiver;
        private Entity _linkedProvider;
        private SemanticKey _linkedAlias;

        /// <summary>
        /// Establishes the relationship between the two processors.
        /// </summary>
        public void LinkContext()
        {
            // Fallback: If receiver is null, try to find it on this GameObject
            if (_receiver == null)
            {
                _receiver = GetComponent<EntityController>();
            }

            Debug.Assert(_receiver != null, $"[AttributeContextLinker] No Receiver (Target) Controller assigned on {gameObject.name}");
            Debug.Assert(_provider != null, $"[AttributeContextLinker] No Provider (Source) Controller assigned for Alias '{_alias}' on {gameObject.name}");
            Debug.Assert(_alias != SemanticKey.None, $"[AttributeContextLinker] Alias cannot be empty on {gameObject.name}");

            if (_receiver == null || _provider == null || _alias == SemanticKey.None) return;

            var receiverEntity = _receiver.Instance;

            // Re-linking replaces this component's previous registration. Registering over the same alias on
            // the same receiver swaps the provider directly, without a transient "missing provider".
            if (_linkedReceiver != null && (_linkedReceiver != receiverEntity || _linkedAlias != _alias))
            {
                RemoveLink();
            }

            // Register the provider's processor inside the receiver's processor
            _linkedReceiver = receiverEntity;
            _linkedProvider = _provider.Instance;
            _linkedAlias = _alias;
            _linkedReceiver.RegisterExternalProvider(_linkedAlias, _linkedProvider);

            Debug.Log($"[AttributeContextLinker] Successfully linked '{_provider.name}' to '{_receiver.name}' as '{_alias}'.");
        }

        /// <summary>
        /// Removes the link this component registered, unless something else has re-registered that alias since.
        /// </summary>
        public void RemoveLink()
        {
            if (_linkedReceiver == null) return;

            Entity current = null;
            _linkedReceiver.ObserveProvider(_linkedAlias).Take(1).Subscribe(provider => current = provider);
            if (current == _linkedProvider)
            {
                _linkedReceiver.UnregisterExternalProvider(_linkedAlias);
            }

            _linkedReceiver = null;
            _linkedProvider = null;
        }

        private void OnDestroy()
        {
            RemoveLink();
        }

        /// <summary>
        /// Sets the provider at runtime (e.g., when a player picks up this weapon).
        /// Passing null removes the link (e.g., when the weapon is dropped).
        /// </summary>
        public void SetProvider(EntityController provider)
        {
            _provider = provider;
            if (_provider != null) LinkContext();
            else RemoveLink();
        }

        public void SetReceiver(EntityController receiver)
        {
            RemoveLink();
            _receiver = receiver;
            if (_receiver != null && _provider != null)
                LinkContext();
        }

        /// <summary>
        /// Changes the alias. An existing link moves to the new alias.
        /// </summary>
        public void SetAlias(SemanticKey alias)
        {
            bool wasLinked = _linkedReceiver != null;
            RemoveLink();
            _alias = alias;
            if (wasLinked) LinkContext();
        }
    }
}