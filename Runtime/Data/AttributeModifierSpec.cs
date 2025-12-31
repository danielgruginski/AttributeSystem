using ReactiveSolutions.AttributeSystem.Core.Modifiers;
using SemanticKeys;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Core.Data
{
    [Serializable]
    public class AttributeModifierSpec
    {
        [Header("Target")]
        public SemanticKey TargetAttribute;
        public List<SemanticKey> TargetPath = new List<SemanticKey>();
        public string SourceId;

        [Header("Pipeline")]
        public ModifierType Type = ModifierType.Additive;
        public int Priority = 0;

        [Header("Logic")]
        //[SemanticKeyFilter("Modifiers")]
        public SemanticKey LogicType;

        [Header("Unified Arguments")]
        [Tooltip("Define all inputs here. Use Mode=Constant for static values, Mode=Attribute for dynamic ones.")]
        public List<ValueSource> Arguments = new List<ValueSource>();
    }
}