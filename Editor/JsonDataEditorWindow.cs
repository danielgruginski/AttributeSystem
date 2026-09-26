using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using ReactiveSolutions.AttributeSystem.Core.Data;
using SemanticKeys;
using System;
using System.IO;

namespace ReactiveSolutions.AttributeSystem.Editor
{
    /// <summary>
    /// Base for the windows that edit one JSON data file at a time (a StatBlock, an EntityProfile, an Effect, a
    /// StatusEffect): New, Open (any file of its kind, in any Resources folder), Save (back to the file it opened), and
    /// a SerializedObject over the data so the usual property drawers apply. A file's ID is its path in its data folder
    /// without ".json" (see <see cref="DataFilePaths"/>); changing the ID and saving writes a new file.
    /// Opening looks up in the project's KeyDomains the keys a file names without listing them in its key table, and
    /// gives renamed keys their current names, so saving writes an up-to-date file.
    /// </summary>
    public abstract class JsonDataEditorWindow : EditorWindow
    {
        /// <summary>The folder of these files in a Resources folder: the loader's ResourcesPath (e.g. "Data/StatBlocks").</summary>
        protected abstract string ResourcesPath { get; }

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

        // The file being edited: its ID, the data folder it is saved in (an asset path, e.g. "Assets/Resources/Data/StatBlocks"),
        // and the file itself (null until it is saved). Serialized, as is the data, so they survive script reloads.
        [SerializeField] private string _id;
        [SerializeField] private string _folder;
        [SerializeField] private string _assetPath;

        // The data as JSON when it was opened, saved or created, to tell whether it has changed.
        [SerializeField] private string _savedJson;

        // The data while scripts reload: as JSON, or as Unity serializes it when it can't be written as JSON yet.
        [SerializeField] private string _snapshot;
        [SerializeField] private string _editorSnapshot;

        private ScriptableObject _container;
        private SerializedObject _serializedObject;
        private Vector2 _scroll;
        private bool? _hasChanges;

        // Visual styles
        protected GUIStyle HeaderStyle { get; private set; }
        protected GUIStyle BoxStyle { get; private set; }

        private string DefaultFolder => "Assets/Resources/" + ResourcesPath;
        private string TargetPath => $"{_folder}/{CleanId}.json";
        private string CleanId => (_id ?? "").Trim().Trim('/');

        protected virtual void OnEnable()
        {
            RestoreContainer();
            if (_folder == null) StartNew(DefaultFolder, "New" + DataLabel);
        }

        protected virtual void OnDisable()
        {
            // Scripts are reloading, or the window is closing: keep what is being edited.
            if (_container == null) return;
            _snapshot = CurrentJson();
            _editorSnapshot = _snapshot == null ? EditorJsonUtility.ToJson(_container) : null;
            DestroyImmediate(_container);
            _container = null;
        }

        protected virtual void OnGUI()
        {
            if (_container == null || _serializedObject == null || _serializedObject.targetObject == null) RestoreContainer();

            if (HeaderStyle == null)
            {
                HeaderStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
                BoxStyle = new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(10, 10, 10, 10) };
            }

            DrawToolbar();
            DrawFileInfo();

            EditorGUILayout.Space();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            _serializedObject.Update();

            EditorGUI.BeginChangeCheck();
            DrawData(_serializedObject.FindProperty("Data"));
            bool edited = EditorGUI.EndChangeCheck();
            if (_serializedObject.ApplyModifiedProperties() || edited) _hasChanges = null;

            EditorGUILayout.EndScrollView();
        }

        // ---------------------------------------------------------------- Opening and creating files

        /// <summary>
        /// Opens a file: an asset path ("Assets/.../Resources/Data/StatBlocks/Weapons/Sword.json") or a full path. A file
        /// outside the data folders (or in a package, which can't be changed) opens as a new file: saving writes it to
        /// Assets/Resources/Data/.... Asks first if the current file has unsaved changes.
        /// </summary>
        internal void OpenFile(string path)
        {
            if (!CanLeave()) return;

            string assetPath = ToAssetPath(path);
            string json = ReadText(assetPath, path);
            string name = Path.GetFileName(path);
            if (json == null)
            {
                EditorUtility.DisplayDialog($"Can't open {name}", $"{path} can't be read.", "OK");
                return;
            }

            object data;
            try
            {
                data = FromJson(json, KeyDomainLookup.FindByName);
            }
            catch (Exception e)
            {
                // A file with a mistake opens in the code editor, at the line of the mistake.
                if (EditorUtility.DisplayDialog($"Can't open {name}", e.Message, "Edit as Text", "Close"))
                {
                    int line = e is JsonFormatException format ? format.Line : 1;
                    InternalEditorUtility.OpenFileAtLineExternal(assetPath != null ? FullPath(assetPath) : path, Math.Max(line, 1));
                }
                return;
            }

            string problem = CheckLoadedData(data);
            if (problem != null)
            {
                EditorUtility.DisplayDialog($"Can't edit {name} here", problem, "OK");
                return;
            }

            ReplaceContainer(data);
            if (assetPath != null && assetPath.StartsWith("Assets/", StringComparison.Ordinal) &&
                DataFilePaths.TrySplit(assetPath, ResourcesPath, out string folder, out _))
            {
                _folder = folder;
                _id = DataFilePaths.IdOf(assetPath, ResourcesPath);
                _assetPath = assetPath;
            }
            else
            {
                _folder = DefaultFolder;
                _id = Path.GetFileNameWithoutExtension(path);
                _assetPath = null;
            }
            _savedJson = CurrentJson();
            _hasChanges = false;
            Repaint();
        }

