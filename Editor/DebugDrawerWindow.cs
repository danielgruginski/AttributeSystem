using UnityEngine;
using UnityEditor;
using ReactiveSolutions.AttributeSystem.Core; // For ValueSource
using System.Collections.Generic;

namespace ReactiveSolutions.AttributeSystem.Editor
{
    public class DebugDrawerWindow : EditorWindow
    {
        public class DebugContainer : ScriptableObject
        {
            public ValueSource SourceA = new ValueSource();
            public ValueSource SourceB = new ValueSource();

            // Adding a list to test list drawing behavior too
            public List<ValueSource> SourceList = new List<ValueSource>();
        }

        private DebugContainer _container;
        private SerializedObject _serializedObject;

        [MenuItem("Window/Attributes/Debug Drawers")]
        public static void ShowWindow()
        {
            GetWindow<DebugDrawerWindow>("Debug Drawers");
        }

        private void OnEnable()
        {
            _container = CreateInstance<DebugContainer>();
            _serializedObject = new SerializedObject(_container);
        }

        private void OnDisable()
        {
            if (_container != null) DestroyImmediate(_container);
        }

        private void OnGUI()
        {
            if (_serializedObject == null || _serializedObject.targetObject == null)
            {
                OnEnable();
            }

            _serializedObject.Update();

            EditorGUILayout.LabelField("Value Source Drawer Debug", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            SerializedProperty propA = _serializedObject.FindProperty("SourceA");
            SerializedProperty propB = _serializedObject.FindProperty("SourceB");
            SerializedProperty propList = _serializedObject.FindProperty("SourceList");

            EditorGUILayout.LabelField("Single Element A:");
            EditorGUILayout.PropertyField(propA);

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Single Element B:");
            EditorGUILayout.PropertyField(propB);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("List Elements:");
            EditorGUILayout.PropertyField(propList, true);

            _serializedObject.ApplyModifiedProperties();
        }
    }
}