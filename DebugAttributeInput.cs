using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace algumacoisaqq.AttributeSystem
{
    public class DebugAttributeInput : MonoBehaviour
    {
        [SerializeField] private KeyCode _toggleKey = KeyCode.F3;

        private void Update()
        {
            if (Input.GetKeyDown(_toggleKey))
            {
                MMEventManager.TriggerEvent(new ToggleDebugAttributesEvent());
            }
        }
    }
}