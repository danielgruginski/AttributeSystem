using UnityEngine;
using UnityEditor;
using ReactiveSolutions.AttributeSystem.Core.Data;

namespace ReactiveSolutions.AttributeSystem.Editor
{
    /// <summary>
    /// Creates and edits EntityProfile JSON files under Resources/Data/EntityProfiles. An EntityController
    /// (Profile Id) or a nested entity entry (Profile Id) picks them by ID; EntityProfileJsonLoader loads them.
    /// </summary>
    public class EntityProfileEditorWindow : JsonDataEditorWindow
    {
        // Bridges the EntityProfile POCO to the Inspector drawers. Only ever created in memory.
        public class EntityProfileContainer : ScriptableObject
        {
            public EntityProfile Data = new EntityProfile();
        }

        [MenuItem("Window/Attribute System/Entity Profile Editor")]
        public static void ShowWindow() => GetWindow<EntityProfileEditorWindow>("Entity Profile Editor");

        protected override string JsonFolder => "Resources/" + EntityProfileJsonLoader.ResourcesPath;
        protected override string DataLabel => "EntityProfile";

        protected override ScriptableObject CreateContainer() => CreateInstance<EntityProfileContainer>();
        protected override object GetData(ScriptableObject container) => ((EntityProfileContainer)container).Data;

        protected override void DrawData(SerializedProperty data)
        {
            // Every field of EntityProfile in declaration order, with its [Header]s and tooltips.
            SerializedProperty property = data.Copy();
            SerializedProperty end = data.GetEndProperty();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren) && !SerializedProperty.EqualContents(property, end))
            {
                EditorGUILayout.PropertyField(property, true);
                enterChildren = false;
            }
        }
    }
}