using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Core.Data
{
    /// <summary>
    /// Marker attribute for EntityProfile JSON ID fields (strings; on a list it applies to each element).
    /// EntityProfileIDDrawer shows it as a dropdown of the JSON files under Resources/Data/EntityProfiles.
    /// </summary>
    public class EntityProfileIDAttribute : PropertyAttribute
    {
    }
}