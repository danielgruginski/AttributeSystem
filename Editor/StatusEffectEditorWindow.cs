using ReactiveSolutions.AttributeSystem.Core.Data;
using SemanticKeys;
using System;
using UnityEditor;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Editor
{
    /// <summary>
    /// Creates and edits StatusEffect JSON files under Resources/Data/StatusEffects (a poison, a blessing, a stun). Game
    /// code loads them with StatusEffectJsonLoader and applies them with StatusEffect.Apply(source, target); effects apply
    /// them by ID; [StatusEffectID] fields pick them.
    /// </summary>
    public class StatusEffectEditorWindow : JsonDataEditorWindow
    {
        // Bridges the StatusEffect POCO to the Inspector drawers. Only ever created in memory.
        public class StatusEffectContainer : ScriptableObject
        {
            public StatusEffect Data = new StatusEffect();
        }

        private const string PathsHelp =
            "The Condition, the Duration and the effects reach attributes through the Source (who applied the status) or the " +
            "Target (who has it): start their paths with one of them. The StatBlock applies to the entity that has the status, " +
            "so its paths are that entity's own.";

        [MenuItem("Window/Attribute System/Status Effect Editor")]
        public static void ShowWindow() => GetWindow<StatusEffectEditorWindow>("Status Effect Editor");

        protected override string JsonFolder => "Resources/" + StatusEffectJsonLoader.ResourcesPath;
        protected override string DataLabel => "StatusEffect";

        protected override ScriptableObject CreateContainer() => CreateInstance<StatusEffectContainer>();
        protected override object GetData(ScriptableObject container) => ((StatusEffectContainer)container).Data;
        protected override void SetData(ScriptableObject container, object data) => ((StatusEffectContainer)container).Data = (StatusEffect)data;

        protected override string ToJson(object data) => StatusEffectJson.ToJson((StatusEffect)data);
        protected override object FromJson(string json, Func<string, SemanticKey> findKey) => StatusEffectJson.FromJson(json, findKey);

        protected override void DrawData(SerializedProperty data)
        {
            EditorGUILayout.HelpBox(PathsHelp, MessageType.Info);

            // Every field of StatusEffect in declaration order, with its tooltips.
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
