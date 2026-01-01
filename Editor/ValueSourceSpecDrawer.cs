using UnityEditor;
using UnityEngine;
using ReactiveSolutions.AttributeSystem.Core;

namespace ReactiveSolutions.AttributeSystem.Editor
{
    [CustomPropertyDrawer(typeof(ValueSource))]
    public class ValueSourceSpecDrawer : PropertyDrawer
    {
        // Constants for layout
        private const float ModeWidth = 80f;
        private const float Padding = 5f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var modeProp = property.FindPropertyRelative("Mode");

            // If in Attribute mode, the height depends on the AttributeReference drawer
            if (modeProp != null && modeProp.enumValueIndex == (int)ValueSource.SourceMode.Attribute)
            {
                var attrRefProp = property.FindPropertyRelative("AttributeRef");
                if (attrRefProp != null)
                {
                    // Add a tiny bit of padding for the mode dropdown line
                    // Actually, AttributeReferenceDrawer handles its own height, 
                    // but we draw the Mode dropdown on the same line as the "Name" part of the ref.
                    // Let's defer to the AttributeReference height.
                    return EditorGUI.GetPropertyHeight(attrRefProp, true);
                }
            }

            // Default single line for Constant mode
            return EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var modeProp = property.FindPropertyRelative("Mode");
            var constantProp = property.FindPropertyRelative("ConstantValue");
            var attrRefProp = property.FindPropertyRelative("AttributeRef");

            // 1. Draw Label
            Rect contentPosition = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

            // 2. Draw Mode Dropdown (Fixed Width)
            Rect modeRect = new Rect(contentPosition.x, contentPosition.y, ModeWidth, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(modeRect, modeProp, GUIContent.none);

            // 3. Draw Content
            Rect valueRect = new Rect(contentPosition.x + ModeWidth + Padding, contentPosition.y, contentPosition.width - ModeWidth - Padding, position.height);

            int modeIndex = modeProp.enumValueIndex; // 0 = Constant, 1 = Attribute

            if (modeIndex == (int)ValueSource.SourceMode.Constant)
            {
                // Just draw the float field
                // Ensure height is single line for the float field even if 'position' is tall
                Rect floatRect = new Rect(valueRect.x, valueRect.y, valueRect.width, EditorGUIUtility.singleLineHeight);
                EditorGUI.PropertyField(floatRect, constantProp, GUIContent.none);
            }
            else
            {
                if (attrRefProp != null)
                {
                    // Pass drawing to AttributeReferenceDrawer.
                    // IMPORTANT: AttributeReferenceDrawer expects to draw the label. 
                    // We pass GUIContent.none because we already drew our main label (e.g. "Input").
                    // However, AttributeReferenceDrawer starts with the "Name" field.
                    // We need to ensure it draws within 'valueRect'.

                    EditorGUI.PropertyField(valueRect, attrRefProp, GUIContent.none, true);
                }
            }

            EditorGUI.EndProperty();
        }
    }
}