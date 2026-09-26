using UnityEngine;
using UnityEditor;
using ReactiveSolutions.AttributeSystem.Core.Data;
using SemanticKeys;
using System;
using System.Linq;

namespace ReactiveSolutions.AttributeSystem.Editor
{
    /// <summary>
    /// Creates and edits EntityProfile JSON files (in Resources/Data/EntityProfiles folders), templates included: a
    /// template is a profile that others build on. An EntityController (Profile Id), a template entry or a nested entity
    /// entry picks them by ID; EntityProfileJsonLoader loads them.
    /// </summary>
    public class EntityProfileEditorWindow : JsonDataEditorWindow
    {
        // Bridges the EntityProfile POCO to the Inspector drawers. Only ever created in memory.
        public class EntityProfileContainer : ScriptableObject
        {
            public EntityProfile Data = new EntityProfile();
        }

        [MenuItem("Tools/Attribute System/Entity Profile Editor", false, 1)]
        public static void ShowWindow() => Open();

        internal static EntityProfileEditorWindow Open() => GetWindow<EntityProfileEditorWindow>("Entity Profile Editor");

        protected override string ResourcesPath => EntityProfileJsonLoader.ResourcesPath;
        protected override string DataLabel => "EntityProfile";

        protected override ScriptableObject CreateContainer() => CreateInstance<EntityProfileContainer>();
        protected override object GetData(ScriptableObject container) => ((EntityProfileContainer)container).Data;
        protected override void SetData(ScriptableObject container, object data) => ((EntityProfileContainer)container).Data = (EntityProfile)data;

        protected override string ToJson(object data) => EntityProfileJson.ToJson((EntityProfile)data);
        protected override object FromJson(string json, Func<string, SemanticKey> findKey) => EntityProfileJson.FromJson(json, findKey);

        // Templates and nested entities are shown by Profile Id; a profile written in full in the file would be lost on save.
        protected override string CheckLoadedData(object data)
        {
            var profile = (EntityProfile)data;
            var inline = profile.Templates.Where(entry => entry.Profile != null).Select(entry => $"the template '{entry.Profile.ProfileName}'")
                .Concat(profile.NestedEntities.Where(entry => entry.Profile != null).Select(entry => $"the nested entity '{entry.ProviderKey}'"))
                .ToList();
            return inline.Count == 0
                ? null
                : $"The file writes the profiles of {string.Join(", ", inline)} in full, and this window only shows templates and " +
                  "nested entities by Profile Id. Edit the file in a text editor, or save each of those profiles as its own file " +
                  "and refer to it by ID.";
        }

        private const string TemplatesHelp =
            "A template is a profile that others build on (e.g. Templates/Character), edited here like any profile: open it " +
            "with Open, or click Edit next to it under Templates.";

        protected override void DrawData(SerializedProperty data)
        {
            EditorGUILayout.HelpBox(TemplatesHelp, MessageType.Info);

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