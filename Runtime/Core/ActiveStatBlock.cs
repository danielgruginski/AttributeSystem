using System;
using System.Collections.Generic;
using SemanticKeys;

namespace ReactiveSolutions.AttributeSystem.Core
{
    /// <summary>
    /// Represents a "Live" instance of a StatBlock applied to a processor.
    /// Keeps track of the specific modifier instances created so they can be removed cleanly.
    /// </summary>
    public class ActiveStatBlock : IDisposable
    {
        private readonly AttributeProcessor _targetProcessor;
        private readonly List<AppliedModifierInfo> _appliedModifiers = new();

        public ActiveStatBlock(AttributeProcessor targetProcessor)
        {
            _targetProcessor = targetProcessor;
        }

        /// <summary>
        /// Registers a modifier that was just added to the processor.
        /// </summary>
        public void Track(IAttributeModifier modifier, SemanticKey targetAttribute, List<SemanticKey> path)
        {
            _appliedModifiers.Add(new AppliedModifierInfo
            {
                Modifier = modifier,
                Target = targetAttribute,
                Path = path
            });
        }

        /// <summary>
        /// Removes all modifiers tracked by this instance from the processor.
        /// </summary>
        public void Dispose()
        {
            foreach (var info in _appliedModifiers)
            {
                // We remove by INSTANCE, so even if 10 modifiers have SourceID="Sword",
                // only THIS specific one is removed.
                _targetProcessor.RemoveModifier(info.Modifier, info.Target, info.Path);
            }
            _appliedModifiers.Clear();
        }

        private struct AppliedModifierInfo
        {
            public IAttributeModifier Modifier;
            public SemanticKey Target;
            public List<SemanticKey> Path;
        }
    }
}