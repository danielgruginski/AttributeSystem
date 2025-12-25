using UnityEngine;
using UnityEditor;
using System.IO;
using System;
using System.Reflection;
using ReactiveSolutions.AttributeSystem.Core.Data;

namespace ReactiveSolutions.AttributeSystem.Editor
{
    public class StatBlockEditorWindow : EditorWindow
    {
        // -----------------------------------------------------------
        // Helper Container: Bridges POCO StatBlock -> Unity Inspector
        // -----------------------------------------------------------
        public class StatBlockContainer : ScriptableObject
        {
            public StatBlock Data = new StatBlock();
        }
        // -----------------------------------------------------------

        private StatBlockContainer _container; // Replaces _currentBlock
        private SerializedObject _serializedObject;
        private const string JSON_PATH = "Resources/Data/StatBlocks";

        private string _currentFileName = "NewStatBlock";
        private string _fullFilePath;
        private Vector2 _scroll;

        [MenuItem("Window/Attributes/Stat Block Editor (Unified)")]
        public static void ShowWindow() => GetWindow<StatBlockEditorWindow>("StatBlock Editor");

        private void OnEnable()
        {
            EnsureDirectory();
            if (_container == null) CreateNewContainer();
        }

        private void OnDisable()
        {
            if (_container != null) DestroyImmediate(_container);
        }

        private void OnGUI()
        {
            if (_container == null || _serializedObject == null || _serializedObject.targetObject == null)
            {
                CreateNewContainer();
            }

            DrawHeader();

            EditorGUILayout.Space();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            _serializedObject.Update();

            // Access the 'Data' field of our container
            SerializedProperty dataProp = _serializedObject.FindProperty("Data");

            // Draw the children of 'Data' (BaseValues, Modifiers) directly
            // This skips drawing the "Data" foldout itself for a cleaner look
            SerializedProperty iterator = dataProp.Copy();
            SerializedProperty endProperty = iterator.GetEndProperty();

            // Enter the 'Data' object
            if (iterator.NextVisible(true))
            {
                do
                {
                    if (SerializedProperty.EqualContents(iterator, endProperty)) break;
                    EditorGUILayout.PropertyField(iterator, true);
                }
                while (iterator.NextVisible(false)); // Don't enter children of lists automatically
            }

            _serializedObject.ApplyModifiedProperties();
            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Unified StatBlock Editor", EditorStyles.boldLabel);

            // Filename editing
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Filename (No ext):", GUILayout.Width(110));
            _currentFileName = EditorGUILayout.TextField(_currentFileName);
            EditorGUILayout.LabelField(".json", GUILayout.Width(40));
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_fullFilePath))
            {
                EditorGUILayout.HelpBox($"Editing: {_fullFilePath}", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("Unsaved New Block", MessageType.Warning);
            }

            EditorGUILayout.EndVertical();

            // Toolbar
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("New")) CreateNewContainer();
            if (GUILayout.Button("Load")) LoadJson();
            if (GUILayout.Button("Save")) SaveJson();
            EditorGUILayout.EndHorizontal();
        }

        private void CreateNewContainer()
        {
            if (_container != null) DestroyImmediate(_container);
            _container = ScriptableObject.CreateInstance<StatBlockContainer>();
            _serializedObject = new SerializedObject(_container);

            _currentFileName = "NewStatBlock";
            _fullFilePath = null;
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
            if (string.IsNullOrEmpty(_currentFileName))
            {
                EditorUtility.DisplayDialog("Error", "Please enter a filename.", "OK");
                return;
            }

            string path = Path.Combine(Application.dataPath, JSON_PATH);
            string fileName = _currentFileName.Replace(" ", "_") + ".json";
            string fullPath = Path.Combine(path, fileName);

            // Serialize the POCO directly
            string json = JsonUtility.ToJson(_container.Data, true);
            File.WriteAllText(fullPath, json);

            AssetDatabase.Refresh();
            ClearDrawerCaches();
            _fullFilePath = fullPath;
            Debug.Log($"Saved StatBlock to {fileName}");
        }

        private void LoadJson()
        {
            string path = Path.Combine(Application.dataPath, JSON_PATH);
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);

            string filePath = EditorUtility.OpenFilePanel("Load JSON", path, "json");
            if (string.IsNullOrEmpty(filePath)) return;

            try
            {
                string json = File.ReadAllText(filePath);

                CreateNewContainer();
                // Overwrite the POCO inside the container
                JsonUtility.FromJsonOverwrite(json, _container.Data);
                _serializedObject.Update();

                _fullFilePath = filePath;
                _currentFileName = Path.GetFileNameWithoutExtension(filePath);
            }
            catch (Exception e)
            {
                Debug.LogError($"Load failed: {e.Message}");
            }
        }

        private void ClearDrawerCaches()
        {
            var idDrawerType = Type.GetType("ReactiveSolutions.AttributeSystem.Editor.StatBlockIDDrawer");
            if (idDrawerType != null)
            {
                var field = idDrawerType.GetField("_availableOptions", BindingFlags.Static | BindingFlags.NonPublic);
                if (field != null) field.SetValue(null, null);
            }

            var listDrawerType = Type.GetType("ReactiveSolutions.AttributeSystem.Editor.StatBlockIDListDrawer");
            if (listDrawerType != null)
            {
                var field = listDrawerType.GetField("_availableIDs", BindingFlags.Static | BindingFlags.NonPublic);
                if (field != null) field.SetValue(null, null);
            }
        }
    }
}