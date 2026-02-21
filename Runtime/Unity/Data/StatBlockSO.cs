using ReactiveSolutions.AttributeSystem.Core.Data;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Unity.Data
{
    /// <summary>
    /// Unity ScriptableObject wrapper for the StatBlock POCO.
    /// Exposes the data to the Unity Inspector.
    /// </summary>
    [CreateAssetMenu(fileName = "NewStatBlock", menuName = "Attribute System/Stat Block")]
    public class StatBlockSO : ScriptableObject
    {
        [SerializeField]
        public StatBlock StatBlock = new StatBlock();
    }
}