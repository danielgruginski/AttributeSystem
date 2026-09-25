using ReactiveSolutions.AttributeSystem.Core.Data;
using System;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Unity.Data
{
    /// <summary>
    /// Unity ScriptableObject wrapper for the StatBlock POCO.
    /// Deprecated: nothing in the package reads it, and it is no longer in the Create menu. Author StatBlocks as
    /// JSON with the Stat Block Editor, or inline on an EntityController's profile. Kept so existing assets still load.
    /// </summary>
    [Obsolete("Author StatBlocks as JSON with the Stat Block Editor (Window > Attribute System) instead. This wrapper will be removed.")]
    public class StatBlockSO : ScriptableObject
    {
        [SerializeField]
        public StatBlock StatBlock = new StatBlock();
    }
}