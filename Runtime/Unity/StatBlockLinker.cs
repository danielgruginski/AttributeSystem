using System.Collections.Generic;
using ReactiveSolutions.AttributeSystem.Core;
using ReactiveSolutions.AttributeSystem.Core.Data;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Unity
{
    /// <summary>
    /// Links one or more StatBlocks (loaded from JSON by ID) to the AttributeProcessor on this object.
    /// This handles the lifecycle of the StatBlocks (Applying on Start, Disposing on Destroy).
    /// </summary>
    [RequireComponent(typeof(AttributeController))]
    public class StatBlockLinker : MonoBehaviour
    {
        [Tooltip("List of StatBlock IDs to apply (e.g. 'BaseStats', 'WarriorClass').")]
        [StatBlockID]
        public List<string> StatBlockIds = new List<string>();

        private AttributeController _controller;
        private List<ActiveStatBlock> _activeBlocks = new List<ActiveStatBlock>();

        // Cache the factory so we don't recreate it for every block
        private IModifierFactory _modifierFactory;

        private void Awake()
        {
            _controller = GetComponent<AttributeController>();
            _modifierFactory = new ModifierFactory();
        }

        private void Start()
        {
            ApplyStatBlocks();
        }

        private void OnDestroy()
        {
            ClearStatBlocks();
        }

        /// <summary>
        /// Clears current blocks and reapplies everything in the StatBlockIds list.
        /// Useful if the ID list changes at runtime.
        /// </summary>
        public void ApplyStatBlocks()
        {
            ClearStatBlocks();

            if (_controller == null || _controller.Processor == null)
            {
                Debug.LogWarning("[StatBlockLinker] No AttributeController/Processor found.");
                return;
            }

            foreach (var id in StatBlockIds)
            {
                if (string.IsNullOrEmpty(id)) continue;

                StatBlock block = new StatBlock();
                StatBlockJsonLoader.LoadIntoStatBlock(id, block );
                if (block != null)
                {
                    // Apply the block and store the handle
                    var activeHandle = block.ApplyToProcessor(_controller.Processor, _modifierFactory);
                    _activeBlocks.Add(activeHandle);
                }
                else
                {
                    Debug.LogWarning($"[StatBlockLinker] Could not load StatBlock with ID: {id}");
                }
            }
        }

        /// <summary>
        /// Removes all currently applied stat blocks.
        /// </summary>
        public void ClearStatBlocks()
        {
            foreach (var handle in _activeBlocks)
            {
                handle?.Dispose();
            }
            _activeBlocks.Clear();
        }
    }
}