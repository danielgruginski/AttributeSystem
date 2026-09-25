using UnityEngine;
using UnityEditor;
using SemanticKeys;
using System;
using System.IO;

namespace ReactiveSolutions.AttributeSystem.Editor
{
    /// <summary>
    /// Base for editor windows that edit one JSON data file at a time (a StatBlock, an EntityProfile): the file
    /// name, New / Load / Save, and a SerializedObject over the data so the usual property drawers apply.
    /// Files are saved under Assets/{JsonFolder}; a file's ID is its path in that folder without ".json".
    /// Loading looks up in the project's KeyDomains the keys a file names without listing them in its key table,
    /// and gives renamed keys their current names, so saving writes an up-to-date file.
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

        /// <summary>The data as JSON (e.g. StatBlockJson.ToJson).</summary>
        protected abstract string ToJson(object data);

        /// <summary>
        /// Reads a file (e.g. StatBlockJson.FromJson). <paramref name="findKey"/> finds the keys it names that aren't
        /// in its key table.
        /// </summary>
        protected abstract object FromJson(string json, Func<string, SemanticKey> findKey);

        /// <summary>Replaces the container's "Data".</summary>
        protected abstract void SetData(ScriptableObject container, object data);

        /// <summary>Why data just read from a file can't be edited in this window, or null if it can.</summary>
        protected virtual string CheckLoadedData(object data) => null;

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

            string json;
            try
            {
                json = ToJson(GetData(_container));
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog($"Can't save the {DataLabel}", e.Message, "OK");
                return;
            }

            // The name may include subfolders (e.g. "Weapons/IronSword"), matching the loaders' IDs.
            string fileName = _currentFileName.Replace(" ", "_") + ".json";
            string fullPath = Path.Combine(FolderPath, fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
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

            string fileName = Path.GetFileName(filePath);
            object data;
            try
            {
                data = FromJson(File.ReadAllText(filePath), KeyDomainLookup.FindByName);
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog($"Can't load {fileName}", e.Message, "OK");
                return;
            }

            string problem = CheckLoadedData(data);
            if (problem != null)
            {
                EditorUtility.DisplayDialog($"Can't edit {fileName} here", problem, "OK");
                return;
            }

            CreateNewContainer();
            SetData(_container, data);
            _serializedObject.Update();
            KeyDomainLookup.RefreshKeys(_serializedObject);

            _fullFilePath = filePath;
            _currentFileName = ToDataId(filePath, FolderPath);
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