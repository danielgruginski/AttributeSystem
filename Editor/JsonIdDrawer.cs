using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace ReactiveSolutions.AttributeSystem.Editor
{
    /// <summary>
    /// Base for dropdowns that pick a JSON data file by ID: its path in its data folder without the extension (e.g.
    /// "Weapons/IronSword"), which is what the JSON loaders expect. The dropdown lists the files of every Resources
    /// folder, and slashes show as submenus. Edit opens the picked file in its editor window: a profile's template in
    /// the Entity Profile Editor, a StatBlock in the Stat Block Editor, and so on.
    /// </summary>
    public abstract class JsonIdDrawer : PropertyDrawer
    {
        private const float EditWidth = 38f;
        private static readonly GUIContent EditContent = new GUIContent("Edit", "Open this file in its editor window.");

        /// <summary>The kind of file picked.</summary>
        internal abstract DataKind Kind { get; }

        /// <summary>The string property holding the ID (the property itself, or a field of it).</summary>
        protected virtual SerializedProperty GetIdProperty(SerializedProperty property) => property;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty idProperty = GetIdProperty(property);

            EditorGUI.BeginProperty(position, label, property);

            // Draw Label
            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

            var files = DataFiles.Find(Kind.ResourcesPath);
            string currentId = idProperty.stringValue;

            // Edit, when the ID names a file. It opens once this GUI pass is over, since the file may replace what is being drawn.
            string currentPath = DataFiles.PathOf(Kind.ResourcesPath, currentId);
            if (currentPath != null)
            {
                var button = new Rect(position.xMax - EditWidth, position.y, EditWidth, EditorGUIUtility.singleLineHeight);
                position.width -= EditWidth + 2f;
                if (GUI.Button(button, EditContent, EditorStyles.miniButton))
                {
                    var kind = Kind;
                    EditorApplication.delayCall += () => kind.Open(currentPath);
                }
            }

            var displayOptions = new List<string> { "None" };
            int selectedIndex = 0;
            for (int i = 0; i < files.Count; i++)
            {
                displayOptions.Add(files[i].Id);
                if (files[i].Id == currentId) selectedIndex = i + 1;
            }

            if (!string.IsNullOrEmpty(currentId) && selectedIndex == 0)
            {
                // Keep showing an ID whose file is gone (or not created yet) instead of "None".
                displayOptions.Add($"{currentId} (missing)");
                selectedIndex = displayOptions.Count - 1;
            }

            int newIndex = EditorGUI.Popup(position, selectedIndex, displayOptions.ToArray());

            if (newIndex != selectedIndex)
            {
                if (newIndex == 0) idProperty.stringValue = "";
                else if (newIndex <= files.Count) idProperty.stringValue = files[newIndex - 1].Id;
            }

            EditorGUI.EndProperty();
        }
    }
}
