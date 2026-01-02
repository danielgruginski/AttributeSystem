using UnityEngine;
using UnityEditor;
using ReactiveSolutions.AttributeSystem.Unity;
using ReactiveSolutions.AttributeSystem.Core;
using System.Linq;
using System.Collections.Generic;
using SemanticKeys;

namespace ReactiveSolutions.AttributeSystem.Editor
{
    public class AttributeDebuggerWindow : EditorWindow
    {
        private AttributeController _selectedController;
        private Vector2 _scrollPosition;
        private bool _autoRefresh = true;

        // Tag Filter State (for Attributes) - Keeping this for UI organization
        private string _selectedTagFilter = null;
        private List<string> _availableTags = new List<string>();

        [MenuItem("Window/Attribute System/Attribute Debugger")]
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

            if (_selectedController.Processor == null)
            {
                EditorGUILayout.HelpBox("Selected Controller has no Processor initialized.", MessageType.Warning);
                return;
            }
            DrawProcessorTags();
            GUILayout.Space(5);

            DrawAttributesList();

            // Repaint constantly for live updates if requested
            if (_autoRefresh && Application.isPlaying)
            {
                Repaint();
            }
        }

        private void DrawProcessorTags()
        {
            var tags = _selectedController.Processor.Tags;
            if (tags.Count > 0)
            {
                GUILayout.BeginHorizontal("box");
                GUILayout.Label("Entity Tags:", EditorStyles.boldLabel, GUILayout.Width(80));
                foreach (var tag in tags)
                {
                    GUI.backgroundColor = new Color(1f, 0.9f, 0.6f); // Light Yellow for Entity Tags
                    //GUILayout.Label(tag, EditorStyles.helpBox);
                    GUI.backgroundColor = Color.white;
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
        }

        private void DrawControllerSelection()
        {
            GUILayout.BeginHorizontal();

            _selectedController = (AttributeController)EditorGUILayout.ObjectField("Controller", _selectedController, typeof(AttributeController), true);

            if (GUILayout.Button("Find Player", GUILayout.Width(100)))
            {
                var go = GameObject.FindWithTag("Player");
                if (go) _selectedController = go.GetComponent<AttributeController>();
            }

            GUILayout.EndHorizontal();

            _autoRefresh = EditorGUILayout.Toggle("Auto Refresh", _autoRefresh);
        }

        private void DrawAttributesList()
        {
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);

            var processor = _selectedController.Processor;

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
            if (attr.ReactivePropertyAccess != null)
                current = attr.ReactivePropertyAccess.Value;

            GUILayout.Label($"Value: {current:0.##}", EditorStyles.largeLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label($"Base: {attr.BaseValue:0.##}", EditorStyles.miniLabel);
            GUILayout.EndHorizontal();

            // Modifiers Dropdown / List
            if (attr.Modifiers != null)
            {
                EditorGUI.indentLevel++;
                foreach (var mod in attr.Modifiers)
                {
                    DrawModifierEntry(mod, processor: _selectedController.Processor);
                }
                EditorGUI.indentLevel--;
            }

            GUILayout.EndVertical();
        }

        private void DrawModifierEntry(IAttributeModifier mod, AttributeProcessor processor)
        {
            EditorGUILayout.BeginHorizontal();

            // Source ID
            // Assuming we cast to a base class or interface has SourceId? 
            // The interface IAttributeModifier defined in documentation earlier didn't strictly force SourceId prop, 
            // but the concrete classes do. Let's check IAttributeModifier definition from previous context.
            // Documentation says: public interface IAttributeModifier { Type, Priority, GetMagnitude }
            // It missed SourceId in the interface definition in documentation, but ModifierArgs has it.
            // Let's assume standard modifiers have it or we display Type.

            // We'll try to reflect 'SourceId' or just show Type
            var typeName = mod.GetType().Name;
            var opType = mod.Type.ToString();

            // Basic Info
            EditorGUILayout.LabelField($"{opType} ({mod.Priority})", GUILayout.Width(120));
            EditorGUILayout.LabelField(typeName, GUILayout.Width(120));

            // Try to resolve magnitude for debug
            // Note: subscribing inside OnGUI is bad. 
            // We can't easily get the 'current' value from an Observable without a property.
            // So we skip displaying exact modifier magnitude for now unless we cache it.

            // Helper to get SourceId if possible
            string sourceId = "Unknown Source";
            var prop = mod.GetType().GetProperty("SourceId");
            if (prop != null)
            {
                var val = prop.GetValue(mod);
                if (val != null) sourceId = val.ToString();
            }

            EditorGUILayout.LabelField(sourceId);

            EditorGUILayout.EndHorizontal();
        }
    }
}