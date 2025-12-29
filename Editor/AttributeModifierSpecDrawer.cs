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

            // 1. Header
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

            // 5. Arguments List
            var argsProp = property.FindPropertyRelative("Arguments");
            if (argsProp != null)
            {
                // Calculate height based on CURRENT array size. 
                // Do not guess or resize here. Trust the serialized data.
                int arraySize = argsProp.arraySize;

                // Header "Parameters"
                h += LineH + Spacing;

                for (int i = 0; i < arraySize; i++)
                {
                    var element = argsProp.GetArrayElementAtIndex(i);
                    h += (element != null ? EditorGUI.GetPropertyHeight(element, true) : LineH) + Spacing;
                }
            }

            return h + 10; // Padding
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            // --- Background Box ---
            GUI.Box(position, GUIContent.none, EditorStyles.helpBox);

            Rect contentPos = new Rect(position.x + 5, position.y + 5, position.width - 10, position.height - 10);
            float currentY = contentPos.y;

            Rect NextRect(float height)
            {
                Rect r = new Rect(contentPos.x, currentY, contentPos.width, height);
                currentY += height + Spacing;
                return r;
            }

            // --- 1. Header ---
            EditorGUI.LabelField(NextRect(LineH), "Modifier Spec", EditorStyles.boldLabel);

            // --- 2. Logic Type ---
            var logicTypeProp = property.FindPropertyRelative("LogicType");
            string currentLogic = "Static";

            if (logicTypeProp != null)
            {
                float logicH = EditorGUI.GetPropertyHeight(logicTypeProp);

                EditorGUI.BeginChangeCheck();
                EditorGUI.PropertyField(NextRect(logicH), logicTypeProp, new GUIContent("Logic Operation"));
                if (EditorGUI.EndChangeCheck())
                {
                    property.serializedObject.ApplyModifiedProperties();
                    property.serializedObject.Update();
                }

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

            // --- 4. Configuration Row ---
            var priorityProp = property.FindPropertyRelative("Priority");
            var modTypeProp = property.FindPropertyRelative("Type");

            if (priorityProp != null && modTypeProp != null)
            {
                Rect rowRect = NextRect(LineH);
                float colW = rowRect.width / 2f;

                Rect typeRect = new Rect(rowRect.x, rowRect.y, colW - 2, rowRect.height);
                Rect priRect = new Rect(rowRect.x + colW + 2, rowRect.y, colW - 2, rowRect.height);

                float oldLabelW = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 40;
                EditorGUI.PropertyField(typeRect, modTypeProp, new GUIContent("Type"));
                EditorGUIUtility.labelWidth = 30;
                EditorGUI.PropertyField(priRect, priorityProp, new GUIContent("Pri"));
                EditorGUIUtility.labelWidth = oldLabelW;
            }

            // --- 5. Arguments List ---
            var argsProp = property.FindPropertyRelative("Arguments");
            if (argsProp != null)
            {
                string[] paramNames = ModifierFactory.GetParameterNames(currentLogic);
                if (paramNames == null) paramNames = new string[0];

                // Check for resize need
                if (argsProp.arraySize != paramNames.Length)
                {
                    // Resize immediately and Apply.
                    argsProp.arraySize = paramNames.Length;
                    property.serializedObject.ApplyModifiedProperties();

                    // Exit to let the next repaint handle drawing with correct sizes.
                    // This prevents "Invalid GUILayout state" from mismatching array sizes.
                    EditorGUI.EndProperty();
                    return;
                }

                EditorGUI.LabelField(NextRect(LineH), "Parameters", EditorStyles.boldLabel);

                EditorGUI.indentLevel++;
                for (int i = 0; i < argsProp.arraySize; i++)
                {
                    if (i >= paramNames.Length) break;

                    var element = argsProp.GetArrayElementAtIndex(i);
                    if (element != null)
                    {
                        string paramLabel = paramNames[i];
                        float elHeight = EditorGUI.GetPropertyHeight(element, true);
                        EditorGUI.PropertyField(NextRect(elHeight), element, new GUIContent(paramLabel), true);
                    }
                }
                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }
    }
}