using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ReactiveSolutions.AttributeSystem;
using ReactiveSolutions.AttributeSystem.Core.Data;

namespace ReactiveSolutions.AttributeSystem.Editor
{

    /// <summary>
    /// Draws the StatBlockID struct as a dropdown menu of available JSON files.
    /// </summary>
    [CustomPropertyDrawer(typeof(StatBlockID))]
    public class StatBlockIDDrawer : PropertyDrawer
    {
        private const string JSON_SUB_PATH = "Resources/Data/StatBlocks";
        private static string[] _availableOptions;

        // We cache the valid options to avoid IO every frame
        private static void CacheOptions(bool force = false)
        {
            // Only refresh if null, empty or forced.
            if (!force && _availableOptions != null && _availableOptions.Length > 0) return;

            string fullPath = Path.Combine(Application.dataPath, JSON_SUB_PATH);
            if (!Directory.Exists(fullPath))
            {
                _availableOptions = new string[] { "Error: Path Not Found" };
                return;
            }

            // Get files and add a "None" or "Empty" option at the start if desired
            var files = Directory.GetFiles(fullPath, "*.json")
                                 .Select(Path.GetFileNameWithoutExtension)
                                 .ToList();

            files.Insert(0, "None"); // Optional: Allow clearing the selection
            _availableOptions = files.ToArray();
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // Ensure we have data
            CacheOptions();

            // Find the specific string property inside the struct
            SerializedProperty idProperty = property.FindPropertyRelative("ID");

            if (idProperty == null)
            {
                EditorGUI.LabelField(position, label.text, "Error: ID field not found");
                return;
            }

            string currentID = idProperty.stringValue;
            int currentIndex = 0;

            // Find current index in the list
            // We use a simple loop; for massive lists (1000+ items) this could be optimized, 
            // but for file lists it's fine.
            bool found = false;
            for (int i = 0; i < _availableOptions.Length; i++)
            {
                if (_availableOptions[i] == currentID)
                {
                    currentIndex = i;
                    found = true;
                    break;
                }
            }

            // --- Layout Calculation ---

            // 1. Calculate the Button Rect (Fixed width on the right)
            float refreshButtonWidth = 20f;
            float spacing = 2f;

            // 2. Calculate the Main Content Rect (Full width minus button minus spacing)
            float contentWidth = position.width - refreshButtonWidth - spacing;

            Rect contentRect = new Rect(position.x, position.y, contentWidth, position.height);
            Rect refreshRect = new Rect(position.x + contentWidth + spacing, position.y, refreshButtonWidth, position.height);

            // Visual handling for "Missing" data (e.g. file was deleted)
            if (!found && !string.IsNullOrEmpty(currentID))
            {
                // Draw a warning and a text field so the user can see/fix the broken ID manually
                float warningWidth = 20f;
                Rect warningRect = new Rect(position.x, position.y, warningWidth, position.height);
                Rect fieldRect = new Rect(position.x + warningWidth, position.y, position.width - warningWidth, position.height);

                EditorGUI.LabelField(warningRect, new GUIContent("!", "File not found in Resources"));

                // Allow editing the string manually if the file is missing
                string newValue = EditorGUI.TextField(fieldRect, label, currentID);
                if (newValue != currentID)
                {
                    idProperty.stringValue = newValue;
                }
            }
            else
            {
                // === STANDARD STATE (Dropdown) ===
                int newIndex = EditorGUI.Popup(contentRect, label.text, currentIndex, _availableOptions);

                if (newIndex != currentIndex)
                {
                    string selected = _availableOptions[newIndex];
                    idProperty.stringValue = (selected == "None") ? "" : selected;
                }
            }

            // Draw Refresh Button
            // We use a predefined icon like "Refresh" or standard text
            var refreshIcon = EditorGUIUtility.IconContent("Refresh");
            if (GUI.Button(refreshRect, refreshIcon, EditorStyles.iconButton)) 
            {
                CacheOptions(force: true);
            }
        }
    }
}