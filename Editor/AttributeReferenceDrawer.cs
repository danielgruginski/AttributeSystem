using UnityEditor;
using UnityEngine;
using ReactiveSolutions.AttributeSystem.Core.Data;

namespace ReactiveSolutions.AttributeSystem.Editor
{
    [CustomPropertyDrawer(typeof(AttributeReference))]
    public class AttributeReferenceDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var pathProp = property.FindPropertyRelative("Path");
            float height = EditorGUIUtility.singleLineHeight; // Name line

            if (pathProp.isExpanded)
            {
                height += EditorGUI.GetPropertyHeight(pathProp, true) + EditorGUIUtility.standardVerticalSpacing;
            }
            return height + EditorGUIUtility.standardVerticalSpacing;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var nameProp = property.FindPropertyRelative("Name");
            var pathProp = property.FindPropertyRelative("Path");

            // Line 1: Label + Name + Path Toggle
            Rect line1 = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

            // Draw Foldout Arrow manually for the Path
            Rect foldoutRect = new Rect(line1.x, line1.y, 15, line1.height);
            pathProp.isExpanded = EditorGUI.Foldout(foldoutRect, pathProp.isExpanded, GUIContent.none);

            // Draw the Label and Name Field (Shifted slightly to right to avoid foldout overlap)
            Rect contentRect = new Rect(line1.x + 15, line1.y, line1.width - 15, line1.height);
            EditorGUI.PropertyField(contentRect, nameProp, label);

            // Line 2+: Path List (if expanded)
            if (pathProp.isExpanded)
            {
                float y = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                Rect pathRect = new Rect(position.x, y, position.width, EditorGUI.GetPropertyHeight(pathProp, true));

                EditorGUI.indentLevel++;
                EditorGUI.PropertyField(pathRect, pathProp, new GUIContent("Context Path"), true);
                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }
    }
}