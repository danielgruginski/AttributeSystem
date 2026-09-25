using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Core.Data
{
    /// <summary>
    /// Marker attribute for StatBlock JSON ID fields (string or StatBlockID; on a list it applies to each element).
    /// StatBlockIDDrawer shows it as a dropdown of the JSON files under Resources/Data/StatBlocks.
    /// </summary>
    public class StatBlockIDAttribute : PropertyAttribute
    {
        // This class requires no implementation; its existence is the key.
    }
}