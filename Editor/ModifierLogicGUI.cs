using System;
using System.Collections.Generic;
using System.Linq;
using ReactiveSolutions.AttributeSystem.Core.Modifiers;
using UnityEditor;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Editor
{
    /// <summary>
    /// Draws a [SerializeReference] ModifierLogic field (a modifier's logic, or a formula): a dropdown of every logic
    /// class in the project (built-in or your own), followed by the chosen logic's fields.
    /// </summary>
    public static class ModifierLogicGUI
    {
        private static readonly GUIContent LogicLabel = new GUIContent("Logic", "Computes the modifier's value. Derive from FormulaLogic or ModifierLogic to add your own.");

        private static Type[] _types;
        private static GUIContent[] _options;

        /// <summary>Every concrete [Serializable] ModifierLogic class with a parameterless constructor, by display name.</summary>
        private static Type[] Types
        {
            get
            {
                if (_types == null) BuildTypeList();
                return _types;
            }
        }

        /// <summary>"None", then the display name of each of <see cref="Types"/>.</summary>
        private static GUIContent[] Options
        {
            get
            {
                if (_options == null) BuildTypeList();
                return _options;
            }
        }

        private static void BuildTypeList()
        {
            _types = TypeCache.GetTypesDerivedFrom<ModifierLogic>()
                .Where(t => !t.IsAbstract && !t.IsGenericTypeDefinition && t.IsDefined(typeof(SerializableAttribute), false)
                            && t.GetConstructor(Type.EmptyTypes) != null)
                .OrderBy(DisplayName)
                .ToArray();

            // Two classes with the same name (in different namespaces) are told apart by their namespace.
            var names = _types.Select(DisplayName).ToList();
            _options = new[] { new GUIContent("None") }
                .Concat(_types.Select((t, i) => new GUIContent(names.Count(n => n == names[i]) > 1 ? $"{names[i]} ({t.Namespace})" : names[i])))
                .ToArray();
        }

        private static string DisplayName(Type type) => ObjectNames.NicifyVariableName(ModifierLogic.GetDisplayName(type));

        /// <summary>The logic's class, from the "AssemblyName Namespace.Class" name Unity stores (null if none or unknown).</summary>
        private static Type CurrentType(SerializedProperty logicProperty)
        {
            string fullTypeName = logicProperty.managedReferenceFullTypename;
            if (string.IsNullOrEmpty(fullTypeName)) return null;

            return Types.FirstOrDefault(t => $"{t.Assembly.GetName().Name} {t.FullName.Replace('+', '/')}" == fullTypeName);
        }

        private static IEnumerable<SerializedProperty> Children(SerializedProperty logicProperty)
        {
            SerializedProperty child = logicProperty.Copy();
            SerializedProperty end = logicProperty.GetEndProperty();
            bool enterChildren = true;
            while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
            {
                yield return child;
                enterChildren = false;
            }
        }

        private static void SetType(SerializedProperty logicProperty, int optionIndex)
        {
            Type type = optionIndex > 0 ? Types[optionIndex - 1] : null;
            logicProperty.managedReferenceValue = type != null ? Activator.CreateInstance(type) : null;
            logicProperty.serializedObject.ApplyModifiedProperties();
        }

        private static int SelectedIndex(SerializedProperty logicProperty)
        {
            Type current = CurrentType(logicProperty);
            return current == null ? 0 : Array.IndexOf(Types, current) + 1;
        }

        private static string MissingTypeMessage(SerializedProperty logicProperty)
        {
            string fullTypeName = logicProperty.managedReferenceFullTypename;
            return !string.IsNullOrEmpty(fullTypeName) && CurrentType(logicProperty) == null
                ? $"Unknown logic class '{fullTypeName}'. Was it renamed or removed?"
                : null;
        }

        // --- Rect-based (property drawers) ----------------------------------------------------------

        /// <summary>The height of the dropdown and, below it, the logic's fields.</summary>
        public static float GetHeight(SerializedProperty logicProperty) =>
            EditorGUIUtility.singleLineHeight + GetFieldsHeight(logicProperty);

        /// <summary>The height of the logic's fields (and of a warning if its class is missing), drawn by <see cref="DrawFields"/>.</summary>
        public static float GetFieldsHeight(SerializedProperty logicProperty)
        {
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            float height = 0f;
            if (MissingTypeMessage(logicProperty) != null) height += spacing + EditorGUIUtility.singleLineHeight * 2;
            foreach (var child in Children(logicProperty))
            {
                height += spacing + EditorGUI.GetPropertyHeight(child, true);
            }
            return height;
        }

        /// <summary>The "Logic" dropdown, then the logic's fields.</summary>
        public static void Draw(Rect position, SerializedProperty logicProperty)
        {
            var row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            DrawTypePopup(row, logicProperty, LogicLabel);
            DrawFields(new Rect(position.x, row.yMax, position.width, position.height - row.height), logicProperty);
        }

        /// <summary>The dropdown of logic classes, on one line. Pass GUIContent.none for no label.</summary>
        public static void DrawTypePopup(Rect position, SerializedProperty logicProperty, GUIContent label)
        {
            int selected = SelectedIndex(logicProperty);
            int chosen = label == null || label == GUIContent.none
                ? EditorGUI.Popup(position, selected, Options)
                : EditorGUI.Popup(position, label, selected, Options);
            if (chosen != selected)
            {
                SetType(logicProperty, chosen);
                GUIUtility.ExitGUI(); // The fields below changed; redraw from scratch.
            }
        }

        /// <summary>The logic's fields, indented (and a warning if its class is missing).</summary>
        public static void DrawFields(Rect position, SerializedProperty logicProperty)
        {
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            var row = new Rect(position.x, position.y, position.width, 0f);

            string missing = MissingTypeMessage(logicProperty);
            if (missing != null)
            {
                row.y += spacing;
                row.height = EditorGUIUtility.singleLineHeight * 2;
                EditorGUI.HelpBox(row, missing, MessageType.Warning);
                row.y += row.height;
            }

            EditorGUI.indentLevel++;
            foreach (var child in Children(logicProperty))
            {
                row.y += spacing;
                row.height = EditorGUI.GetPropertyHeight(child, true);
                EditorGUI.PropertyField(row, child, true);
                row.y += row.height;
            }
            EditorGUI.indentLevel--;
        }

        // --- Layout-based (editor windows) ----------------------------------------------------------

        public static void DrawLayout(SerializedProperty logicProperty)
        {
            int selected = SelectedIndex(logicProperty);
            int chosen = EditorGUILayout.Popup(LogicLabel, selected, Options);
            if (chosen != selected)
            {
                SetType(logicProperty, chosen);
                GUIUtility.ExitGUI(); // The fields below changed; redraw from scratch.
            }

            string missing = MissingTypeMessage(logicProperty);
            if (missing != null) EditorGUILayout.HelpBox(missing, MessageType.Warning);

            EditorGUI.indentLevel++;
            foreach (var child in Children(logicProperty))
            {
                EditorGUILayout.PropertyField(child, true);
            }
            EditorGUI.indentLevel--;
        }
    }
}