using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace algumacoisaqq.AttributeSystem
{
    /// <summary>
    /// Static utility to load all StatBlocks from JSON files in the Resources folder.
    /// </summary>
    public static class StatBlockJsonLoader
    {
        private const string JSON_PATH = "Data/StatBlocks";

        private static readonly Dictionary<string, StatBlock> _loadedBlocks = new();
        private static bool _isLoaded = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void LoadAllStatBlocks()
        {
            if (_isLoaded) return;
            _loadedBlocks.Clear();

            TextAsset[] jsonFiles = Resources.LoadAll<TextAsset>(JSON_PATH);

            if (jsonFiles.Length == 0)
            {
                Debug.LogWarning($"StatBlockLoader found no JSON files in Resources/{JSON_PATH}.");
                return;
            }

            foreach (var jsonAsset in jsonFiles)
            {
                try
                {
                    StatBlock block = JsonUtility.FromJson<StatBlock>(jsonAsset.text);
                    string blockId = jsonAsset.name;

                    if (block == null)
                    {
                        Debug.LogError($"Failed to deserialize '{blockId}'.");
                        continue;
                    }

                    // --- DIAGNOSTIC CHECKS & SANITIZATION ---

                    // 1. Check for Overwrites (Duplicate Files)
                    if (_loadedBlocks.ContainsKey(blockId))
                    {
                        string pathMsg = "";
#if UNITY_EDITOR
                        pathMsg = $" (Path: {AssetDatabase.GetAssetPath(jsonAsset)})";
#endif
                        Debug.LogError($"[StatBlockLoader] DUPLICATE DETECTED: A StatBlock named '{blockId}' was already loaded! This file is overwriting the previous one.{pathMsg}");
                    }

                    bool hasFixedData = false;
                    // 2. Validate & Fix Modifiers
                    // We use a FOR loop because Modifiers are Structs. 
                    // Modifying 'mod' in a foreach loop would not change the data in the list.
                    for (int i = 0; i < block.Modifiers.Count; i++)
                    {
                        var mod = block.Modifiers[i];

                        if (string.IsNullOrEmpty(mod.OperationType))
                        {
                            // AUTO-FIX: Default to Constant so the game doesn't break
                            mod.OperationType = "Constant";
                            block.Modifiers[i] = mod; // Write struct back to list
                            hasFixedData = true;

                            // Log Warning (Instead of Error) with RAW JSON check
                            string fullPath = "Unknown (Build)";
#if UNITY_EDITOR
                            fullPath = AssetDatabase.GetAssetPath(jsonAsset);
#endif

                            Debug.LogWarning($"[StatBlockLoader] JSON MISSING DATA in '{blockId}' -> Attribute '{mod.AttributeName}'.\n" +
                                           $"Missing 'OperationType'. Auto-fixing to 'Constant'.\n" +
                                           $"File: {fullPath}\n" +
                                           $"Raw JSON Content Seen by Unity: {jsonAsset.text}");
                            // ^ Check this log! If 'OperationType' is missing here, Unity isn't seeing your file edits.
                        }
                    }

                    if (hasFixedData)
                    {
                        Debug.Log($"[StatBlockLoader] Auto-corrected legacy/broken data in '{blockId}'.");
                    }

                    block.BlockName = blockId;
                    _loadedBlocks[blockId] = block;
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to load {jsonAsset.name}: {e.Message}");
                }
            }
            _isLoaded = true;
            Debug.Log($"[StatBlockLoader] Loaded {_loadedBlocks.Count} stat blocks.");
        }

        public static StatBlock GetStatBlock(string blockName)
        {
            if (!_isLoaded || _loadedBlocks.Count == 0)
            {
                Debug.LogWarning($"[StatBlockLoader] Lazy loading triggered for '{blockName}'.");
                LoadAllStatBlocks();
            }

            if (_loadedBlocks.TryGetValue(blockName, out var block))
            {
                return block;
            }

            Debug.LogError($"StatBlock '{blockName}' not found. Check Resources/{JSON_PATH}.");
            return null;
        }
    }
}