using UnityEngine;
using UniRx;
using System;
using SemanticKeys;
using ReactiveSolutions.AttributeSystem.Core;

namespace ReactiveSolutions.AttributeSystem.Unity.UI
{
    /// <summary>
    /// Base class for any UI component that needs to react to an Attribute.
    /// Encapsulates targeting logic and handles race conditions automatically.
    /// </summary>
    public abstract class AttributeUIBehaviour : MonoBehaviour
    {
        [Header("Targeting")]
        [SerializeField] protected EntityController _initialController;

        protected readonly ReactiveProperty<EntityController> _targetController = new();
        protected readonly CompositeDisposable _disposables = new();

        protected virtual void Awake()
        {
            if (_initialController != null)
            {
                SetController(_initialController);
            }
        }

        /// <summary>
        /// Injects the controller to be watched.
        /// </summary>
        public void SetController(EntityController controller)
        {
            _targetController.Value = controller;
        }

        /// <summary>
        /// Internalizes the triple-wait logic.
        /// Call this in Awake or Start to bind a specific attribute to a callback.
        /// </summary>
        protected void MonitorAttribute(SemanticKey attributeName, Action<float> onValueChanged)
        {
            Debug.Assert(!string.IsNullOrEmpty(attributeName), $"[AttributeUI] Attribute name is null or empty on {gameObject.name}");

            // Switch-based: retargeting (or clearing) the controller drops the previous entity's subscription.
            _targetController
                .Select(controller => controller != null ? controller.Instance : null)
                .ObserveAttributeValue(attributeName)
                .Subscribe(onValueChanged)
                .AddTo(_disposables);
        }

        protected virtual void OnDestroy()
        {
            _disposables.Dispose();
        }
    }
}