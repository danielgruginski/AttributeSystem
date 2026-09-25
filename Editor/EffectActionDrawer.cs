using ReactiveSolutions.AttributeSystem.Core.Data;
using UnityEditor;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Editor
{
    /// <summary>
    /// Draws an EffectAction in a box: its target (with its path, which starts with Source or Target), Type, Chance
    /// and Condition, then the Logic dropdown and the chosen logic's fields.
    /// </summary>
    [CustomPropertyDrawer(typeof(EffectAction))]
    public class EffectActionDrawer : PropertyDrawer
    {
        private const float Padding = 5f;

        private static float LineH => EditorGUIUtility.singleLineHeight;
        private static float Spacing => EditorGUIUtility.standardVerticalSpacing;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var target = property.FindPropertyRelative("Target");
            ExpandPath(target);

            float h = Padding * 2;
            h += LineH + Spacing; // Header
            h += EditorGUI.GetPropertyHeight(target, true) + Spacing;
            h += LineH + Spacing; // Type
            h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("Chance"), true) + Spacing;
            h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("Condition"), true) + Spacing;
            h += ModifierLogicGUI.GetHeight(property.FindPropertyRelative("Logic"));
            return h;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            GUI.Box(position, GUIContent.none, EditorStyles.helpBox);

            var content = new Rect(position.x + Padding, position.y + Padding, position.width - Padding * 2, position.height - Padding * 2);
            float y = content.y;

            Rect Next(float height)
            {
                var r = new Rect(content.x, y, content.width, height);
                y += height + Spacing;
                return r;
            }

            EditorGUI.LabelField(Next(LineH), string.IsNullOrEmpty(label.text) ? "Action" : label.text, EditorStyles.boldLabel);

            var target = property.FindPropertyRelative("Target");
            ExpandPath(target);
            EditorGUI.PropertyField(Next(EditorGUI.GetPropertyHeight(target, true)), target, new GUIContent("Target", target.tooltip), true);

            EditorGUI.PropertyField(Next(LineH), property.FindPropertyRelative("Type"));

            var chance = property.FindPropertyRelative("Chance");
            EditorGUI.PropertyField(Next(EditorGUI.GetPropertyHeight(chance, true)), chance, true);

            var condition = property.FindPropertyRelative("Condition");
            EditorGUI.PropertyField(Next(EditorGUI.GetPropertyHeight(condition, true)), condition, true);

            var logic = property.FindPropertyRelative("Logic");
            ModifierLogicGUI.Draw(Next(ModifierLogicGUI.GetHeight(logic)), logic);

            EditorGUI.EndProperty();
        }

        /// <summary>Shows an AttributeReference's path: in an effect it names the entity (Source or Target).</summary>
        internal static void ExpandPath(SerializedProperty attributeReference)
        {
            var path = attributeReference?.FindPropertyRelative("Path");
            if (path != null) path.isExpanded = true;
        }
    }

    /// <summary>Draws an EffectCost: the resource (with its path, which starts with Source or Target) and the amount.</summary>
    [CustomPropertyDrawer(typeof(EffectCost))]
    public class EffectCostDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var resource = property.FindPropertyRelative("Resource");
            EffectActionDrawer.ExpandPath(resource);
            return EditorGUI.GetPropertyHeight(resource, true) + EditorGUIUtility.standardVerticalSpacing +
                   EditorGUI.GetPropertyHeight(property.FindPropertyRelative("Amount"), true);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var resource = property.FindPropertyRelative("Resource");
            EffectActionDrawer.ExpandPath(resource);
            float resourceHeight = EditorGUI.GetPropertyHeight(resource, true);
            EditorGUI.PropertyField(new Rect(position.x, position.y, position.width, resourceHeight), resource,
                new GUIContent("Resource", resource.tooltip), true);

            var amount = property.FindPropertyRelative("Amount");
            float y = position.y + resourceHeight + EditorGUIUtility.standardVerticalSpacing;
            EditorGUI.PropertyField(new Rect(position.x, y, position.width, EditorGUI.GetPropertyHeight(amount, true)), amount, true);

            EditorGUI.EndProperty();
        }
    }
}
