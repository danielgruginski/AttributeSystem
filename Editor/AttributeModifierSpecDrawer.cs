using ReactiveSolutions.AttributeSystem.Core;
using UnityEditor;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Editor
{
    /// <summary>
    /// Draws an AttributeModifierSpec in a box: target, Type and Priority, Source Id, then the Logic dropdown
    /// and the chosen logic's fields.
    /// </summary>
    [CustomPropertyDrawer(typeof(AttributeModifierSpec))]
    public class AttributeModifierSpecDrawer : PropertyDrawer
    {
        private const float Padding = 5f;

        private static float LineH => EditorGUIUtility.singleLineHeight;
        private static float Spacing => EditorGUIUtility.standardVerticalSpacing;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float h = Padding * 2;

            // Header
            h += LineH + Spacing;

            // Target
            h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("TargetAttribute")) + Spacing;
            h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("TargetPath"), true) + Spacing;

            // Type & Priority, Source Id
            h += LineH + Spacing;
            h += LineH + Spacing;

            // Logic
            h += ModifierLogicGUI.GetHeight(property.FindPropertyRelative("Logic"));

            return h;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            // --- Background Box ---
            GUI.Box(position, GUIContent.none, EditorStyles.helpBox);

            Rect content = new Rect(position.x + Padding, position.y + Padding, position.width - Padding * 2, position.height - Padding * 2);
            float currentY = content.y;

            Rect NextRect(float height)
            {
                Rect r = new Rect(content.x, currentY, content.width, height);
                currentY += height + Spacing;
                return r;
            }

            // --- Header ---
            EditorGUI.LabelField(NextRect(LineH), string.IsNullOrEmpty(label.text) ? "Modifier" : label.text, EditorStyles.boldLabel);

            // --- Target ---
            var targetProp = property.FindPropertyRelative("TargetAttribute");
            EditorGUI.PropertyField(NextRect(EditorGUI.GetPropertyHeight(targetProp)), targetProp);

            var targetPathProp = property.FindPropertyRelative("TargetPath");
            EditorGUI.PropertyField(NextRect(EditorGUI.GetPropertyHeight(targetPathProp, true)), targetPathProp, true);

            // --- Type & Priority ---
            Rect row = NextRect(LineH);
            float half = row.width / 2f;
            float oldLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 40;
            EditorGUI.PropertyField(new Rect(row.x, row.y, half - 2, row.height), property.FindPropertyRelative("Type"));
            EditorGUIUtility.labelWidth = 50;
            EditorGUI.PropertyField(new Rect(row.x + half + 2, row.y, half - 2, row.height), property.FindPropertyRelative("Priority"));
            EditorGUIUtility.labelWidth = oldLabelWidth;

            // --- Source Id ---
            EditorGUI.PropertyField(NextRect(LineH), property.FindPropertyRelative("SourceId"), new GUIContent("Source Id"));

            // --- Logic ---
            var logicProp = property.FindPropertyRelative("Logic");
            ModifierLogicGUI.Draw(NextRect(ModifierLogicGUI.GetHeight(logicProp)), logicProp);

            EditorGUI.EndProperty();
        }
    }
}