        /// <summary>
        /// Starts a new file, to be saved in <paramref name="folder"/> (a data folder of this kind) with the ID
        /// <paramref name="id"/>. Asks first if the current file has unsaved changes.
        /// </summary>
        internal void NewFile(string folder, string id)
        {
            if (!CanLeave()) return;
            StartNew(folder, id);
            Repaint();
        }

        private void StartNew(string folder, string id)
        {
            ReplaceContainer(null);
            _folder = folder;
            _id = id;
            _assetPath = null;
            _savedJson = CurrentJson();
            _hasChanges = false;
        }

        private void OpenOtherFile()
        {
            string start = Directory.Exists(FullPath(_folder)) ? FullPath(_folder) : Application.dataPath;
            string path = EditorUtility.OpenFilePanel($"Open a {DataLabel} file", start, "json");
            if (!string.IsNullOrEmpty(path)) OpenFile(path);
        }

        /// <summary>Whether the window can show something else: no unsaved changes, or the user saved or dropped them.</summary>
        private bool CanLeave()
        {
            if (!HasChanges) return true;

            int choice = EditorUtility.DisplayDialogComplex("Unsaved changes", $"Save the changes to '{CleanId}' first?", "Save", "Cancel", "Don't Save");
            if (choice == 0) return Save();
            return choice == 2;
        }

        // ---------------------------------------------------------------- Saving

        private bool Save()
        {
            string id = CleanId;
            if (id.Length == 0)
            {
                EditorUtility.DisplayDialog($"Can't save the {DataLabel}", "Enter an ID first, such as Weapons/IronSword.", "OK");
                return false;
            }

            string json;
            try
            {
                json = ToJson(GetData(_container));
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog($"Can't save the {DataLabel}", e.Message, "OK");
                return false;
            }

            string target = TargetPath;
            bool newFolder;
            try
            {
                string fullPath = FullPath(target);
                if (target != _assetPath && File.Exists(fullPath) &&
                    !EditorUtility.DisplayDialog("Replace the file?", $"{target} already exists. Replace it?", "Replace", "Cancel"))
                {
                    return false;
                }

                string directory = Path.GetDirectoryName(fullPath);
                newFolder = !Directory.Exists(directory);
                Directory.CreateDirectory(directory);
                File.WriteAllText(fullPath, json);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is NotSupportedException)
            {
                EditorUtility.DisplayDialog($"Can't save {target}", e.Message, "OK");
                return false;
            }

            // A new folder is imported with the file by a refresh.
            if (newFolder) AssetDatabase.Refresh();
            else AssetDatabase.ImportAsset(target);

            _id = id;
            _assetPath = target;
            _savedJson = json;
            _hasChanges = false;
            Debug.Log($"[Attribute System] Saved the {DataLabel} '{id}' to {target}");
            return true;
        }

        private bool HasChanges
        {
            get
            {
                if (_hasChanges == null) _hasChanges = CurrentJson() != _savedJson;
                return _hasChanges.Value;
            }
        }

