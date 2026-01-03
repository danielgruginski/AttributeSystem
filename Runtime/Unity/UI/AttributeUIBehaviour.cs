using UnityEngine;
using UniRx;
using System;
using SemanticKeys;

namespace ReactiveSolutions.AttributeSystem.Unity.UI
{
    /// <summary>
    /// Base class for any UI component that needs to react to an Attribute.
    /// Encapsulates targeting logic and handles race conditions automatically.
    /// </summary>
    public abstract class AttributeUIBehaviour : MonoBehaviour
    {
        [Header("Targeting")]
        [SerializeField] protected AttributeController _initialController;

        protected readonly ReactiveProperty<AttributeController> _targetController = new();
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
        public void SetController(AttributeController controller)
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

            _targetController
                .Where(controller => controller != null)
                .SelectMany(controller => controller.GetAttributeObservable(attributeName))
                .SelectMany(attribute => attribute.Value)
                .Subscribe(onValueChanged)
                .AddTo(_disposables);
        }

        protected virtual void OnDestroy()
        {
            _disposables.Dispose();
        }
    }
}