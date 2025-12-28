using ReactiveSolutions.AttributeSystem.Core;
using ReactiveSolutions.AttributeSystem.Core.Data;
using SemanticKeys;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Editor
{
    [CustomPropertyDrawer(typeof(AttributeModifierSpec))]
    public class AttributeModifierSpecDrawer : PropertyDrawer
    {
        private float LineH => EditorGUIUtility.singleLineHeight;
        private float Spacing => EditorGUIUtility.standardVerticalSpacing+8;

        // Cache for finding real GUIDs from KeyDomains
        private static Dictionary<string, string> _logicGuidCache;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float h = 0;

            // 1. Header "Modifier Spec"
            h += LineH + Spacing;

            // 2. Logic Type Dropdown (MOVED TO TOP)
            h += LineH + Spacing;

            // 3. Target Section (Attribute + Path)
            var targetProp = property.FindPropertyRelative("TargetAttribute");
            h += EditorGUI.GetPropertyHeight(targetProp) + Spacing;

            var targetPathProp = property.FindPropertyRelative("TargetPath");
            if (targetPathProp != null)
            {
                // We always draw the path property (it handles its own expansion)
                h += EditorGUI.GetPropertyHeight(targetPathProp, true) + Spacing;
            }

            // 4. Priority & Type Row
            h += LineH + Spacing;

            // 5. Arguments (Always Expanded)
            h += LineH + Spacing; // Header "Parameters"

            var argsProp = property.FindPropertyRelative("Arguments");
            if (argsProp != null)
            {
                // We draw the elements manually, so we sum their heights
                for (int i = 0; i < argsProp.arraySize; i++)
                {
                    var element = argsProp.GetArrayElementAtIndex(i);
                    h += EditorGUI.GetPropertyHeight(element, true) + Spacing;
                }
            }

            return h + 10; // Extra padding
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            // --- Background Box ---
            GUI.Box(position, GUIContent.none, EditorStyles.helpBox);

            Rect contentPos = new Rect(position.x + 5, position.y + 5, position.width - 10, position.height - 10);
            float currentY = contentPos.y;

            // Helper to grab a rect and advance Y
            Rect NextRect(float height)
            {
                Rect r = new Rect(contentPos.x, currentY, contentPos.width, height);
                currentY += height + Spacing;
                return r;
            }

            // --- 1. Header ---
            EditorGUI.LabelField(NextRect(LineH), "Modifier Spec", EditorStyles.boldLabel);

            // --- 2. Logic Selection (MOVED TO TOP) ---
            var logicTypeProp = property.FindPropertyRelative("LogicType");
            var logicValueProp = logicTypeProp.FindPropertyRelative("_value");
            var logicGuidProp = logicTypeProp.FindPropertyRelative("_guid");

            string currentLogic = logicValueProp.stringValue;
            //if (string.IsNullOrEmpty(currentLogic)) currentLogic = "Static";

            var availableTypes = ModifierFactory.GetAllKeys().ToArray();
            int currentIndex = System.Array.IndexOf(availableTypes, currentLogic);
            if (currentIndex == -1) currentIndex = 0;

            Rect logicRect = NextRect(LineH);

            // Explicitly handle control ID to avoid focus stealing
            int logicControlID = GUIUtility.GetControlID(FocusType.Keyboard, logicRect);
            int newIndex = EditorGUI.Popup(logicRect, "Logic Operation", currentIndex, availableTypes);

            if (newIndex != currentIndex && newIndex >= 0 && newIndex < availableTypes.Length)
            {
                string newSelection = availableTypes[newIndex];
                logicValueProp.stringValue = newSelection;

                // Attempt to find the REAL guid from the project's KeyDomains
                string realGuid = FindGuidForLogic(newSelection);
                logicGuidProp.stringValue = realGuid;
            }

            // --- 3. Target Section ---
            var targetProp = property.FindPropertyRelative("TargetAttribute");
            float targetH = EditorGUI.GetPropertyHeight(targetProp);
            EditorGUI.PropertyField(NextRect(targetH), targetProp);

            var targetPathProp = property.FindPropertyRelative("TargetPath");
            if (targetPathProp != null)
            {
                float pathH = EditorGUI.GetPropertyHeight(targetPathProp, true);
                EditorGUI.PropertyField(NextRect(pathH), targetPathProp, new GUIContent("Target Path"), true);
            }

            // --- 4. Configuration Row (Type | Priority) ---
            var priorityProp = property.FindPropertyRelative("Priority");
            var modTypeProp = property.FindPropertyRelative("Type");

            Rect rowRect = NextRect(LineH);
            float colW = rowRect.width / 2f;

            Rect typeRect = new Rect(rowRect.x, rowRect.y, colW - 2, rowRect.height);
            Rect priRect = new Rect(rowRect.x + colW + 2, rowRect.y, colW - 2, rowRect.height);

            float oldLabelW = EditorGUIUtility.labelWidth;

            // Type
            EditorGUIUtility.labelWidth = 40;
            EditorGUI.PropertyField(typeRect, modTypeProp, new GUIContent("Type"));

            // Priority
            EditorGUIUtility.labelWidth = 30;
            EditorGUI.PropertyField(priRect, priorityProp, new GUIContent("Pri"));

            EditorGUIUtility.labelWidth = oldLabelW;

            // --- 5. Arguments List (Fixed / Always Expanded) ---
            var argsProp = property.FindPropertyRelative("Arguments");

            // Sync list size to Logic Type requirements
            string[] paramNames = ModifierFactory.GetParameterNames(currentLogic);
            if (argsProp.arraySize != paramNames.Length)
            {
                argsProp.arraySize = paramNames.Length;
            }

            // Header for parameters
            EditorGUI.LabelField(NextRect(LineH), "Parameters", EditorStyles.boldLabel);

            // Draw Elements
            EditorGUI.indentLevel++;
            for (int i = 0; i < argsProp.arraySize; i++)
            {
                var element = argsProp.GetArrayElementAtIndex(i);
                string paramLabel = (i < paramNames.Length) ? paramNames[i] : $"Arg {i}";

                float elHeight = EditorGUI.GetPropertyHeight(element, true);
                EditorGUI.PropertyField(NextRect(elHeight), element, new GUIContent(paramLabel), true);
            }
            EditorGUI.indentLevel--;

            EditorGUI.EndProperty();
        }

        /// <summary>
        /// Scans project KeyDomains to find a matching GUID for the logic name.
        /// Prioritizes domains named 'Modifiers' if multiple matches exist.
        /// </summary>
        private string FindGuidForLogic(string logicName)
        {
            if (_logicGuidCache == null)
            {
                _logicGuidCache = new Dictionary<string, string>();
                // Find all KeyDomain assets
                string[] guids = AssetDatabase.FindAssets("t:KeyDomain");
                foreach (var g in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(g);
                    var domain = AssetDatabase.LoadAssetAtPath<KeyDomain>(path);
                    if (domain != null)
                    {
                        foreach (var key in domain.Keys)
                        {
                            // If we have a collision, prioritize "Modifiers" domain
                            if (_logicGuidCache.ContainsKey(key.Name))
                            {
                                if (domain.DomainName == "Modifiers")
                                {
                                    _logicGuidCache[key.Name] = key.Guid;
                                }
                            }
                            else
                            {
                                _logicGuidCache[key.Name] = key.Guid;
                            }
                        }
                    }
                }
            }

            if (_logicGuidCache.TryGetValue(logicName, out string guid))
            {
                return guid;
            }

            // Fallback: If not in domain, return Empty. 
            // Warning: If you rely on 'IsValid', this will be false. 
            return string.Empty;
        }
    }
}