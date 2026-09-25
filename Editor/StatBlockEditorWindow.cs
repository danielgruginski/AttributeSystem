using UnityEngine;
using UnityEditor;
using System.IO;
using System;
using System.Collections.Generic;
using System.Reflection;
using ReactiveSolutions.AttributeSystem.Core; // For ModifierFactory
using ReactiveSolutions.AttributeSystem.Core.Data;
using SemanticKeys;

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

        private StatBlockContainer _container;
        private SerializedObject _serializedObject;
        private const string JSON_PATH = "Resources/Data/StatBlocks";

        private string _currentFileName = "NewStatBlock";
        private string _fullFilePath;
        private Vector2 _scroll;

        // Visual styles
        private GUIStyle _headerStyle;
        private GUIStyle _boxStyle;

        [MenuItem("Window/Attribute System/Stat Block Editor (Unified)")]
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
            // Safety Init
            if (_container == null || _serializedObject == null || _serializedObject.targetObject == null)
            {
                CreateNewContainer();
            }

            // Setup Styles
            if (_headerStyle == null)
            {
                _headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
                _boxStyle = new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(10, 10, 10, 10) };
            }

            DrawHeader();

            EditorGUILayout.Space();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            _serializedObject.Update();

            SerializedProperty dataProp = _serializedObject.FindProperty("Data");
            SerializedProperty activationProp = dataProp.FindPropertyRelative("ActivationCondition");
            SerializedProperty tagsProp = dataProp.FindPropertyRelative("Tags");
            SerializedProperty remoteTagsProp = dataProp.FindPropertyRelative("RemoteTags");
            SerializedProperty pointersProp = dataProp.FindPropertyRelative("Pointers");
            SerializedProperty baseValuesProp = dataProp.FindPropertyRelative("BaseValues");
            SerializedProperty modifiersProp = dataProp.FindPropertyRelative("Modifiers");

            // --- 1. Draw Base Values (Standard Inspector is fine here) ---
            EditorGUILayout.LabelField("Activation Conditions", _headerStyle);
            EditorGUILayout.PropertyField(activationProp, true);

            EditorGUILayout.LabelField("Tags", _headerStyle);
            EditorGUILayout.PropertyField(tagsProp, true);

            EditorGUILayout.LabelField("Remote Tags", _headerStyle);
            EditorGUILayout.PropertyField(remoteTagsProp, true);

            EditorGUILayout.LabelField("Attribute Pointers (Aliases)", _headerStyle);
            EditorGUILayout.PropertyField(pointersProp, true);

            EditorGUILayout.LabelField("Base Attributes", _headerStyle);
            EditorGUILayout.PropertyField(baseValuesProp, true);


            EditorGUILayout.Space(15);
            EditorGUILayout.LabelField("Modifier Pipeline", _headerStyle);

            // --- 2. Custom Modifier Drawing ---
            DrawModifiersList(modifiersProp);

            _serializedObject.ApplyModifiedProperties();
            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// Manually draws the list of modifiers to avoid PropertyDrawer limitations.
        /// </summary>
        private void DrawModifiersList(SerializedProperty list)
        {
            if (list == null) return;

            // Header Row
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+ Add Modifier", GUILayout.Height(24), GUILayout.Width(120)))
            {
                list.arraySize++;
            }
            EditorGUILayout.EndHorizontal();

            // Iterate Elements
            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty spec = list.GetArrayElementAtIndex(i);

                // Draw the individual modifier box
                // We pass 'i' to handle deletion correctly
                bool deleted = DrawModifierSpec(spec, i);

                if (deleted)
                {
                    list.DeleteArrayElementAtIndex(i);
                    break; // Exit loop to avoid invalid index access this frame
                }
            }
        }

        /// <summary>
        /// Draws a single AttributeModifierSpec. 
        /// Returns TRUE if the user requested to delete this item.
        /// </summary>
        private bool DrawModifierSpec(SerializedProperty spec, int index)
        {
            EditorGUILayout.BeginVertical(_boxStyle);

            // -- Toolbar (Title + Delete) --
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Modifier #{index + 1}", EditorStyles.boldLabel);
            if (GUILayout.Button("X", GUILayout.Width(25)))
            {
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return true;
            }
            EditorGUILayout.EndHorizontal();

            // -- Logic Type --
            SerializedProperty logicProp = spec.FindPropertyRelative("LogicType");

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(logicProp, new GUIContent("Logic Type"));

            // FIX 2: If Logic Type changed, force an update immediately so GetLogicKeyString sees it.
            if (EditorGUI.EndChangeCheck())
            {
                spec.serializedObject.ApplyModifiedProperties();
                spec.serializedObject.Update();
            }

            // Determine the current logic key (String)
            string logicKey = GetLogicKeyString(logicProp);

            // -- Sync Arguments --
            bool isKnownLogic = ModifierFactory.TryGetParameterNames(logicKey, out var paramNames);
            if (!isKnownLogic) paramNames = new string[0];
            SerializedProperty argsProp = spec.FindPropertyRelative("Arguments");

            // RESIZE SAFETY: only ever grow automatically (shrinking would delete data for logic types
            // this editor session doesn't know); removing extras is an explicit button below.
            if (isKnownLogic && argsProp.arraySize < paramNames.Length)
            {
                argsProp.arraySize = paramNames.Length;

                // FIX 3: Force apply so the new null elements exist in memory
                // before the PropertyField tries to draw them below.
                spec.serializedObject.ApplyModifiedProperties();
                spec.serializedObject.Update();
            }

            // -- Target & Configuration --
            EditorGUILayout.PropertyField(spec.FindPropertyRelative("TargetAttribute"));
            EditorGUILayout.PropertyField(spec.FindPropertyRelative("TargetPath"));

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(spec.FindPropertyRelative("Type"));
            EditorGUILayout.PropertyField(spec.FindPropertyRelative("Priority"));
            EditorGUILayout.EndHorizontal();

            // -- Arguments (Dynamic Labels) --
            if (argsProp.arraySize > 0 || !isKnownLogic)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Parameters:", EditorStyles.miniBoldLabel);

                if (!isKnownLogic)
                {
                    EditorGUILayout.HelpBox($"Unknown logic type '{logicKey}': arguments are kept as they are.", MessageType.Warning);
                }
                else if (argsProp.arraySize > paramNames.Length &&
                         GUILayout.Button($"Remove {argsProp.arraySize - paramNames.Length} unused argument(s)"))
                {
                    argsProp.arraySize = paramNames.Length;
                    spec.serializedObject.ApplyModifiedProperties();
                    GUIUtility.ExitGUI(); // Layout changed mid-event; redraw from scratch.
                }

                EditorGUI.indentLevel++;
                for (int j = 0; j < argsProp.arraySize; j++)
                {
                    SerializedProperty arg = argsProp.GetArrayElementAtIndex(j);
                    string label = !isKnownLogic ? $"Argument {j}"
                        : j < paramNames.Length ? paramNames[j] : $"Unused ({j})";

                    // Draw the ValueSource using its own drawer (ValueSourceSpecDrawer)
                    EditorGUILayout.PropertyField(arg, new GUIContent(label));
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5); // Spacing between items

            return false;
        }

        private string GetLogicKeyString(SerializedProperty semanticKeyProp)
        {
            // The factory is keyed by the SemanticKey's string value ("_value"). Unset means Static, as at runtime.
            var valProp = semanticKeyProp.FindPropertyRelative("_value");
            string key = valProp != null ? valProp.stringValue : null;
            return string.IsNullOrEmpty(key) ? "Static" : key;
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
            // The name may include subfolders (e.g. "Weapons/IronSword"), matching StatBlockJsonLoader IDs.
            string fileName = _currentFileName.Replace(" ", "_") + ".json";
            string fullPath = Path.Combine(path, fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

            string json = JsonUtility.ToJson(_container.Data, true);
            File.WriteAllText(fullPath, json);

            // ImportAsset expects a project-relative path ("Assets/...").
            AssetDatabase.ImportAsset($"Assets/{JSON_PATH}/{fileName}");
            _fullFilePath = fullPath;
            Debug.Log($"Saved StatBlock to {fileName}");
            Repaint();
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
                JsonUtility.FromJsonOverwrite(json, _container.Data);
                _serializedObject.Update();

                _fullFilePath = filePath;
                _currentFileName = ToStatBlockId(filePath, path);
            }
            catch (Exception e)
            {
                Debug.LogError($"Load failed: {e.Message}");
            }
        }

        /// <summary>
        /// ".../Resources/Data/StatBlocks/Weapons/IronSword.json" -> "Weapons/IronSword" (the ID StatBlockJsonLoader expects).
        /// </summary>
        private static string ToStatBlockId(string filePath, string rootPath)
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