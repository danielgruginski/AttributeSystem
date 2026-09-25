using UnityEditor;
using UnityEngine;
using ReactiveSolutions.AttributeSystem.Core.Data;

namespace ReactiveSolutions.AttributeSystem.Editor
{
    /// <summary>
    /// Draws a template entry on one line, as its Profile Id dropdown. (A template built in code has no ID and isn't
    /// serialized, so the Inspector never holds one.)
    /// </summary>
    [CustomPropertyDrawer(typeof(TemplateEntry))]
    public class TemplateEntryDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) =>
            EditorGUI.PropertyField(position, property.FindPropertyRelative(nameof(TemplateEntry.ProfileId)), label);

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
            EditorGUI.GetPropertyHeight(property.FindPropertyRelative(nameof(TemplateEntry.ProfileId)), label, true);
    }
}
