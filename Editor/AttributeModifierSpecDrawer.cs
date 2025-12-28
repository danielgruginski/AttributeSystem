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
        private float Spacing => EditorGUIUtility.standardVerticalSpacing;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float h = 0;

            // 1. Header "Modifier Spec"
            h += LineH + Spacing;

            // 2. Logic Type (SemanticKey)
            var logicProp = property.FindPropertyRelative("LogicType");
            h += (logicProp != null ? EditorGUI.GetPropertyHeight(logicProp) : LineH) + Spacing;

            // 3. Target Section
            var targetProp = property.FindPropertyRelative("TargetAttribute");
            h += (targetProp != null ? EditorGUI.GetPropertyHeight(targetProp) : LineH) + Spacing;

            var targetPathProp = property.FindPropertyRelative("TargetPath");
            if (targetPathProp != null)
                h += EditorGUI.GetPropertyHeight(targetPathProp, true) + Spacing;

            // 4. Priority & Type
            h += LineH + Spacing;

            // 5. Arguments (Always Expanded)
            h += LineH + Spacing; // Header "Parameters"

            var argsProp = property.FindPropertyRelative("Arguments");
            if (argsProp != null)
            {
                // To safely calculate height, we need to know the *intended* size.
                // We peek at the LogicType to see what it *should* be.
                var logicTypeProp = property.FindPropertyRelative("LogicType");
                string currentLogic = "Static";
                if (logicTypeProp != null)
                {
                    var logicValueProp = logicTypeProp.FindPropertyRelative("_value");
                    if (logicValueProp != null) currentLogic = logicValueProp.stringValue;
                }
                if (string.IsNullOrEmpty(currentLogic)) currentLogic = "Static";

                string[] paramNames = ModifierFactory.GetParameterNames(currentLogic);
                int expectedSize = paramNames != null ? paramNames.Length : 0;

                // We use the MAX of current or expected to reserve enough space to avoid overlap
                // if resizing hasn't happened yet. OnGUI will handle the actual resize.
                // This prevents "next element draws on top of list" during the resize frame.
                int displayCount = Mathf.Max(argsProp.arraySize, expectedSize);

                for (int i = 0; i < displayCount; i++)
                {
                    // If the element exists, measure it. If not (virtual), assume default height.
                    if (i < argsProp.arraySize)
                    {
                        var element = argsProp.GetArrayElementAtIndex(i);
                        h += (element != null ? EditorGUI.GetPropertyHeight(element, true) : LineH) + Spacing;
                    }
                    else
                    {
                        h += LineH + Spacing; // Fallback for pending elements
                    }
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

            // --- 2. Logic Type (SemanticKey) ---
            var logicTypeProp = property.FindPropertyRelative("LogicType");
            string currentLogic = "Static";

            if (logicTypeProp != null)
            {
                float logicH = EditorGUI.GetPropertyHeight(logicTypeProp);

                EditorGUI.BeginChangeCheck();
                // This invokes SemanticKeyDrawer (Dropdown) automatically
                EditorGUI.PropertyField(NextRect(logicH), logicTypeProp, new GUIContent("Logic Operation"));

                if (EditorGUI.EndChangeCheck())
                {
                    // CRITICAL FIX: Flush changes immediately so we can read the new value below
                    property.serializedObject.ApplyModifiedProperties();
                    // Force update of the SerializedObject we are drawing to reflect the change in this frame
                    property.serializedObject.Update();
                }

                // Read the string value from the inner field to drive the parameter list
                var logicValueProp = logicTypeProp.FindPropertyRelative("_value");
                if (logicValueProp != null)
                {
                    currentLogic = logicValueProp.stringValue;
                }
                if (string.IsNullOrEmpty(currentLogic)) currentLogic = "Static";
            }

            // --- 3. Target Section ---
            var targetProp = property.FindPropertyRelative("TargetAttribute");
            if (targetProp != null)
                EditorGUI.PropertyField(NextRect(EditorGUI.GetPropertyHeight(targetProp)), targetProp);

            var targetPathProp = property.FindPropertyRelative("TargetPath");
            if (targetPathProp != null)
            {
                EditorGUI.PropertyField(NextRect(EditorGUI.GetPropertyHeight(targetPathProp, true)), targetPathProp, new GUIContent("Target Path"), true);
            }

            // --- 4. Configuration Row (Type | Priority) ---
            var priorityProp = property.FindPropertyRelative("Priority");
            var modTypeProp = property.FindPropertyRelative("Type");

            if (priorityProp != null && modTypeProp != null)
            {
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
            }

            // --- 5. Arguments List (Fixed / Always Expanded) ---
            var argsProp = property.FindPropertyRelative("Arguments");
            if (argsProp != null)
            {
                string[] paramNames = ModifierFactory.GetParameterNames(currentLogic);
                if (paramNames == null) paramNames = new string[0];

                // --- SAFE RESIZING LOGIC ---
                if (argsProp.arraySize != paramNames.Length)
                {
                    argsProp.arraySize = paramNames.Length;

                    // We apply properties to commit the size change.
                    property.serializedObject.ApplyModifiedProperties();
                    property.serializedObject.Update();

                    // Note: We do NOT exit here. We continue drawing with the new size.
                    // Since GetPropertyHeight reserved space for the *max* of old/new size, 
                    // we should have enough room to draw without overlap.
                }

                EditorGUI.LabelField(NextRect(LineH), "Parameters", EditorStyles.boldLabel);

                EditorGUI.indentLevel++;
                for (int i = 0; i < argsProp.arraySize; i++)
                {
                    // Safety break
                    if (i >= paramNames.Length) break;

                    var element = argsProp.GetArrayElementAtIndex(i);
                    if (element != null)
                    {
                        string paramLabel = paramNames[i];
                        float elHeight = EditorGUI.GetPropertyHeight(element, true);

                        // We use PropertyField, which invokes ValueSourceSpecDrawer
                        // This uses the specific label from paramNames instead of "Element X"
                        EditorGUI.PropertyField(NextRect(elHeight), element, new GUIContent(paramLabel), true);
                    }
                }
                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }
    }
}