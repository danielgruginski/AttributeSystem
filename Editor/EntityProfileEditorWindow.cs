using UnityEngine;
using UnityEditor;
using ReactiveSolutions.AttributeSystem.Core.Data;
using SemanticKeys;
using System;
using System.Linq;

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
        protected override void SetData(ScriptableObject container, object data) => ((EntityProfileContainer)container).Data = (EntityProfile)data;

        protected override string ToJson(object data) => EntityProfileJson.ToJson((EntityProfile)data);
        protected override object FromJson(string json, Func<string, SemanticKey> findKey) => EntityProfileJson.FromJson(json, findKey);

        // A nested entity is shown by its Profile Id; a nested profile written in full in the file would be lost on save.
        protected override string CheckLoadedData(object data)
        {
            var inline = ((EntityProfile)data).NestedEntities.Where(entry => entry.Profile != null).Select(entry => $"'{entry.ProviderKey}'").ToList();
            return inline.Count == 0
                ? null
                : $"The file writes the profiles of the nested entities {string.Join(", ", inline)} in full, and this window only " +
                  "shows nested entities by Profile Id. Edit the file in a text editor, or save each nested profile as its own file " +
                  "and refer to it by ID.";
        }

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