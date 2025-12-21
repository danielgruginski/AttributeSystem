using algumacoisaqq.AttributeSystem;
using KiwiGames.AttributeBridges;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(AttributeNameAttribute))]
public class AttributeNameDrawer : PropertyDrawer
{
    // Cache the list so we don't reflect every frame
    private static string[] _attributeOptions;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // 1. Ensure we have the list of options
        if (_attributeOptions == null)
        {
            LoadAttributeNames();
        }

        // 2. Check if the property is actually a string
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.LabelField(position, label.text, "Use [AttributeName] on strings only.");
            return;
        }

        // 3. Find the current value's index in our list
        // Default to index 0 ("None" or first item) if not found.
        int currentIndex = 0;
        string currentString = property.stringValue;

        if (!string.IsNullOrEmpty(currentString))
        {
            for (int i = 0; i < _attributeOptions.Length; i++)
            {
                if (_attributeOptions[i] == currentString)
                {
                    currentIndex = i;
                    break;
                }
            }
        }

        // 4. Draw the Popup (Dropdown)
        // If the current value isn't in the list (e.g. deleted attribute), we can show a warning or custom text.
        // For simplicity, we just use the popup.

        int newIndex = EditorGUI.Popup(position, label.text, currentIndex, _attributeOptions);

        // 5. Save the selection back to the string field
        if (newIndex >= 0 && newIndex < _attributeOptions.Length)
        {
            // If option is "None", save empty string, else save the value
            string selected = _attributeOptions[newIndex];
            property.stringValue = (selected == "None") ? "" : selected;
        }
    }

    private void LoadAttributeNames()
    {
        var options = new List<string>();

        // Add a "None" option for clearing the field
        options.Add("None");

        // Use Reflection to get all public const strings from AttributeNames
        FieldInfo[] fields = typeof(AttributeDefinition).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

        foreach (FieldInfo field in fields)
        {
            if (field.IsLiteral && !field.IsInitOnly && field.FieldType == typeof(string))
            {
                // We use GetRawConstantValue to avoid initializing the class if not needed, 
                // but GetValue(null) is fine for constants too.
                string value = (string)field.GetValue(null);
                options.Add(value);
            }
        }

        _attributeOptions = options.ToArray();
    }
}