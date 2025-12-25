using UnityEngine;
using UnityEditor;
using System.Linq;
using ReactiveSolutions.AttributeSystem.Core;

namespace ReactiveSolutions.AttributeSystem.Editor
{
    [CustomPropertyDrawer(typeof(AttributeModifierSpec))]
    public class AttributeModifierSpecDrawer : PropertyDrawer
    {
        private const float LineHeight = 18f;
        private const float VerticalSpacing = 2f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded) return LineHeight;

            // Header + Name + Priority + OperationType + MergeMode
            float height = LineHeight + (LineHeight + VerticalSpacing) * 4;

            // Calculate dynamic height based on the CURRENT schema
            var typeProp = property.FindPropertyRelative("OperationType");
            string opType = typeProp.stringValue;

            // Safety default for height calculation
            if (string.IsNullOrEmpty(opType)) opType = "Constant";

            // Fetch Schema from Registry
            var factory = ModifierFactoryRegistry.Get(opType);
            var schema = factory.GetSchema();

            if (schema.RequiredParams != null)
            {
                // Add height for each required parameter
                height += schema.RequiredParams.Length * (LineHeight + VerticalSpacing);
                height += 8f; // Padding for the box
            }

            return height + 10f; // Bottom padding
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            Rect headerRect = new Rect(position.x, position.y, position.width, LineHeight);
            var nameProp = property.FindPropertyRelative("AttributeName");
            var typeProp = property.FindPropertyRelative("OperationType");

            // Ensure we always have a valid string for display
            string currentOpType = typeProp.stringValue;
            if (string.IsNullOrEmpty(currentOpType))
            {
                currentOpType = "Constant";
                // Optional: Write back to property so the empty string doesn't persist
                // typeProp.stringValue = "Constant"; 
            }

            string headerLabel = $"[{currentOpType}] {nameProp.stringValue}";

            property.isExpanded = EditorGUI.Foldout(headerRect, property.isExpanded, headerLabel, true);

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                float currentY = position.y + LineHeight + VerticalSpacing;

                // 1. Basic Properties
                DrawProp(ref currentY, position.width, nameProp);
                DrawProp(ref currentY, position.width, property.FindPropertyRelative("Priority"));

                // 2. Operation Type Dropdown
                // We use a hardcoded list for safety, or you can expose a public GetKeys() in Registry
                string[] options = { "Constant", "Linear", "Exponential", "Ratio", "DiminishingReturns", "Clamp", "Product", "TriangularBonus" };

                // Find index, defaulting to 0 (Constant) if not found or empty
                int currentIndex = System.Array.FindIndex(options, x => x.Equals(currentOpType, System.StringComparison.OrdinalIgnoreCase));
                if (currentIndex < 0) currentIndex = 0;

                Rect typeRect = new Rect(position.x + 15, currentY, position.width - 15, LineHeight);
                EditorGUI.BeginChangeCheck();
                int newIndex = EditorGUI.Popup(typeRect, "Operation", currentIndex, options);
                if (EditorGUI.EndChangeCheck())
                {
                    typeProp.stringValue = options[newIndex];
                    // AUTO-SYNC: When type changes, we immediately fix the params
                    SyncParams(property.FindPropertyRelative("Params"), options[newIndex]);
                }
                currentY += LineHeight + VerticalSpacing;

                // 3. Merge Mode
                DrawProp(ref currentY, position.width, property.FindPropertyRelative("MergeMode"));

                // 4. Dynamic Parameters
                // KEY FIX: Use currentOpType (which is safe) instead of typeProp.stringValue (which might be empty)
                var factory = ModifierFactoryRegistry.Get(currentOpType);
                var schema = factory.GetSchema();

                if (schema.RequiredParams != null && schema.RequiredParams.Length > 0)
                {
                    var paramsListProp = property.FindPropertyRelative("Params");

                    // Draw Box Background
                    float paramsHeight = schema.RequiredParams.Length * (LineHeight + VerticalSpacing) + 4;
                    GUI.Box(new Rect(position.x + 10, currentY, position.width - 10, paramsHeight), GUIContent.none, EditorStyles.helpBox);
                    currentY += 2;

                    // Loop through the REQUIRED keys (from Schema)
                    foreach (var key in schema.RequiredParams)
                    {
                        SerializedProperty paramProp = FindParamInList(paramsListProp, key);

                        if (paramProp == null)
                        {
                            Rect btnRect = new Rect(position.x + 15, currentY, position.width - 30, LineHeight);
                            if (GUI.Button(btnRect, $"Add Missing Param: {key}"))
                            {
                                AddParam(paramsListProp, key);
                            }
                            currentY += LineHeight + VerticalSpacing;
                        }
                        else
                        {
                            var valueProp = paramProp.FindPropertyRelative("Value");
                            Rect rect = new Rect(position.x + 15, currentY, position.width - 25, LineHeight);
                            EditorGUI.PropertyField(rect, valueProp, new GUIContent(key));
                            currentY += LineHeight + VerticalSpacing;
                        }
                    }
                }

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        // --- HELPERS ---

        private void DrawProp(ref float y, float width, SerializedProperty prop)
        {
            Rect rect = new Rect(15 + 15, y, width - 30, LineHeight); // Indent
            EditorGUI.PropertyField(rect, prop);
            y += LineHeight + VerticalSpacing;
        }

        private SerializedProperty FindParamInList(SerializedProperty list, string key)
        {
            // Safety check if list is somehow null/broken
            if (list == null || list.arraySize < 0) return null;

            // Use Registry normalizer if available, else standard lower
            string normKey = key.ToLowerInvariant().Replace(" ", "");

            for (int i = 0; i < list.arraySize; i++)
            {
                var element = list.GetArrayElementAtIndex(i);
                var nameProp = element.FindPropertyRelative("Name");
                if (nameProp == null) continue; // Skip broken elements

                string existingName = nameProp.stringValue.ToLowerInvariant().Replace(" ", "");
                if (existingName == normKey)
                    return element;
            }
            return null;
        }

        private void SyncParams(SerializedProperty list, string opType)
        {
            var factory = ModifierFactoryRegistry.Get(opType);
            var schema = factory.GetSchema();

            if (schema.RequiredParams == null) return;

            foreach (var req in schema.RequiredParams)
            {
                if (FindParamInList(list, req) == null)
                {
                    AddParam(list, req);
                }
            }
        }

        private void AddParam(SerializedProperty list, string name)
        {
            list.arraySize++;
            var element = list.GetArrayElementAtIndex(list.arraySize - 1);
            element.FindPropertyRelative("Name").stringValue = name;

            var val = element.FindPropertyRelative("Value");
            val.FindPropertyRelative("Type").enumValueIndex = 0; // Constant
            val.FindPropertyRelative("ConstantValue").floatValue = 0f;
            val.FindPropertyRelative("AttributeName").stringValue = "";
        }
    }
}