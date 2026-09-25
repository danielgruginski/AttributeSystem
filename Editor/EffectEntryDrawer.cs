using ReactiveSolutions.AttributeSystem.Core.Data;
using UnityEditor;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Editor
{
    /// <summary>
    /// Draws an EffectEntry (an effect a status effect applies): the dropdown of effect files and, when none is picked,
    /// the effect written in the status below it.
    /// </summary>
    [CustomPropertyDrawer(typeof(EffectEntry))]
    public class EffectEntryDrawer : PropertyDrawer
    {
        private static readonly GUIContent InlineLabel = new GUIContent("Effect (written here)", "Used when no Effect Id is picked.");

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;
            if (string.IsNullOrEmpty(property.FindPropertyRelative("EffectId").stringValue))
            {
                height += EditorGUIUtility.standardVerticalSpacing + EditorGUI.GetPropertyHeight(property.FindPropertyRelative("Effect"), true);
            }
            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var id = property.FindPropertyRelative("EffectId");
            var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(line, id, label);

            if (string.IsNullOrEmpty(id.stringValue))
            {
                var effect = property.FindPropertyRelative("Effect");
                float y = line.y + line.height + EditorGUIUtility.standardVerticalSpacing;
                EditorGUI.indentLevel++;
                EditorGUI.PropertyField(new Rect(position.x, y, position.width, EditorGUI.GetPropertyHeight(effect, true)), effect, InlineLabel, true);
                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }
    }
}
