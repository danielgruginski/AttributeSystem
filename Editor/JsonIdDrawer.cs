using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ReactiveSolutions.AttributeSystem.Editor
{
    /// <summary>
    /// Base for dropdowns that pick a JSON data file by ID: its path under a Resources folder without the
    /// extension (e.g. "Weapons/IronSword"), which is what the JSON loaders expect. Slashes show as submenus.
    /// </summary>
    public abstract class JsonIdDrawer : PropertyDrawer
    {
        /// <summary>The folder with the JSON files, relative to Assets (e.g. "Resources/Data/StatBlocks").</summary>
        protected abstract string Folder { get; }

        /// <summary>The string property holding the ID (the property itself, or a field of it).</summary>
        protected virtual SerializedProperty GetIdProperty(SerializedProperty property) => property;

        private static readonly Dictionary<string, (string[] ids, float time)> _cache = new Dictionary<string, (string[], float)>();

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty idProperty = GetIdProperty(property);

            EditorGUI.BeginProperty(position, label, property);

            // Draw Label
            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

            string[] ids = GetIds(Folder);
            string currentId = idProperty.stringValue;

            var displayOptions = new List<string> { "None" };
            displayOptions.AddRange(ids);

            int selectedIndex = 0;
            if (!string.IsNullOrEmpty(currentId))
            {
                int index = Array.IndexOf(ids, currentId);
                if (index >= 0)
                {
                    selectedIndex = index + 1;
                }
                else
                {
                    // Keep showing an ID whose file is gone (or not created yet) instead of "None".
                    displayOptions.Add($"{currentId} (missing)");
                    selectedIndex = displayOptions.Count - 1;
                }
            }

            int newIndex = EditorGUI.Popup(position, selectedIndex, displayOptions.ToArray());

            if (newIndex != selectedIndex)
            {
                if (newIndex == 0) idProperty.stringValue = "";
                else if (newIndex <= ids.Length) idProperty.stringValue = ids[newIndex - 1];
            }

            EditorGUI.EndProperty();
        }

        private static string[] GetIds(string folder)
        {
            if (_cache.TryGetValue(folder, out var cached) && Time.realtimeSinceStartup - cached.time <= 5f)
            {
                return cached.ids;
            }

            string[] ids = new string[0];
            string path = Path.Combine(Application.dataPath, folder);
            if (Directory.Exists(path))
            {
                string root = Path.GetFullPath(path).Replace('\\', '/').TrimEnd('/') + "/";
                ids = Directory.GetFiles(path, "*.json", SearchOption.AllDirectories)
                    .Select(p => Path.GetFullPath(p).Replace('\\', '/').Substring(root.Length))
                    .Select(rel => rel.Substring(0, rel.Length - ".json".Length))
                    .OrderBy(id => id)
                    .ToArray();
            }

            _cache[folder] = (ids, Time.realtimeSinceStartup);
            return ids;
        }
    }
}