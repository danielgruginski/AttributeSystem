using UnityEditor;
using UnityEngine;
using ReactiveSolutions.AttributeSystem;

namespace ReactiveSolutions.AttributeSystem.Editor
{
    [CustomPropertyDrawer(typeof(ValueSourceSpec))]
    public class ValueSourceSpecDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            // We squeeze everything into one line for compactness
            return EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var typeProp = property.FindPropertyRelative("Type");
            var constProp = property.FindPropertyRelative("ConstantValue");
            var attrProp = property.FindPropertyRelative("AttributeName");

            // Layout: Label | Dropdown (30%) | Value Field (Remaining)

            // 1. Label
            Rect labelRect = new Rect(position.x, position.y, EditorGUIUtility.labelWidth, position.height);
            EditorGUI.LabelField(labelRect, label);

            // Calculate remaining width
            float contentX = position.x + EditorGUIUtility.labelWidth;
            float contentWidth = position.width - EditorGUIUtility.labelWidth;

            // 2. Type Dropdown
            float typeWidth = contentWidth * 0.35f;
            Rect typeRect = new Rect(contentX, position.y, typeWidth, position.height);
            EditorGUI.PropertyField(typeRect, typeProp, GUIContent.none);

            // 3. Value Field (Context Sensitive)
            float valueX = contentX + typeWidth + 5f;
            float valueWidth = contentWidth - typeWidth - 5f;
            Rect valueRect = new Rect(valueX, position.y, valueWidth, position.height);

            int typeIndex = typeProp.enumValueIndex; // 0 = Constant, 1 = Attribute (based on ValueSource.SourceType enum)

            if (typeIndex == 0) // Constant
            {
                EditorGUI.PropertyField(valueRect, constProp, GUIContent.none);
            }
            else // Attribute
            {
                // You could use your existing StatBlockIDDrawer logic here if you wanted a dropdown of attributes!
                // For now, a text field is safe.
                string currentVal = attrProp.stringValue;
                string newVal = EditorGUI.TextField(valueRect, currentVal);
                if (newVal != currentVal)
                {
                    attrProp.stringValue = newVal;
                }

                if (string.IsNullOrEmpty(attrProp.stringValue))
                {
                    // Placeholder hint
                    GUIColorScope(Color.gray, () =>
                    {
                        EditorGUI.LabelField(valueRect, "Attribute Name");
                    });
                }
            }

            EditorGUI.EndProperty();
        }

        private void GUIColorScope(Color color, System.Action action)
        {
            Color c = GUI.color;
            GUI.color = color;
            action();
            GUI.color = c;
        }
    }
}