using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ReactiveSolutions.AttributeSystem;

namespace ReactiveSolutions.AttributeSystem.Editor
{

    /// <summary>
    /// Custom drawer for a List<string> that is marked with StatBlockIDAttribute.
    /// It draws the standard list and adds a button below to select and add a new StatBlock jsonID from JSON files.
    /// </summary>
    [CustomPropertyDrawer(typeof(StatBlockIDAttribute))]
    public class StatBlockIDListDrawer : PropertyDrawer
    {
        private const string JSON_SUB_PATH = "Resources/Data/StatBlocks";
        private static List<string> _availableIDs;
        private static bool _loggedActivation = false;
        private const float ButtonHeight = 20f;
        private const float Padding = 5f;

        private void CacheAvailableIDs()
        {
            if (_availableIDs != null) return;

            _availableIDs = new List<string>();
            // Use Application.dataPath to find assets reliably
            string fullPath = Path.Combine(Application.dataPath, JSON_SUB_PATH);

            if (!Directory.Exists(fullPath))
            {
                Debug.LogWarning($"StatBlock JSON path not found: {fullPath}");
                return;
            }

            _availableIDs.AddRange(
                Directory.GetFiles(fullPath, "*.json")
                    .Select(Path.GetFileNameWithoutExtension)
            );
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            // WARNING: Calling EditorGUI.GetPropertyHeight(property, true) here causes Infinite Recursion
            // because it invokes this Drawer again. We must calculate height manually.

            float height = EditorGUIUtility.singleLineHeight; // Header/Foldout height

            if (property.isExpanded)
            {
                // Add height for "Size" field
                height += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                // Add height for each element
                for (int i = 0; i < property.arraySize; i++)
                {
                    SerializedProperty element = property.GetArrayElementAtIndex(i);
                    height += EditorGUI.GetPropertyHeight(element, true) + EditorGUIUtility.standardVerticalSpacing;
                }
            }

            // Add button height
            height += ButtonHeight + Padding + EditorGUIUtility.standardVerticalSpacing;

            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (!_loggedActivation)
            {
                Debug.Log("StatBlockIDListDrawer activated successfully.");
                _loggedActivation = true;
            }

            // Safety check
            if (!property.isArray)
            {
                EditorGUI.HelpBox(position, "StatBlockIDAttribute should only be used on a List<string>.", MessageType.Error);
                return;
            }

            // --- MANUAL LIST DRAWING ---
            // We draw manually to avoid the recursion issues caused by EditorGUI.PropertyField(property)
            // which triggers the "Multiple Buttons" and layout glitches.

            // 1. Draw Foldout
            Rect headerRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(headerRect, property.isExpanded, label);

            float currentY = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;

                // 2. Draw Size Field
                Rect sizeRect = new Rect(position.x, currentY, position.width, EditorGUIUtility.singleLineHeight);
                int newSize = EditorGUI.IntField(sizeRect, "Size", property.arraySize);
                if (newSize != property.arraySize)
                {
                    property.arraySize = newSize;
                }
                currentY += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                // 3. Draw Elements
                for (int i = 0; i < property.arraySize; i++)
                {
                    SerializedProperty element = property.GetArrayElementAtIndex(i);
                    float elementHeight = EditorGUI.GetPropertyHeight(element, true);

                    Rect elementRect = new Rect(position.x, currentY, position.width, elementHeight);
                    // We pass GUIContent.none or a specific label to avoid duplicating the variable name
                    EditorGUI.PropertyField(elementRect, element, new GUIContent($"Element {i}"), true);

                    currentY += elementHeight + EditorGUIUtility.standardVerticalSpacing;
                }

                EditorGUI.indentLevel--;
            }

            // 4. Draw "Add" Button
            // We position this after the list (expanded or collapsed)
            Rect buttonRect = new Rect(position.x + Padding, currentY + Padding, position.width - 2 * Padding, ButtonHeight);

            CacheAvailableIDs();

            if (GUI.Button(buttonRect, "Add Stat Block jsonID"))
            {
                ShowStatBlockIDMenu(property);
            }
        }

        private void ShowStatBlockIDMenu(SerializedProperty targetListProperty)
        {
            GenericMenu menu = new GenericMenu();

            if (_availableIDs == null || _availableIDs.Count == 0)
            {
                menu.AddItem(new GUIContent("No StatBlocks found. Create some JSON files!"), false, () => { });
            }
            else
            {
                foreach (string id in _availableIDs)
                {
                    menu.AddItem(new GUIContent(id), false, OnIDSelected, new MenuSelectionContext(targetListProperty, id));
                }
            }

            menu.ShowAsContext();
        }

        private void OnIDSelected(object context)
        {
            if (context is MenuSelectionContext selectionContext)
            {
                if (selectionContext.TargetObject == null) return;

                // NUCLEAR FIX for Serialization Glitches:
                // 1. Create a fresh SerializedObject to bypass stale iterators.
                SerializedObject so = new SerializedObject(selectionContext.TargetObject);
                so.Update();

                SerializedProperty listProperty = so.FindProperty(selectionContext.PropertyPath);
                if (listProperty == null)
                {
                    Debug.LogError($"Could not find property at path: {selectionContext.PropertyPath}");
                    return;
                }

                // 2. Resize the array safely
                // Using arraySize++ is more robust than InsertArrayElementAtIndex for simple appends
                listProperty.arraySize++;

                // 3. Force a flush of the size change
                // We write the size change to the object, then re-read it.
                // This ensures the array is physically larger in memory before we try to access index [size-1]
                so.ApplyModifiedProperties();
                so.Update();

                // 4. Access the new element (Last index)
                int newIndex = listProperty.arraySize - 1;
                SerializedProperty newElement = listProperty.GetArrayElementAtIndex(newIndex);

                // 5. Validate Type
                if (newElement.propertyType != SerializedPropertyType.String)
                {
                    Debug.LogError($"[StatBlockIDListDrawer] Type Mismatch: Found '{newElement.propertyType}'. Expected String. Path: {newElement.propertyPath}");
                    // Revert size if it failed
                    listProperty.arraySize--;
                    so.ApplyModifiedProperties();
                    return;
                }

                // 6. Set ReactivePropertyAccess
                newElement.stringValue = selectionContext.JsonID;
                so.ApplyModifiedProperties();
            }
        }

        private class MenuSelectionContext
        {
            public UnityEngine.Object TargetObject { get; }
            public string PropertyPath { get; }
            public string JsonID { get; }

            public MenuSelectionContext(SerializedProperty property, string id)
            {
                TargetObject = property.serializedObject.targetObject;
                PropertyPath = property.propertyPath;
                JsonID = id;
            }
        }
    }
}