using UnityEngine;
using UnityEditor;
using System;
using System.IO;

namespace ReactiveSolutions.AttributeSystem.Editor
{
    /// <summary>
    /// Base for editor windows that edit one JSON data file at a time (a StatBlock, an EntityProfile): the file
    /// name, New / Load / Save, and a SerializedObject over the data so the usual property drawers apply.
    /// Files are saved under Assets/{JsonFolder}; a file's ID is its path in that folder without ".json".
    /// </summary>
    public abstract class JsonDataEditorWindow : EditorWindow
    {
        /// <summary>The folder with the JSON files, relative to Assets (e.g. "Resources/Data/StatBlocks").</summary>
        protected abstract string JsonFolder { get; }

        /// <summary>The kind of data, for labels and logs (e.g. "StatBlock").</summary>
        protected abstract string DataLabel { get; }

        /// <summary>The title shown at the top of the window.</summary>
        protected virtual string Title => $"{DataLabel} Editor";

        /// <summary>A new in-memory container whose serialized field "Data" holds the object being edited.</summary>
        protected abstract ScriptableObject CreateContainer();

        /// <summary>The object being edited: the container's "Data".</summary>
        protected abstract object GetData(ScriptableObject container);

        /// <summary>Draws the "Data" property.</summary>
        protected abstract void DrawData(SerializedProperty data);

        private ScriptableObject _container;
        private SerializedObject _serializedObject;

        private string _currentFileName;
        private string _fullFilePath;
        private Vector2 _scroll;

        // Visual styles
        protected GUIStyle HeaderStyle { get; private set; }
        protected GUIStyle BoxStyle { get; private set; }

        private string FolderPath => Path.Combine(Application.dataPath, JsonFolder);

        protected virtual void OnEnable()
        {
            EnsureDirectory();
            if (_container == null) CreateNewContainer();
        }

        protected virtual void OnDisable()
        {
            if (_container != null) DestroyImmediate(_container);
        }

        protected virtual void OnGUI()
        {
            // Safety Init
            if (_container == null || _serializedObject == null || _serializedObject.targetObject == null)
            {
                CreateNewContainer();
            }

            // Setup Styles
            if (HeaderStyle == null)
            {
                HeaderStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
                BoxStyle = new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(10, 10, 10, 10) };
            }

            DrawHeader();

            EditorGUILayout.Space();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            _serializedObject.Update();

            DrawData(_serializedObject.FindProperty("Data"));

            _serializedObject.ApplyModifiedProperties();
            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label(Title, EditorStyles.boldLabel);

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
                EditorGUILayout.HelpBox($"Unsaved new {DataLabel}", MessageType.Warning);
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
            _container = CreateContainer();
            _serializedObject = new SerializedObject(_container);

            _currentFileName = "New" + DataLabel;
            _fullFilePath = null;
        }

        private void EnsureDirectory()
        {
            if (!Directory.Exists(FolderPath))
            {
                Directory.CreateDirectory(FolderPath);
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

            // The name may include subfolders (e.g. "Weapons/IronSword"), matching the loaders' IDs.
            string fileName = _currentFileName.Replace(" ", "_") + ".json";
            string fullPath = Path.Combine(FolderPath, fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

            string json = JsonUtility.ToJson(GetData(_container), true);
            File.WriteAllText(fullPath, json);

            // ImportAsset expects a project-relative path ("Assets/...").
            AssetDatabase.ImportAsset($"Assets/{JsonFolder}/{fileName}");
            _fullFilePath = fullPath;
            Debug.Log($"Saved {DataLabel} to {fileName}");
            Repaint();
        }

        private void LoadJson()
        {
            if (!Directory.Exists(FolderPath)) Directory.CreateDirectory(FolderPath);

            string filePath = EditorUtility.OpenFilePanel("Load JSON", FolderPath, "json");
            if (string.IsNullOrEmpty(filePath)) return;

            try
            {
                string json = File.ReadAllText(filePath);

                CreateNewContainer();
                JsonUtility.FromJsonOverwrite(json, GetData(_container));
                _serializedObject.Update();

                _fullFilePath = filePath;
                _currentFileName = ToDataId(filePath, FolderPath);
            }
            catch (Exception e)
            {
                Debug.LogError($"Load failed: {e.Message}");
            }
        }

        /// <summary>
        /// ".../Resources/Data/StatBlocks/Weapons/IronSword.json" -> "Weapons/IronSword" (the ID the loaders expect).
        /// </summary>
        private static string ToDataId(string filePath, string rootPath)
        {
            string full = Path.GetFullPath(filePath).Replace('\\', '/');
            string root = Path.GetFullPath(rootPath).Replace('\\', '/').TrimEnd('/') + "/";
            string relative = full.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                ? full.Substring(root.Length)
                : Path.GetFileName(full);
            return relative.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                ? relative.Substring(0, relative.Length - ".json".Length)
                : relative;
        }
    }
}