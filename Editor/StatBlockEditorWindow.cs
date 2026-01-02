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

            // FIX: Force ModifierFactory to initialize its static metadata.
            // Without this, _parameterMetadata is empty until the game starts.
            new ModifierFactory();
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
                new ModifierFactory(); // Ensure factory metadata is ready
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
            SerializedProperty tagsProp = dataProp.FindPropertyRelative("Tags");
            SerializedProperty remoteTagsProp = dataProp.FindPropertyRelative("RemoteTags");
            SerializedProperty baseValuesProp = dataProp.FindPropertyRelative("BaseValues");
            SerializedProperty modifiersProp = dataProp.FindPropertyRelative("Modifiers");

            // --- 1. Draw Base Values (Standard Inspector is fine here) ---

            EditorGUILayout.LabelField("Tags", _headerStyle);
            EditorGUILayout.PropertyField(tagsProp, true);

            EditorGUILayout.LabelField("Remote Tags", _headerStyle);
            EditorGUILayout.PropertyField(remoteTagsProp, true);

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
            string[] paramNames = ModifierFactory.GetParameterNames(logicKey);
            SerializedProperty argsProp = spec.FindPropertyRelative("Arguments");

            // RESIZE SAFETY: Only resize if needed, and apply immediately
            if (argsProp.arraySize != paramNames.Length)
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
            if (paramNames.Length > 0)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Parameters:", EditorStyles.miniBoldLabel);

                EditorGUI.indentLevel++;
                for (int j = 0; j < argsProp.arraySize; j++)
                {
                    if (j >= paramNames.Length) break;

                    SerializedProperty arg = argsProp.GetArrayElementAtIndex(j);
                    string label = paramNames[j];

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
            // Try to find the backing field "_value" or "Guid"
            var valProp = semanticKeyProp.FindPropertyRelative("_value");
            if (valProp != null) return valProp.stringValue;

            var guidProp = semanticKeyProp.FindPropertyRelative("Guid");
            if (guidProp != null) return guidProp.stringValue;

            return "Static";
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

            string json = JsonUtility.ToJson(_container.Data, true);
            File.WriteAllText(fullPath, json);

            AssetDatabase.ImportAsset(fullPath);
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
                _currentFileName = Path.GetFileNameWithoutExtension(filePath);
            }
            catch (Exception e)
            {
                Debug.LogError($"Load failed: {e.Message}");
            }
        }
    }
}