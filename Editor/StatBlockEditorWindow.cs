using UnityEngine;
using UnityEditor;
using ReactiveSolutions.AttributeSystem.Core; // For ModifierFactory
using ReactiveSolutions.AttributeSystem.Core.Data;

namespace ReactiveSolutions.AttributeSystem.Editor
{
    public class StatBlockEditorWindow : JsonDataEditorWindow
    {
        // -----------------------------------------------------------
        // Helper Container: Bridges POCO StatBlock -> Unity Inspector
        // -----------------------------------------------------------
        public class StatBlockContainer : ScriptableObject
        {
            public StatBlock Data = new StatBlock();
        }

        [MenuItem("Window/Attribute System/Stat Block Editor (Unified)")]
        public static void ShowWindow() => GetWindow<StatBlockEditorWindow>("StatBlock Editor");

        protected override string JsonFolder => "Resources/" + StatBlockJsonLoader.ResourcesPath;
        protected override string DataLabel => "StatBlock";
        protected override string Title => "Unified StatBlock Editor";

        protected override ScriptableObject CreateContainer() => CreateInstance<StatBlockContainer>();
        protected override object GetData(ScriptableObject container) => ((StatBlockContainer)container).Data;

        protected override void DrawData(SerializedProperty dataProp)
        {
            SerializedProperty activationProp = dataProp.FindPropertyRelative("ActivationCondition");
            SerializedProperty tagsProp = dataProp.FindPropertyRelative("Tags");
            SerializedProperty remoteTagsProp = dataProp.FindPropertyRelative("RemoteTags");
            SerializedProperty pointersProp = dataProp.FindPropertyRelative("Pointers");
            SerializedProperty baseValuesProp = dataProp.FindPropertyRelative("BaseValues");
            SerializedProperty modifiersProp = dataProp.FindPropertyRelative("Modifiers");

            // --- 1. Draw Base Values (Standard Inspector is fine here) ---
            // Shown in logs (e.g. when a block is disabled) and by the Attribute Debugger.
            EditorGUILayout.PropertyField(dataProp.FindPropertyRelative("BlockName"), new GUIContent("Block Name"));
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Activation Conditions", HeaderStyle);
            EditorGUILayout.PropertyField(activationProp, true);

            EditorGUILayout.LabelField("Tags", HeaderStyle);
            EditorGUILayout.PropertyField(tagsProp, true);

            EditorGUILayout.LabelField("Remote Tags", HeaderStyle);
            EditorGUILayout.PropertyField(remoteTagsProp, true);

            EditorGUILayout.LabelField("Attribute Pointers (Aliases)", HeaderStyle);
            EditorGUILayout.PropertyField(pointersProp, true);

            EditorGUILayout.LabelField("Base Attributes", HeaderStyle);
            EditorGUILayout.PropertyField(baseValuesProp, true);


            EditorGUILayout.Space(15);
            EditorGUILayout.LabelField("Modifier Pipeline", HeaderStyle);

            // --- 2. Custom Modifier Drawing ---
            DrawModifiersList(modifiersProp);
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
            EditorGUILayout.BeginVertical(BoxStyle);

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
            EditorGUILayout.PropertyField(spec.FindPropertyRelative("SourceId"), new GUIContent("Source Id"));

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
    }
}