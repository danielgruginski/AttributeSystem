using UnityEngine;
using System.Collections.Generic;
using System;

namespace ReactiveSolutions.AttributeSystem.Core.Data
{
    public static class StatBlockJsonLoader
    {
        /// <summary>
        /// Loads JSON data into an existing StatBlock instance.
        /// This wrapper allows us to add migration logic or custom parsing in the future if needed.
        /// </summary>
        public static void LoadIntoStatBlock(string json, StatBlock block)
        {
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogWarning("[StatBlockJsonLoader] JSON string is empty.");
                return;
            }

            // JsonUtility handles Lists of strings (Tags) and Lists of serializable classes (RemoteTags) automatically.
            JsonUtility.FromJsonOverwrite(json, block);

            // Note: If we had to handle version migration (e.g. renaming fields from old JSONs), 
            // we would check a "Version" field here and apply logic.
            // For now, the standard overwrite works for the new Tag fields.
        }
    }
}