using UnityEngine;

namespace algumacoisaqq.AttributeSystem
{
    /// <summary>
    /// Marker attribute used on a List<string> to tell the custom property drawer
    /// (StatBlockIDListDrawer) to handle the field and present available StatBlock JSON IDs.
    /// </summary>
    public class StatBlockIDAttribute : PropertyAttribute
    {
        // This class requires no implementation; its existence is the key.
    }
}