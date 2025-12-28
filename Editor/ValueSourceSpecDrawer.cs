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

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            // 1. Fetch Properties (Safe Checks)
            var modeProp = property.FindPropertyRelative("Mode");
            var constantProp = property.FindPropertyRelative("ConstantValue");
            var attrNameProp = property.FindPropertyRelative("AttributeName");
            var providerPathProp = property.FindPropertyRelative("ProviderPath");

            if (modeProp == null)
            {
                EditorGUI.LabelField(position, "Error: ValueSource fields not found.");
                EditorGUI.EndProperty();
                return;
            }

            // 2. Draw Label (if inside a list, this is "Element X" or our custom label)
            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

            // 3. Draw Mode Dropdown
            var modeRect = new Rect(position.x, position.y, ModeWidth, position.height);
            EditorGUI.PropertyField(modeRect, modeProp, GUIContent.none);

            // 4. Draw Context-Specific Field
            var valueRect = new Rect(position.x + ModeWidth + Padding, position.y, position.width - ModeWidth - Padding, position.height);

            int modeIndex = modeProp.enumValueIndex;
            // Assuming Enum Order: 0 = Constant, 1 = Attribute (Check your Enum definition!)

            if (modeIndex == (int)ValueSource.SourceMode.Constant)
            {
                if (constantProp != null)
                    EditorGUI.PropertyField(valueRect, constantProp, GUIContent.none);
            }
            else // Attribute Mode
            {
                // Draw Attribute Name and potentially Provider Path

                float halfWidth = valueRect.width * 0.5f;
                var attrRect = new Rect(valueRect.x, valueRect.y, halfWidth, valueRect.height);
                var pathRect = new Rect(valueRect.x + halfWidth, valueRect.y, halfWidth, valueRect.height);

                if (attrNameProp != null)
                {
                    // FIX: Use PropertyField directly. 
                    // Since 'attrNameProp' is a SemanticKey, Unity will automatically invoke SemanticKeyDrawer.
                    // This replaces the manual TextField logic that was breaking things.
                    EditorGUI.PropertyField(attrRect, attrNameProp, GUIContent.none);
                }

                // Optional: Show Provider Path count or simplified view
                if (providerPathProp != null)
                {
                    // Draw a label showing path count, or ideally a foldout if space permitted.
                    // For a single line drawer, a label is safest.
                    string pathInfo = providerPathProp.arraySize > 0 ? $" [{providerPathProp.arraySize} Path Nodes]" : " [Local]";
                    EditorGUI.LabelField(pathRect, pathInfo, EditorStyles.miniLabel);
                }
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }
    }
}