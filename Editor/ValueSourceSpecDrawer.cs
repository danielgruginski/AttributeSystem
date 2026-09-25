using UnityEditor;
using UnityEngine;
using ReactiveSolutions.AttributeSystem.Core;

namespace ReactiveSolutions.AttributeSystem.Editor
{
    /// <summary>
    /// Draws a ValueSource: its Mode, then a number (Constant), an attribute and its path (Attribute), or a logic
    /// dropdown with the logic's fields below it (Formula).
    /// </summary>
    [CustomPropertyDrawer(typeof(ValueSource))]
    public class ValueSourceSpecDrawer : PropertyDrawer
    {
        // Constants for layout
        private const float ModeWidth = 80f;
        private const float Padding = 5f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var modeProp = property.FindPropertyRelative("Mode");
            if (modeProp == null) return EditorGUIUtility.singleLineHeight;

            switch ((ValueSource.SourceMode)modeProp.enumValueIndex)
            {
                case ValueSource.SourceMode.Attribute:
                    // The Mode dropdown shares the first line with the AttributeReference drawer, which sets the height.
                    var attrRefProp = property.FindPropertyRelative("AttributeRef");
                    return attrRefProp != null ? EditorGUI.GetPropertyHeight(attrRefProp, true) : EditorGUIUtility.singleLineHeight;

                case ValueSource.SourceMode.Formula:
                    // The logic dropdown shares the first line; the logic's fields go below.
                    var formulaProp = property.FindPropertyRelative("Formula");
                    return EditorGUIUtility.singleLineHeight + (formulaProp != null ? ModifierLogicGUI.GetFieldsHeight(formulaProp) : 0f);

                default:
                    return EditorGUIUtility.singleLineHeight;
            }
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var modeProp = property.FindPropertyRelative("Mode");
            var constantProp = property.FindPropertyRelative("ConstantValue");
            var attrRefProp = property.FindPropertyRelative("AttributeRef");
            var formulaProp = property.FindPropertyRelative("Formula");

            // 1. Draw Label
            Rect contentPosition = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

            // 2. Draw Mode Dropdown (Fixed Width)
            Rect modeRect = new Rect(contentPosition.x, contentPosition.y, ModeWidth, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(modeRect, modeProp, GUIContent.none);

            // 3. Draw Content
            Rect valueRect = new Rect(contentPosition.x + ModeWidth + Padding, contentPosition.y, contentPosition.width - ModeWidth - Padding, position.height);
            Rect valueLine = new Rect(valueRect.x, valueRect.y, valueRect.width, EditorGUIUtility.singleLineHeight);

            // The indent is already in the prefix label's rects; don't apply it twice.
            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            switch ((ValueSource.SourceMode)modeProp.enumValueIndex)
            {
                case ValueSource.SourceMode.Constant:
                    EditorGUI.PropertyField(valueLine, constantProp, GUIContent.none);
                    break;

                case ValueSource.SourceMode.Attribute:
                    // AttributeReferenceDrawer draws the attribute on the first line, and its path below.
                    if (attrRefProp != null) EditorGUI.PropertyField(valueRect, attrRefProp, GUIContent.none, true);
                    break;

                case ValueSource.SourceMode.Formula:
                    if (formulaProp == null) break;
                    ModifierLogicGUI.DrawTypePopup(valueLine, formulaProp, GUIContent.none);

                    // The formula's fields, below, across the full width.
                    EditorGUI.indentLevel = indent;
                    Rect fields = new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight,
                        position.width, position.height - EditorGUIUtility.singleLineHeight);
                    ModifierLogicGUI.DrawFields(fields, formulaProp);
                    break;
            }

            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }
    }
}
