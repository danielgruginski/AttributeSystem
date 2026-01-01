using UnityEngine;
using System.Collections.Generic;
using System;

namespace ReactiveSolutions.AttributeSystem.Core.Data
{
    /// <summary>
    /// Utility to bridge raw JSON strings into the StatBlock data structure.
    /// Uses a wrapper class to comply with Unity's JsonUtility limitations.
    /// </summary>
    public static class StatBlockJsonLoader
    {
        [Serializable]
        private class StatBlockWrapper
        {
            public List<StatBlock.BaseValueEntry> BaseValues;
            public List<AttributeModifierSpec> Modifiers;
        }

        public static void LoadIntoStatBlock(string json, StatBlock target)
        {
            try
            {
                var data = JsonUtility.FromJson<StatBlockWrapper>(json);

                if (data == null)
                {
                    Debug.LogError("[StatBlockLoader] Failed to parse JSON. Data is null.");
                    return;
                }

                target.BaseValues = data.BaseValues ?? new List<StatBlock.BaseValueEntry>();
                target.Modifiers = data.Modifiers ?? new List<AttributeModifierSpec>();
            }
            catch (Exception e)
            {
                Debug.LogError($"[StatBlockLoader] JSON Exception: {e.Message}");
            }
        }

        /// <summary>
        /// Example JSON structure expected:
        /// {
        ///   "BaseValues": [ { "Name": "Health", "Value": 100 } ],
        ///   "Modifiers": [ 
        ///     { 
        ///       "TargetAttribute": "Damage", 
        ///       "SourceId": "Weapon_01",
        ///       "Category": 0, 
        ///       "SourceMode": 1, 
        ///       "AttributePath": "Owner.Strength", 
        ///       "Coeff": 1.5 
        ///     } 
        ///   ]
        /// }
        /// </summary>
        public static string GetSchemaHint() => "Check the internal StatBlockWrapper for the expected JSON structure.";
    }
}