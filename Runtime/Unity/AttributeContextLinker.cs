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
        [SerializeField] private AttributeController _receiver;

        [Tooltip("The controller that will PROVIDE the stats (e.g., the Player).")]
        [SerializeField] private AttributeController _provider;

        [Header("Configuration")]
        [Tooltip("The key used in dot notation (e.g., if Alias is 'Owner', use 'Owner.Strength').")]
        [SerializeField] private string _alias = "Owner";

        [Tooltip("If true, the link is established during Awake.")]
        [SerializeField] private bool _linkOnAwake = true;

        private void Awake()
        {
            if (_linkOnAwake)
            {
                LinkContext();
            }
        }

        /// <summary>
        /// Establishes the relationship between the two processors.
        /// </summary>
        public void LinkContext()
        {
            // Fallback: If receiver is null, try to find it on this GameObject
            if (_receiver == null)
            {
                _receiver = GetComponent<AttributeController>();
            }

            Debug.Assert(_receiver != null, $"[AttributeContextLinker] No Receiver (Target) Controller assigned on {gameObject.name}");
            Debug.Assert(_provider != null, $"[AttributeContextLinker] No Provider (Source) Controller assigned for Alias '{_alias}' on {gameObject.name}");
            Debug.Assert(!string.IsNullOrEmpty(_alias), $"[AttributeContextLinker] Alias cannot be empty on {gameObject.name}");

            if (_receiver != null && _provider != null)
            {
                // Register the provider's processor inside the receiver's processor
                _receiver.Processor.RegisterExternalProvider(_alias, _provider.Processor);

                Debug.Log($"[AttributeContextLinker] Successfully linked '{_provider.name}' to '{_receiver.name}' as '{_alias}'.");
            }
        }

        /// <summary>
        /// Sets the provider at runtime (e.g., when a player picks up this weapon).
        /// </summary>
        public void SetProvider(AttributeController provider)
        {
            _provider = provider;
            LinkContext();
        }

        public void SetReceiver(AttributeController receiver) => _receiver = receiver;
        public void SetAlias(string alias) => _alias = alias;
    }
}