        /// <summary>The data as it would be saved, or null if it can't be (it then counts as changed).</summary>
        private string CurrentJson()
        {
            try
            {
                return ToJson(GetData(_container));
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ---------------------------------------------------------------- Drawing

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button(new GUIContent("New", $"Start a new {DataLabel}."), EditorStyles.toolbarButton, GUILayout.Width(45)))
            {
                NewFile(_folder, "New" + DataLabel);
                GUIUtility.ExitGUI();
            }

            if (GUILayout.Button(new GUIContent("Open", $"Open a {DataLabel} file of the project."), EditorStyles.toolbarDropDown, GUILayout.Width(55)))
            {
                ShowOpenMenu(GUILayoutUtility.GetLastRect());
            }

            bool saveAsNew = _assetPath != null && TargetPath != _assetPath;
            var saveContent = new GUIContent(saveAsNew ? "Save as New" : "Save", $"Write {TargetPath}.");
            if (GUILayout.Button(saveContent, EditorStyles.toolbarButton, GUILayout.Width(85)))
            {
                Save();
                GUIUtility.ExitGUI();
            }

            GUILayout.FlexibleSpace();

            using (new EditorGUI.DisabledScope(_assetPath == null))
            {
                if (GUILayout.Button(new GUIContent("Show in Project", "Select the file in the Project window."), EditorStyles.toolbarButton))
                {
                    var file = AssetDatabase.LoadAssetAtPath<TextAsset>(_assetPath);
                    Selection.activeObject = file;
                    EditorGUIUtility.PingObject(file);
                }

                if (GUILayout.Button(new GUIContent("Edit as Text", "Open the file in your code editor."), EditorStyles.toolbarButton))
                {
                    InternalEditorUtility.OpenFileAtLineExternal(FullPath(_assetPath), 1);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawFileInfo()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label(Title, EditorStyles.boldLabel);

            _id = EditorGUILayout.TextField(
                new GUIContent("ID", "The file's path in its data folder, without .json: what loaders and ID dropdowns use (e.g. Weapons/IronSword). " +
                                     "Slashes make subfolders. A new ID saves a new file."),
                _id);

            string changes = HasChanges ? " Unsaved changes." : "";
            if (_assetPath == null)
            {
                EditorGUILayout.HelpBox($"New {DataLabel}: Save writes {TargetPath}.{changes}", MessageType.Info);
            }
            else if (TargetPath != _assetPath)
            {
                EditorGUILayout.HelpBox($"Save as New writes {TargetPath}. The file you opened, {_assetPath}, stays as it is.{changes}", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox($"{_assetPath}{changes}", MessageType.None);
            }

            EditorGUILayout.EndVertical();
        }

        private void ShowOpenMenu(Rect position)
        {
            var menu = new GenericMenu();
            var files = DataFiles.Find(ResourcesPath);
            if (files.Count == 0) menu.AddDisabledItem(new GUIContent($"No {DataLabel} files yet"));

            // IDs with folders show as submenus: RPGStarter > Templates > Character.
            foreach (var file in files)
            {
                string assetPath = file.AssetPath;
                menu.AddItem(new GUIContent(file.Id), assetPath == _assetPath, () => OpenFile(assetPath));
            }

            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Other File..."), false, OpenOtherFile);
            menu.DropDown(position);
        }

        // ---------------------------------------------------------------- Container and paths

        private void RestoreContainer()
        {
            string snapshot = _snapshot;
            string editorSnapshot = _editorSnapshot;
            ReplaceContainer(null);

            try
            {
                if (snapshot != null)
                {
                    ReplaceContainer(FromJson(snapshot, KeyDomainLookup.FindByName));
                }
                else if (editorSnapshot != null)
                {
                    EditorJsonUtility.FromJsonOverwrite(editorSnapshot, _container);
                    _serializedObject = new SerializedObject(_container);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Attribute System] The {DataLabel} being edited couldn't be restored after the scripts reloaded: {e.Message}");
            }
        }

        private void ReplaceContainer(object data)
        {
            _snapshot = null;
            _editorSnapshot = null;
            if (_container != null) DestroyImmediate(_container);
            _container = CreateContainer();
            _container.hideFlags = HideFlags.DontSave;
            if (data != null) SetData(_container, data);

            _serializedObject = new SerializedObject(_container);
            if (data != null)
            {
                _serializedObject.Update();
                KeyDomainLookup.RefreshKeys(_serializedObject);
            }
            _hasChanges = null;
        }

        private static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);

        private static string FullPath(string assetPath) => Path.GetFullPath(Path.Combine(ProjectRoot, assetPath));

        /// <summary>"Assets/..." (or "Packages/...") for a path in the project, or null for a file elsewhere.</summary>
        private static string ToAssetPath(string path)
        {
            string normalized = path.Replace('\\', '/');
            if (normalized.StartsWith("Assets/", StringComparison.Ordinal) || normalized.StartsWith("Packages/", StringComparison.Ordinal))
            {
                return normalized;
            }

            string root = Path.GetFullPath(ProjectRoot).Replace('\\', '/').TrimEnd('/') + "/";
            string full = Path.GetFullPath(path).Replace('\\', '/');
            return full.StartsWith(root + "Assets/", StringComparison.OrdinalIgnoreCase) ? full.Substring(root.Length) : null;
        }

        private static string ReadText(string assetPath, string path)
        {
            try
            {
                // A package's files may not be where its asset path says, so they are read as assets.
                if (assetPath != null && assetPath.StartsWith("Packages/", StringComparison.Ordinal))
                {
                    return AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath)?.text;
                }
                return File.ReadAllText(assetPath != null ? FullPath(assetPath) : path);
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }
    }
}
