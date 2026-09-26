using UnityEngine;
using UnityEditor;
using ReactiveSolutions.AttributeSystem.Unity;
using ReactiveSolutions.AttributeSystem.Core;
using ReactiveSolutions.AttributeSystem.Core.Modifiers;
using System.Linq;
using System.Collections.Generic;
using SemanticKeys;

namespace ReactiveSolutions.AttributeSystem.Editor
{
    public class AttributeDebuggerWindow : EditorWindow
    {
        private EntityController _selectedController;
        private Vector2 _scrollPosition;
        private bool _autoRefresh = true;

        [MenuItem("Tools/Attribute System/Attribute Debugger", false, 20)]
        public static void ShowWindow()
        {
            GetWindow<AttributeDebuggerWindow>("Attribute Debugger");
        }

        private void OnGUI()
        {
            GUILayout.Label("Attribute System Debugger", EditorStyles.boldLabel);

            DrawControllerSelection();

            GUILayout.Space(10);

            if (_selectedController == null)
            {
                EditorGUILayout.HelpBox("Select an Attribute Controller to inspect.", MessageType.Info);
                return;
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Debugger only works in Play Mode.", MessageType.Warning);
                // We can technically inspect in Editor mode if the Processor is initialized, 
                // but usually it inits on Awake/Start.
                return;
            }

            if (_selectedController.Instance == null)
            {
                EditorGUILayout.HelpBox("Selected Controller has no Processor initialized.", MessageType.Warning);
                return;
            }
            DrawProcessorTags();
            GUILayout.Space(5);

            DrawAttributesList();
        }

        // Called ~10 times per second; repainting from OnGUI itself would redraw every editor frame.
        private void OnInspectorUpdate()
        {
            if (_autoRefresh && Application.isPlaying && _selectedController != null)
            {
                Repaint();
            }
        }

        private void DrawProcessorTags()
        {
            var tags = _selectedController.Instance.Tags;
            if (tags.Count > 0)
            {
                GUILayout.BeginHorizontal("box");
                GUILayout.Label("Entity Tags:", EditorStyles.boldLabel, GUILayout.Width(80));
                foreach (var tag in tags)
                {
                    GUI.backgroundColor = new Color(1f, 0.9f, 0.6f); // Light Yellow for Entity Tags
                    // Value is the tag's reference count (how many sources applied it).
                    GUILayout.Label(tag.Value > 1 ? $"{tag.Key} x{tag.Value}" : tag.Key.ToString(), EditorStyles.helpBox);
                    GUI.backgroundColor = Color.white;
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
        }

        private void DrawControllerSelection()
        {
            GUILayout.BeginHorizontal();

            _selectedController = (EntityController)EditorGUILayout.ObjectField("Controller", _selectedController, typeof(EntityController), true);

            if (GUILayout.Button("Find Player", GUILayout.Width(100)))
            {
                var go = GameObject.FindWithTag("Player");
                if (go) _selectedController = go.GetComponent<EntityController>();
            }

            GUILayout.EndHorizontal();

            _autoRefresh = EditorGUILayout.Toggle("Auto Refresh", _autoRefresh);
        }

        private void DrawAttributesList()
        {
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);

            var processor = _selectedController.Instance;

            // Snapshot of keys to avoid modification errors during iteration
            // We use the raw dictionary from the processor
            var attributes = processor.Attributes;

            if (attributes.Count == 0)
            {
                GUILayout.Label("No Attributes found.");
            }
            else
            {
                foreach (var kvp in attributes)
                {
                    DrawAttributeEntry(kvp.Key, kvp.Value);
                }
            }

            GUILayout.EndScrollView();
        }

        private void DrawAttributeEntry(SemanticKey key, Attribute attr)
        {
            GUILayout.BeginVertical("box");

            // Header: Name and Value
            GUILayout.BeginHorizontal();
            GUILayout.Label(key.ToString(), EditorStyles.boldLabel, GUILayout.Width(150));

            // Live Value
            float current = 0f;
            if (attr.ObservableValue != null)
                current = attr.ObservableValue.Value;

            GUILayout.Label($"Value: {current:0.##}", EditorStyles.largeLabel);
            GUILayout.FlexibleSpace();
            var pointer = attr.ActivePointerTarget;
            if (pointer.HasValue)
            {
                var path = pointer.Value.Path != null && pointer.Value.Path.Count > 0
                    ? string.Join(".", pointer.Value.Path) + "."
                    : string.Empty;
                GUILayout.Label($"-> {path}{pointer.Value.Name}", EditorStyles.miniLabel);
            }
            GUILayout.Label($"Base: {attr.BaseValue:0.##}", EditorStyles.miniLabel);
            GUILayout.EndHorizontal();

            // Modifiers Dropdown / List
            if (attr.Modifiers != null)
            {
                EditorGUI.indentLevel++;
                foreach (var mod in attr.Modifiers)
                {
                    DrawModifierEntry(mod, processor: _selectedController.Instance);
                }
                EditorGUI.indentLevel--;
            }

            GUILayout.EndVertical();
        }

        private void DrawModifierEntry(IAttributeModifier mod, Entity processor)
        {
            EditorGUILayout.BeginHorizontal();

            // For modifiers from StatBlocks (or LogicModifier), the logic class says more than the wrapper's.
            var typeName = mod is LogicModifier logicModifier
                ? ModifierLogic.GetDisplayName(logicModifier.Logic.GetType())
                : mod.GetType().Name;
            var opType = mod.Type.ToString();

            // Basic Info
            EditorGUILayout.LabelField($"{opType} ({mod.Priority})", GUILayout.Width(120));
            EditorGUILayout.LabelField(typeName, GUILayout.Width(120));

            // Try to resolve magnitude for debug
            // Note: subscribing inside OnGUI is bad. 
            // We can't easily get the 'current' value from an Observable without a property.
            // So we skip displaying exact modifier magnitude for now unless we cache it.

            // SourceId is part of IAttributeModifier (some modifiers implement it explicitly, so no reflection).
            string sourceId = string.IsNullOrEmpty(mod.SourceId) ? "Unknown Source" : mod.SourceId;

            EditorGUILayout.LabelField(sourceId);

            EditorGUILayout.EndHorizontal();
        }
    }
}