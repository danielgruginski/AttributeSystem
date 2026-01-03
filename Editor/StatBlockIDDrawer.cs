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
    /// Draws a dropdown for StatBlockID strings.
    /// Supports both [StatBlockID] attribute on strings AND fields of type StatBlockID struct.
    /// </summary>
    [CustomPropertyDrawer(typeof(StatBlockIDAttribute))]
    [CustomPropertyDrawer(typeof(StatBlockID))]
    public class StatBlockIDDrawer : PropertyDrawer
    {
        private static string[] _cachedIds;
        private static float _lastCacheTime;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // Handle Struct vs String property
            SerializedProperty idProperty;
            if (property.type == nameof(StatBlockID))
            {
                idProperty = property.FindPropertyRelative("ID");
                // Label handling for structs in lists is automatic usually, but let's be safe
            }
            else
            {
                idProperty = property;
            }

            EditorGUI.BeginProperty(position, label, property);

            // Draw Label
            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

            RefreshCacheIfNeeded();

            int selectedIndex = -1;
            string currentId = idProperty.stringValue;

            // Find current index
            if (!string.IsNullOrEmpty(currentId))
            {
                for (int i = 0; i < _cachedIds.Length; i++)
                {
                    if (_cachedIds[i] == currentId)
                    {
                        selectedIndex = i;
                        break;
                    }
                }
            }

            // Draw Dropdown
            var displayOptions = new List<string>(_cachedIds);
            displayOptions.Insert(0, "None");

            int newIndex = EditorGUI.Popup(position, selectedIndex + 1, displayOptions.ToArray());

            if (newIndex != selectedIndex + 1)
            {
                if (newIndex == 0)
                {
                    idProperty.stringValue = "";
                }
                else
                {
                    idProperty.stringValue = _cachedIds[newIndex - 1];
                }
            }

            EditorGUI.EndProperty();
        }

        private void RefreshCacheIfNeeded()
        {
            if (_cachedIds == null || Time.realtimeSinceStartup - _lastCacheTime > 5f)
            {
                string path = Path.Combine(Application.dataPath, "Resources/Data/StatBlocks");
                if (Directory.Exists(path))
                {
                    string[] files = Directory.GetFiles(path, "*.json");
                    _cachedIds = files.Select(p => Path.GetFileNameWithoutExtension(p)).ToArray();
                }
                else
                {
                    _cachedIds = new string[0];
                }
                _lastCacheTime = Time.realtimeSinceStartup;
            }
        }
    }
}