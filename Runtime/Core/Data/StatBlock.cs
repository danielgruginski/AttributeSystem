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
    public class StatBlock
    {
        [System.Serializable]
        public struct BaseValueEntry
        {
            public SemanticKey Name;
            public float Value;
        }



        public string BlockName = "New Block"; // Helper for editor naming

        [Tooltip("Conditions for this block to be active.")]
        public StatBlockCondition ActivationCondition;

        // Simple local tags (e.g. "Undead")
        public List<SemanticKey> Tags = new List<SemanticKey>();

        // Remote tags (e.g. Apply "Blessed" to "Owner")
        public List<TagModifierSpec> RemoteTags = new List<TagModifierSpec>();

        public List<BaseValueEntry> BaseValues = new List<BaseValueEntry>();
        public List<AttributeModifierSpec> Modifiers = new List<AttributeModifierSpec>();

        /// <summary>
        /// Populates a processor and returns an ActiveStatBlock handle to manage the lifecycle of applied modifiers.
        /// </summary>
        public ActiveStatBlock ApplyToProcessor(AttributeProcessor processor, IModifierFactory factory)
        {
            factory ??= new ModifierFactory();
            var activeBlockHandle = new ActiveStatBlock();

            // 1. Set Base Values (Permanent for the session, generally not reverted by ActiveStatBlock)
            // We apply these regardless of condition, as they are "Base Stats" usually defined by the object existence itself.
            // If dynamic base stats are needed, they should be Modifiers (Override type).
            foreach (var entry in BaseValues)
            {
                if (!string.IsNullOrEmpty(entry.Name))
                {
                    processor.SetOrUpdateBaseValue(entry.Name, entry.Value);
                }
            }

            // 2. Observe Condition
            // We create a disposable subscription that monitors the condition.
            // Inside, we manage the "Inner" ActiveStatBlock that actually holds the modifiers.

            var innerHandleSerial = new SerialDisposable();
            activeBlockHandle.AddHandle(innerHandleSerial);

            // Default to 'Always' if null
            var conditionStream = ActivationCondition != null
                ? ConditionEvaluator.Observe(ActivationCondition, processor)
                : Observable.Return(true);

            var subscription = conditionStream
                .DistinctUntilChanged()
                .Subscribe(isActive =>
                {
                    if (isActive)
                    {
                        // ACTIVATE: Apply everything and store the receipt in the SerialDisposable
                        // This automatically disposes any previous receipt if it existed (though Distinct prevents thrashing)
                        innerHandleSerial.Disposable = ApplyContent(processor, factory);
                    }
                    else
                    {
                        // DEACTIVATE: Dispose the inner content
                        innerHandleSerial.Disposable = null;
                    }
                });

            activeBlockHandle.AddHandle(subscription);

            return activeBlockHandle;
        }

        /// <summary>
        /// Helper to apply the actual modifiers/tags. Returns a disposable handle for them.
        /// </summary>
        private IDisposable ApplyContent(AttributeProcessor processor, IModifierFactory factory)
        {
            var contentHandle = new ActiveStatBlock();

            // 1. Apply Local Tags
            foreach (var tagKey in Tags)
            {
                if (tagKey != SemanticKey.None)
                {
                    processor.AddTag(tagKey);
                    contentHandle.AddHandle(Disposable.Create(() => processor.RemoveTag(tagKey)));
                }
            }

            // 2. Apply Remote Tags
            foreach (var tagSpec in RemoteTags)
            {
                if (tagSpec.Tag != SemanticKey.None)
                {
                    var conn = new TagConnection(processor, tagSpec.TargetPath, tagSpec.Tag);
                    contentHandle.AddHandle(conn);
                }
            }

            // 3. Apply Modifiers
            foreach (var spec in Modifiers)
            {
                var modifier = factory.Create(spec, processor);
                if (modifier != null)
                {
                    var handle = processor.AddModifier(spec.SourceId, modifier, spec.TargetAttribute, spec.TargetPath);
                    contentHandle.AddHandle(handle);
                }
                else
                {
                    Debug.LogWarning($"StatBlock.ApplyToProcessor: Could not create modifier of type '{spec.LogicType}'");
                }
            }

            return contentHandle;
        }
    }
}