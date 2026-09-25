using ReactiveSolutions.AttributeSystem.Core.Modifiers;
using SemanticKeys;
using System;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Core.Data
{
    /// <summary>
    /// A pure data class representing an entity's statistics.
    /// Because this is a plain class (POCO), it is garbage collected automatically
    /// and perfect for deserializing from JSON at runtime.
    /// </summary>
    [System.Serializable]
    public class StatBlock : ISerializationCallbackReceiver
    {
        [System.Serializable]
        public struct BaseValueEntry
        {
            public SemanticKey Name;
            public float Value;
        }

        [System.Serializable]
        public struct PointerSpec
        {
            [Tooltip("The alias name (e.g. 'MainStat').")]
            public SemanticKey Alias;

            [Tooltip("The target to point to.")]
            public AttributeReference Target;
        }


        public string BlockName = "New Block"; // Helper for editor naming

        [Tooltip("Conditions for this block to be active.")]
        public StatBlockCondition ActivationCondition;

        // Simple local tags (e.g. "Undead")
        public List<SemanticKey> Tags = new List<SemanticKey>();

        // Remote tags (e.g. Apply "Blessed" to "Owner")
        public List<TagModifierSpec> RemoteTags = new List<TagModifierSpec>();

        // Pointers (Aliases)
        public List<PointerSpec> Pointers = new List<PointerSpec>();

        public List<BaseValueEntry> BaseValues = new List<BaseValueEntry>();
        public List<AttributeModifierSpec> Modifiers = new List<AttributeModifierSpec>();

        public void OnBeforeSerialize() { }

        // A modifier duplicated in the Inspector can share its logic object with the original; give it its own.
        public void OnAfterDeserialize() => ModifierLogic.Unshare(Modifiers);

        /// <summary>
        /// Populates a processor and returns an ActiveStatBlock handle to manage the lifecycle of applied modifiers.
        /// Applying never modifies the block, so one block can be applied to any number of entities.
        /// </summary>
        public ActiveStatBlock ApplyToEntity(Entity entity)
        {
            var activeBlockHandle = new ActiveStatBlock();

            // 1. Set Base Values (Permanent for the session, generally not reverted by ActiveStatBlock)
            // We apply these regardless of condition, as they are "Base Stats" usually defined by the object existence itself.
            // If dynamic base stats are needed, they should be Modifiers (Override type).
            foreach (var entry in BaseValues)
            {
                if (entry.Name != SemanticKey.None)
                {
                    entity.SetOrUpdateBaseValue(entry.Name, entry.Value);
                }
            }

            // 2. Observe Condition
            // We create a disposable subscription that monitors the condition.
            // Inside, we manage the "Inner" ActiveStatBlock that actually holds the modifiers.

            var innerHandleSerial = new SerialDisposable();
            activeBlockHandle.AddHandle(innerHandleSerial);

            // Default to 'Always' if null
            var conditionStream = ActivationCondition != null
                ? ConditionEvaluator.Observe(ActivationCondition, entity)
                : Observable.Return(true);

            // If applying or removing the content flips the condition itself (e.g. "while Health < 50: +100 Health"),
            // there is no stable state; toggling would recurse until the stack overflows. Detect it and disable the block.
            bool isTransitioning = false;
            bool isFaulted = false;

            var subscription = conditionStream
                .DistinctUntilChanged()
                .Subscribe(isActive =>
                {
                    if (isFaulted) return;

                    if (isTransitioning)
                    {
                        isFaulted = true;
                        Debug.LogError($"[StatBlock] '{BlockName}' was disabled: its activation condition depends on its own effects " +
                                       "(applying or removing the block flips the condition).");
                        return;
                    }

                    isTransitioning = true;
                    try
                    {
                        // ACTIVATE: Apply everything and store the receipt in the SerialDisposable.
                        // DEACTIVATE: Dispose the inner content.
                        innerHandleSerial.Disposable = isActive ? ApplyContent(entity) : null;
                    }
                    finally
                    {
                        isTransitioning = false;
                    }

                    if (isFaulted) innerHandleSerial.Disposable = null;
                });

            activeBlockHandle.AddHandle(subscription);

            return activeBlockHandle;
        }

        /// <summary>
        /// Helper to apply the actual modifiers/tags. Returns a disposable handle for them.
        /// </summary>
        private IDisposable ApplyContent(Entity processor)
        {
            var contentHandle = new ActiveStatBlock();

            // 1. Apply Pointers (Structural / Reversible)
            // We apply these first so modifiers in this same block can target the alias if needed.
            foreach (var ptr in Pointers)
            {
                if (ptr.Alias != SemanticKey.None && ptr.Target.Name != SemanticKey.None)
                {
                    var ptrHandle = processor.SetPointer(ptr.Alias, ptr.Target.Name, ptr.Target.Path);
                    contentHandle.AddHandle(ptrHandle);
                }
            }

            // 2. Apply Local Tags
            foreach (var tagKey in Tags)
            {
                if (tagKey != SemanticKey.None)
                {
                    processor.AddTag(tagKey);
                    contentHandle.AddHandle(Disposable.Create(() => processor.RemoveTag(tagKey)));
                }
            }

            // 3. Apply Remote Tags
            foreach (var tagSpec in RemoteTags)
            {
                if (tagSpec.Tag != SemanticKey.None)
                {
                    var conn = new TagConnection(processor, tagSpec.TargetPath, tagSpec.Tag);
                    contentHandle.AddHandle(conn);
                }
            }

            // 4. Apply Modifiers
            foreach (var spec in Modifiers)
            {
                if (spec == null) continue;

                if (spec.TargetAttribute == SemanticKey.None)
                {
                    Debug.LogWarning($"[StatBlock] '{BlockName}': skipped a '{ModifierLogic.GetDisplayName(spec.Logic?.GetType())}' modifier with no Target Attribute.");
                    continue;
                }

                if (spec.Logic == null)
                {
                    Debug.LogWarning($"[StatBlock] '{BlockName}': skipped a modifier on '{spec.TargetAttribute}' with no Logic.");
                    continue;
                }

                // The modifier's inputs resolve relative to this entity, even when TargetPath points elsewhere.
                var modifier = spec.CreateModifier(processor);
                contentHandle.AddHandle(processor.AddModifier(spec.SourceId, modifier, spec.TargetAttribute, spec.TargetPath));
            }

            return contentHandle;
        }
    }
}