using UnityEngine;
using System.Collections.Generic;
using System;

namespace ReactiveSolutions.AttributeSystem.Core.Data
{
    public static class StatBlockJsonLoader
    {
        // Path relative to a Resources folder. 
        // Example: If file is at Assets/Resources/Data/StatBlocks/Block.json, 
        // Resources.Load expects "Data/StatBlocks/Block"
        private const string RESOURCE_SUBPATH = "Data/StatBlocks/";

        public static StatBlock Load(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            var block = new StatBlock();
            LoadIntoStatBlock(id, block);
            return block;
        }

        public static void LoadIntoStatBlock(string id, StatBlock block)
        {
            // 1. Sanitize the ID to be a valid Resources path
            // Remove .json extension if present
            string cleanId = id;
            if (cleanId.EndsWith(".json"))
            {
                cleanId = cleanId.Substring(0, cleanId.Length - 5);
            }

            // Remove the hardcoded path prefix if the ID already contains it (avoid duplication)
            // Some users might pass "Data/StatBlocks/MyID", others just "MyID"
            if (cleanId.StartsWith(RESOURCE_SUBPATH))
            {
                cleanId = cleanId.Substring(RESOURCE_SUBPATH.Length);
            }

            string resourcePath = RESOURCE_SUBPATH + cleanId;

            // 2. Load from Resources
            var textAsset = Resources.Load<TextAsset>(resourcePath);

            if (textAsset != null)
            {
                try
                {
                    JsonUtility.FromJsonOverwrite(textAsset.text, block);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[StatBlockJsonLoader] Failed to parse JSON for {id}: {e.Message}");
                }
            }
            else
            {
                // Fallback: Check if the user provided a full raw ID without the path prefix
                // This helps if the ID itself IS the path (though less standard for this loader)
                Debug.LogError($"[StatBlockJsonLoader] Could not load StatBlock with ID: {id} (Attempted path: {resourcePath})");
            }
        }
    }
}