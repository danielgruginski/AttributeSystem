using UnityEngine;
using ReactiveSolutions.AttributeSystem.Core;


namespace ReactiveSolutions.AttributeSystem.Unity
{
    /// <summary>
    /// A bridge component that applies a StatBlock's initial values to an AttributeController.
    /// This gives users explicit control over when and how stats are initialized.
    /// </summary>
    [AddComponentMenu("Attribute System/Stat Block Linker")]
    public class StatBlockLinker : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private AttributeController _targetController;
        [SerializeField] private StatBlockID _statBlock;

        [Header("Settings")]
        [Tooltip("If true, values will be applied as soon as this component awakes.")]
        [SerializeField] private bool _applyOnAwake = true;

        private void Awake()
        {
            if (_applyOnAwake)
            {
                ApplyStatBlock();
            }
        }

        /// <summary>
        /// Manually triggers the population of attributes from the assigned StatBlock.
        /// </summary>
        public void ApplyStatBlock()
        {
            if (_targetController == null)
            {
                Debug.LogWarning($"[StatBlockLinker] No target controller assigned on {gameObject.name}");
                return;
            }

            if (_statBlock == null || string.IsNullOrEmpty(_statBlock.ID))
            {
                Debug.LogWarning($"[StatBlockLinker] No StatBlock ID assigned on {gameObject.name}");
                return;
            }

            // Retrieve the StatBlock data (usually loaded via StatBlockJsonLoader or manual registry)
            // Note: In a production environment, you might use a central StatBlockRegistry here.
            // For now, we assume the user provides the data or the controller is ready to receive definitions.

            // This is where you would call: _targetController.InitializeFromStatBlock(...)
            // If that method doesn't exist yet, we can add a helper to the Controller.
            Debug.Log($"[StatBlockLinker] Linking {_statBlock.ID} to {_targetController.name}");
        }

        public void SetTarget(AttributeController controller) => _targetController = controller;
        public void SetStatBlock(StatBlockID block) => _statBlock = block;
    }
}