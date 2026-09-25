using UnityEditor;
using ReactiveSolutions.AttributeSystem.Core.Data;

namespace ReactiveSolutions.AttributeSystem.Editor
{
    /// <summary>
    /// Draws a dropdown of the StatBlock JSON files under Resources/Data/StatBlocks.
    /// Supports both the [StatBlockID] attribute on strings AND fields of type StatBlockID struct.
    /// </summary>
    [CustomPropertyDrawer(typeof(StatBlockIDAttribute))]
    [CustomPropertyDrawer(typeof(StatBlockID))]
    public class StatBlockIDDrawer : JsonIdDrawer
    {
        protected override string Folder => "Resources/" + StatBlockJsonLoader.ResourcesPath;

        protected override SerializedProperty GetIdProperty(SerializedProperty property) =>
            property.type == nameof(StatBlockID) ? property.FindPropertyRelative("ID") : property;
    }
}