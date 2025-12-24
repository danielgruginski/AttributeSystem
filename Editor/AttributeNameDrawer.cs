using ReactiveSolutions.AttributeSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Editor
{
    [CustomPropertyDrawer(typeof(AttributeNameAttribute))]
    public class AttributeNameDrawer : PropertyDrawer
    {
        private static string[] _cachedNames;
        private static DateTime _lastCacheTime;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.LabelField(position, label.text, "Use [AttributeName] with strings.");
                return;
            }

            string[] names = GetAttributeNames();

            if (names == null || names.Length == 0)
            {
                property.stringValue = EditorGUI.TextField(position, label, property.stringValue);
                return;
            }

            int currentIndex = Array.IndexOf(names, property.stringValue);
            if (currentIndex < 0) currentIndex = 0;

            int newIndex = EditorGUI.Popup(position, label.text, currentIndex, names);
            property.stringValue = names[newIndex];
        }

        private string[] GetAttributeNames()
        {
            // Cache logic to avoid heavy reflection every frame
            if (_cachedNames != null && (DateTime.Now - _lastCacheTime).TotalSeconds < 5)
            {
                return _cachedNames;
            }

            List<string> foundNames = new List<string>();

            // Scan all assemblies for classes marked with [AttributeProvider]
            var types = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch { return Type.EmptyTypes; }
                })
                .Where(t => t.GetCustomAttribute<AttributeProviderAttribute>() != null);

            foreach (var type in types)
            {
                var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                    .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string));

                foreach (var field in fields)
                {
                    foundNames.Add((string)field.GetRawConstantValue());
                }
            }

            _cachedNames = foundNames.OrderBy(s => s).ToArray();
            _lastCacheTime = DateTime.Now;
            return _cachedNames;
        }
    }
}