using ReactiveSolutions.AttributeSystem.Core.Data;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Unity.Data
{
    /// <summary>
    /// Unity ScriptableObject wrapper for the EntityProfile POCO.
    /// Exposes the data to the Unity Inspector.
    /// </summary>
    [CreateAssetMenu(fileName = "NewEntityProfile", menuName = "Attribute System/Entity Profile")]
    public class EntityProfileSO : ScriptableObject
    {
        [SerializeField]
        public EntityProfile Profile = new EntityProfile();
    }
}