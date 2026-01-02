using ReactiveSolutions.AttributeSystem.Core;
using SemanticKeys;
using UniRx;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Unity.UI
{
    /// <summary>
    /// Enables or disables the target GameObject based on whether a specific Tag is present on the AttributeController.
    /// Useful for showing status icons (e.g. "Stunned", "Silenced") or other visual indicators.
    /// </summary>
    [AddComponentMenu("Attribute System/UI/Tag Active Toggle")]
    public class AttributeTagActiveUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private AttributeController _sourceController;
        [Tooltip("The GameObject to enable/disable.")]
        [SerializeField] private GameObject _targetObject;

        [Header("Settings")]
        [SerializeField] private SemanticKey _tag;
        [Tooltip("If true, the object is enabled when the tag is present. If false, it is disabled.")]
        [SerializeField] private bool _activeWhenTagPresent = true;

        private CompositeDisposable _disposables = new CompositeDisposable();

        private void Awake()
        {
            if (_targetObject == this.gameObject) 
            {
                Debug.LogWarning($"{nameof(AttributeTagActiveUI)} on {gameObject.name}: Target Object is set to self. Will stop wprking once set to Disable by this component", this);
            }
            if (_sourceController == null) _sourceController = GetComponentInParent<AttributeController>();
        }

        private void OnEnable()
        {
            if (_sourceController != null)
            {
                BindToController(_sourceController);
            }
        }

        private void OnDisable()
        {
            _disposables.Clear();
        }

        public void SetSource(AttributeController controller)
        {
            _sourceController = controller;
            if (isActiveAndEnabled)
            {
                BindToController(controller);
            }
        }

        private void BindToController(AttributeController controller)
        {
            _disposables.Clear();

            if (controller == null || controller.Processor == null) return;


            // Observe the tag dictionary directly
            // ReactiveDictionary fires Add/Remove/Replace events.
            // However, we just want to know if the key exists and has count > 0.

            // Initial State
            UpdateState(controller.Processor.HasTag(_tag));

            // Subscribe to dictionary changes
            controller.Processor.Tags.ObserveCountChanged().Subscribe(_ =>
            {
                // This fires on count of KEYS changes, not necessarily the ref count value of a specific key.
                // But ObserveAdd/Remove/Replace covers key existence.
                // We also need to check the INT value (ref count) changes if we care about > 0, 
                // but TagManager removes the key entirely when count hits 0.
                // So checking Key Existence is sufficient with the current TagManager implementation.
                UpdateState(controller.Processor.HasTag(_tag));
            })
            .AddTo(_disposables);

            // Also listen specifically for add/remove of our target tag to be responsive
            controller.Processor.Tags.ObserveAdd().Where(evt => evt.Key == _tag).Subscribe(_ => UpdateState(true)).AddTo(_disposables);
            controller.Processor.Tags.ObserveRemove().Where(evt => evt.Key == _tag).Subscribe(_ => UpdateState(false)).AddTo(_disposables);

            // Note: ObserveReplace isn't strictly needed if removal happens at 0, 
            // but if ref count goes 1->2->1, the key remains.
            // Since we only care if it EXISTS (Count > 0), Add/Remove of the KEY is the trigger.
        }

        private void UpdateState(bool tagExists)
        {
            if (_targetObject == null) return;

            bool shouldBeActive = _activeWhenTagPresent ? tagExists : !tagExists;

            if (_targetObject.activeSelf != shouldBeActive)
            {
                _targetObject.SetActive(shouldBeActive);
            }
        }
    }
}