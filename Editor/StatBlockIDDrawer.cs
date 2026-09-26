using UnityEditor;
using ReactiveSolutions.AttributeSystem.Core.Data;

namespace ReactiveSolutions.AttributeSystem.Editor
{
    /// <summary>
    /// Draws a dropdown of the StatBlock JSON files (in every Resources/Data/StatBlocks folder), with Edit to open the
    /// picked one in the Stat Block Editor. Supports both the [StatBlockID] attribute on strings AND fields of type
    /// StatBlockID struct.
    /// </summary>
    [CustomPropertyDrawer(typeof(StatBlockIDAttribute))]
    [CustomPropertyDrawer(typeof(StatBlockID))]
    public class StatBlockIDDrawer : JsonIdDrawer
    {
        internal override DataKind Kind => DataKind.StatBlock;

        protected override SerializedProperty GetIdProperty(SerializedProperty property) =>
            property.type == nameof(StatBlockID) ? property.FindPropertyRelative("ID") : property;
    }
}