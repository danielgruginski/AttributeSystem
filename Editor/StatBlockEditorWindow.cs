using UnityEngine;
using UnityEditor;
using System.IO;
using System;
using System.Collections.Generic;
using ReactiveSolutions.AttributeSystem;

namespace ReactiveSolutions.AttributeSystem.Editor
{

    // Wrapper needed for SerializedObject to work on pure classes
    [Serializable]
    public class StatBlockWrapper : ScriptableObject
    {
        public StatBlock DataBlock = new StatBlock();
    }

    public class StatBlockEditorWindow : EditorWindow
    {
        private StatBlockWrapper _wrapper;
        private SerializedObject _serializedObject;
        private const string JSON_PATH = "Resources/Data/StatBlocks";
        private string _currentFilePath;
        private Vector2 _scroll;

        [MenuItem("Window/Attributes/Stat Block Editor (Unified)")]
        public static void ShowWindow() => GetWindow<StatBlockEditorWindow>("StatBlock Editor");

        private void OnEnable()
        {
            EnsureDirectory();
            if (_wrapper == null) CreateNewWrapper();
        }

        private void OnDisable()
        {
            if (_wrapper != null) DestroyImmediate(_wrapper);
        }

        private void OnGUI()
        {
            if (_wrapper == null || _serializedObject == null) CreateNewWrapper();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Unified StatBlock Editor", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("File:", _currentFilePath != null ? Path.GetFileName(_currentFilePath) : "Unsaved");
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("New")) CreateNewWrapper();
            if (GUILayout.Button("Load")) LoadJson();
            if (GUILayout.Button("Save")) SaveJson();
            EditorGUILayout.EndHorizontal();

            // --- NEW: BATCH FIXER ---
            GUI.backgroundColor = new Color(1f, 0.7f, 0.7f); // Reddish tint
            if (GUILayout.Button("Batch Fix All Legacy Files"))
            {
                if (EditorUtility.DisplayDialog("Batch Fix", "This will scan ALL json files in the folder. If 'OperationType' is missing, it will set it to 'Constant'. Proceed?", "Yes", "Cancel"))
                {
                    BatchSanitizeFiles();
                }
            }
            GUI.backgroundColor = Color.white;
            // ------------------------

            EditorGUILayout.Space();

            // DRAW THE INSPECTOR
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            _serializedObject.Update();

            SerializedProperty blockProp = _serializedObject.FindProperty("DataBlock");
            EditorGUILayout.PropertyField(blockProp, true); // True = Draw Children

            _serializedObject.ApplyModifiedProperties();
            EditorGUILayout.EndScrollView();
        }

        private void CreateNewWrapper()
        {
            if (_wrapper != null) DestroyImmediate(_wrapper);
            _wrapper = ScriptableObject.CreateInstance<StatBlockWrapper>();
            _wrapper.DataBlock.BlockName = "NewStatBlock";
            _serializedObject = new SerializedObject(_wrapper);
            _currentFilePath = null;
        }

        private void EnsureDirectory()
        {
            string path = Path.Combine(Application.dataPath, JSON_PATH);
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                AssetDatabase.Refresh();
            }
        }

        private void SaveJson()
        {
            // 1. Sanitize Data before saving
            SanitizeBlock(_wrapper.DataBlock);

            // 2. Prepare Path
            string path = Path.Combine(Application.dataPath, JSON_PATH);
            string fileName = _wrapper.DataBlock.BlockName.Replace(" ", "_") + ".json";
            string fullPath = Path.Combine(path, fileName);

            // 3. Serialize and Write
            string json = JsonUtility.ToJson(_wrapper.DataBlock, true);
            File.WriteAllText(fullPath, json);

            _currentFilePath = fullPath;
            AssetDatabase.Refresh();
            Debug.Log($"Saved (and sanitized) to {fileName}");
        }

        private void LoadJson()
        {
            string path = Path.Combine(Application.dataPath, JSON_PATH);
            string filePath = EditorUtility.OpenFilePanel("Load JSON", path, "json");

            if (string.IsNullOrEmpty(filePath)) return;

            try
            {
                string json = File.ReadAllText(filePath);
                StatBlock loaded = JsonUtility.FromJson<StatBlock>(json);

                if (loaded != null)
                {
                    CreateNewWrapper();
                    _wrapper.DataBlock = loaded;
                    _serializedObject = new SerializedObject(_wrapper); // Rebind
                    _currentFilePath = filePath;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Load failed: {e.Message}");
            }
        }

        // --- HELPER LOGIC ---

        /// <summary>
        /// Ensures valid defaults for legacy data (e.g. converting "" to "Constant")
        /// </summary>
        private void SanitizeBlock(StatBlock block)
        {
            if (block.Modifiers == null) return;

            // We use a for-loop to modify structs in the list
            for (int i = 0; i < block.Modifiers.Count; i++)
            {
                var mod = block.Modifiers[i];

                // If OperationType is empty, default to Constant
                if (string.IsNullOrEmpty(mod.OperationType))
                {
                    mod.OperationType = "Constant";

                    // Optional: Force a default param if none exist
                    if (mod.Params == null || mod.Params.Count == 0)
                    {
                        mod.Params = new List<ModifierParam>();
                        mod.Params.Add(new ModifierParam
                        {
                            Name = "Value",
                            Value = new ValueSourceSpec { Type = ValueSource.SourceType.Constant, ConstantValue = 0f }
                        });
                    }
                }

                block.Modifiers[i] = mod; // Write back struct
            }
        }

        private void BatchSanitizeFiles()
        {
            string path = Path.Combine(Application.dataPath, JSON_PATH);
            string[] files = Directory.GetFiles(path, "*.json");
            int fixedCount = 0;

            foreach (var file in files)
            {
                try
                {
                    string json = File.ReadAllText(file);
                    StatBlock block = JsonUtility.FromJson<StatBlock>(json);

                    if (block != null)
                    {
                        // Check if modification is needed
                        bool neededFix = false;
                        foreach (var mod in block.Modifiers)
                        {
                            if (string.IsNullOrEmpty(mod.OperationType)) { neededFix = true; break; }
                        }

                        if (neededFix)
                        {
                            SanitizeBlock(block);
                            string newJson = JsonUtility.ToJson(block, true);
                            File.WriteAllText(file, newJson);
                            fixedCount++;
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Batch fix error on {Path.GetFileName(file)}: {e.Message}");
                }
            }

            AssetDatabase.Refresh();
            Debug.Log($"Batch Complete. Fixed {fixedCount} files.");
        }
    }
}