using ReactiveSolutions.AttributeSystem.Core.Data;
using ReactiveSolutions.AttributeSystem.Core.Modifiers;
using SemanticKeys;
using System;
using UnityEditor;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Editor
{
    /// <summary>
    /// Creates and edits Effect JSON files under Resources/Data/Effects (a hit, a spell, a potion). Game code loads them
    /// with EffectJsonLoader and applies them with Effect.Apply(source, target); [EffectID] fields pick them by ID.
    /// </summary>
    public class EffectEditorWindow : JsonDataEditorWindow
    {
        // Bridges the Effect POCO to the Inspector drawers. Only ever created in memory.
        public class EffectContainer : ScriptableObject
        {
            public Effect Data = new Effect();
        }

        private static readonly GUIContent RolesHelp = new GUIContent(
            "An effect reaches attributes and tags through its Source (the attacker, the caster) or its Target: start each " +
            "path with one of them (the keys of the Effect Roles domain). Health with the path Target is the target's Health; " +
            "AttackPower with the path Source is the source's.");

        [MenuItem("Window/Attribute System/Effect Editor")]
        public static void ShowWindow() => GetWindow<EffectEditorWindow>("Effect Editor");

        protected override string JsonFolder => "Resources/" + EffectJsonLoader.ResourcesPath;
        protected override string DataLabel => "Effect";

        protected override ScriptableObject CreateContainer() => CreateInstance<EffectContainer>();
        protected override object GetData(ScriptableObject container) => ((EffectContainer)container).Data;
        protected override void SetData(ScriptableObject container, object data) => ((EffectContainer)container).Data = (Effect)data;

        protected override string ToJson(object data) => EffectJson.ToJson((Effect)data);
        protected override object FromJson(string json, Func<string, SemanticKey> findKey) => EffectJson.FromJson(json, findKey);

        protected override void DrawData(SerializedProperty data)
        {
            EditorGUILayout.PropertyField(data.FindPropertyRelative("EffectName"), new GUIContent("Effect Name"));
            EditorGUILayout.HelpBox(RolesHelp.text, MessageType.Info);
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Condition", HeaderStyle);
            EditorGUILayout.PropertyField(data.FindPropertyRelative("Condition"), true);

            EditorGUILayout.LabelField("Costs", HeaderStyle);
            EditorGUILayout.PropertyField(data.FindPropertyRelative("Costs"), true);

            EditorGUILayout.Space(15);
            EditorGUILayout.LabelField("Actions", HeaderStyle);
            DrawActions(data.FindPropertyRelative("Actions"));

            EditorGUILayout.Space(15);
            EditorGUILayout.LabelField("Status Effects", HeaderStyle);
            EditorGUILayout.PropertyField(data.FindPropertyRelative("RemoveStatusCategories"), new GUIContent("Remove (Categories)"), true);
            EditorGUILayout.PropertyField(data.FindPropertyRelative("Statuses"), new GUIContent("Apply"), true);
        }

        private void DrawActions(SerializedProperty list)
        {
            if (list == null) return;

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+ Add Action", GUILayout.Height(24), GUILayout.Width(120)))
            {
                list.arraySize++;
                // The new entry copies the last one; give it its own logic object instead of sharing that one's.
                list.GetArrayElementAtIndex(list.arraySize - 1).FindPropertyRelative("Logic").managedReferenceValue = new ValueLogic();
            }
            EditorGUILayout.EndHorizontal();

            for (int i = 0; i < list.arraySize; i++)
            {
                if (DrawAction(list.GetArrayElementAtIndex(i), i))
                {
                    list.DeleteArrayElementAtIndex(i);
                    break; // Exit loop to avoid invalid index access this frame
                }
            }
        }

        /// <summary>Draws one action. Returns true if the user asked to delete it.</summary>
        private bool DrawAction(SerializedProperty action, int index)
        {
            EditorGUILayout.BeginVertical(BoxStyle);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Action #{index + 1}", EditorStyles.boldLabel);
            if (GUILayout.Button("X", GUILayout.Width(25)))
            {
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return true;
            }
            EditorGUILayout.EndHorizontal();

            var target = action.FindPropertyRelative("Target");
            EffectActionDrawer.ExpandPath(target);
            EditorGUILayout.PropertyField(target, new GUIContent("Target", target.tooltip), true);
            EditorGUILayout.PropertyField(action.FindPropertyRelative("Type"));
            EditorGUILayout.PropertyField(action.FindPropertyRelative("Chance"), true);
            EditorGUILayout.PropertyField(action.FindPropertyRelative("Condition"), true);

            EditorGUILayout.Space(5);
            ModifierLogicGUI.DrawLayout(action.FindPropertyRelative("Logic"));

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
            return false;
        }
    }
}
