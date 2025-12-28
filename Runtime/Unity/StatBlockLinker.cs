using ReactiveSolutions.AttributeSystem.Core;
using ReactiveSolutions.AttributeSystem.Core.Data;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Unity
{
    /// <summary>
    /// A bridge component that applies a StatBlock's initial values to an AttributeController.
    /// Loads the JSON definition from Resources and applies it at runtime.
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

        // The dependency. In a pure VContainer setup, you would add [Inject] here.
        // For library portability, we use a property that lazy-loads a default if not injected.
        private IModifierFactory _modifierFactory;

        /// <summary>
        /// Allows external systems (DI Container, Bootstrap) to inject the factory.
        /// </summary>
        public void Construct(IModifierFactory factory)
        {
            _modifierFactory = factory;
        }

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

            if (string.IsNullOrEmpty(_statBlock.ID))
            {
                Debug.LogWarning($"[StatBlockLinker] No StatBlock ID assigned on {gameObject.name}");
                return;
            }

            // Ensure we have a factory.
            if (_modifierFactory == null)
            {
                // Fallback: Create a default local factory if none was injected.
                // In a production game using VContainer, this branch should ideally never be hit 
                // if the SceneScope is set up correctly, but it prevents crashes for level designers.
                _modifierFactory = new ModifierFactory();
            }

            // 1. Construct the Resource path
            // Note: Resources.Load paths must not include the extension or "Resources/" prefix
            string resourcePath = $"Data/StatBlocks/{_statBlock.ID}";
            TextAsset jsonFile = Resources.Load<TextAsset>(resourcePath);

            if (jsonFile == null)
            {
                Debug.LogError($"[StatBlockLinker] Could not find JSON file at: Resources/{resourcePath}.json");
                return;
            }

            // 2. Create a temporary ScriptableObject to hold the data
            // We use CreateInstance so we can use the StatBlock's own ApplyToProcessor method logic
            StatBlock tempBlock = new StatBlock();

            // 3. Load Data
            StatBlockJsonLoader.LoadIntoStatBlock(jsonFile.text, tempBlock);

            // 4. Apply to the Processor
            tempBlock.ApplyToProcessor(_targetController.Processor, _modifierFactory);

            Debug.Log($"[StatBlockLinker] Successfully applied '{_statBlock.ID}' to '{_targetController.name}'");
        }

        public void SetTarget(AttributeController controller) => _targetController = controller;
        public void SetStatBlock(StatBlockID block) => _statBlock = block;
    }
}