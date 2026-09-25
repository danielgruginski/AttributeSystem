using ReactiveSolutions.AttributeSystem.Core.Modifiers;
using SemanticKeys;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Core
{
    /// <summary>
    /// A modifier as data (in a StatBlock): the attribute it targets, how and when its value is applied
    /// (Type and Priority), and the logic that computes the value.
    /// </summary>
    [Serializable]
    public class AttributeModifierSpec
    {
        public SemanticKey TargetAttribute;
        [Tooltip("Provider path to the entity whose attribute is modified. Leave empty for the entity the StatBlock is applied to.")]
        public List<SemanticKey> TargetPath = new List<SemanticKey>();
        [Tooltip("Names the modifier's source in logs and in the Attribute Debugger.")]
        public string SourceId;

        [Tooltip("What the value does to the attribute: add to it, multiply it, replace it, or keep it at least (Clamp Min) or at most (Clamp Max) the value.")]
        public ModifierType Type = ModifierType.Additive;
        [Tooltip("Lower priorities apply first. Within a priority: Additive, Multiplicative, Override, Clamp Min, Clamp Max, then the order they were added.")]
        public int Priority = 0;

        [Tooltip("Computes the value.")]
        [SerializeReference]
        public ModifierLogic Logic = new ValueLogic();

        /// <summary>
        /// Creates the modifier for one application of this spec. The logic's inputs resolve relative to
        /// <paramref name="context"/> (the entity the StatBlock is applied to), even when TargetPath points to
        /// another entity. The spec and its logic are shared data and are never modified.
        /// </summary>
        public IAttributeModifier CreateModifier(Entity context)
            => new LogicModifier(Logic, Type, Priority, SourceId, context);
    }
}