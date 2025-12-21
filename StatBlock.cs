using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace algumacoisaqq.AttributeSystem
{
    [Serializable]
    public class StatBlock
    {
        public string BlockName;
        public List<AttributeModifierSpec> Modifiers = new List<AttributeModifierSpec>();
    }

    [Serializable]
    public struct AttributeModifierSpec
    {
        public string AttributeName;
        public int Priority;

        [Tooltip("The ID of the operation (e.g., 'Linear', 'Ratio', 'Exponential'). Case-insensitive.")]
        public string OperationType;

        public AttributeMergeMode MergeMode;

        [Tooltip("Dynamic list of inputs required by the selected Operation.")]
        public List<ModifierParam> Params;

        public IAttributeModifier ToModifier(string sourceId)
        {
            // Construct useful context info for debugging
            string debugContext = $"SourceID: '{sourceId}' | Attribute: '{AttributeName}'";

            // 1. Get Factory (Passing context so we know which file failed)
            var factory = ModifierFactoryRegistry.Get(OperationType, debugContext);

            // 2. Prepare Dictionary for O(1) lookup
            var paramDict = new Dictionary<string, ValueSource>();

            if (Params != null)
            {
                foreach (var p in Params)
                {
                    string key = ModifierFactoryRegistry.NormalizeKey(p.Name);
                    paramDict[key] = p.Value.ToValueSource();
                }
            }

            // 3. Build Logic
            return factory.Create(sourceId, Priority, MergeMode, paramDict);
        }

        public IEnumerable<string> GetDependencyNames()
        {
            if (Params == null) yield break;

            foreach (var p in Params)
            {
                if (p.Value.Type == ValueSource.SourceType.Attribute && !string.IsNullOrEmpty(p.Value.AttributeName))
                {
                    yield return p.Value.AttributeName;
                }
            }
        }
    }

    [Serializable]
    public struct ModifierParam
    {
        public string Name;
        public ValueSourceSpec Value;
    }

    [Serializable]
    public struct ValueSourceSpec
    {
        public ValueSource.SourceType Type;
        public float ConstantValue;
        public string AttributeName;

        public ValueSource ToValueSource() => new ValueSource
        {
            Type = Type,
            ConstantValue = ConstantValue,
            AttributeName = AttributeName
        };
    }
}

    
