using UnityEngine;
using UniRx;
using System;

namespace algumacoisaqq.AttributeSystem
{
    /// <summary>
    /// Base class for all Attribute Modifiers.
    /// Handles boilerplate state, UniRx cleanup, and standard MergeMode logic.
    /// </summary>
    public abstract class BaseAttributeModifier : IAttributeModifier
    {
        public string SourceId { get; protected set; }
        public int Priority { get; protected set; }
        public AttributeMergeMode MergeMode { get; protected set; }

        // Manage subscriptions automatically
        protected readonly CompositeDisposable _disposables = new CompositeDisposable();
        protected Attribute _targetAttribute;

        protected BaseAttributeModifier(string sourceId, int priority, AttributeMergeMode mergeMode)
        {
            SourceId = sourceId;
            Priority = priority;
            MergeMode = mergeMode;
        }

        // --- LIFECYCLE ---

        public virtual void OnAttach(Attribute targetAttribute, AttributeController controller)
        {
            _targetAttribute = targetAttribute;
            // Concrete classes override this to call WatchDependency()
        }

        public virtual void OnDetach()
        {
            _disposables.Clear();
            _targetAttribute = null;
        }

        public void Dispose()
        {
            OnDetach();
            _disposables.Dispose();
        }

        // --- APPLICATION LOGIC ---

        /// <summary>
        /// Template Method: Calculates the raw value based on the concrete implementation,
        /// then merges it with the current value based on MergeMode.
        /// </summary>
        public virtual float Apply(float currentValue, AttributeController controller)
        {
            // 1. Calculate the modification (e.g., return 5 for a +5 sword)
            float calculatedValue = CalculateMagnitude(controller);

            // 2. Merge it
            return MergeMode switch
            {
                AttributeMergeMode.Add => currentValue + calculatedValue,
                AttributeMergeMode.Multiply => currentValue * calculatedValue,
                AttributeMergeMode.Override => calculatedValue,
                _ => currentValue
            };
        }

        /// <summary>
        /// Concrete classes implement the math here. 
        /// E.g., return Input * Coeff + Addend;
        /// </summary>
        protected abstract float CalculateMagnitude(AttributeController controller);

        // --- REACTIVITY HELPERS ---

        /// <summary>
        /// Helper to easily bind this modifier to another attribute's changes.
        /// </summary>
        protected void WatchDependency(AttributeController controller, string dependencyAttributeName)
        {
            if (string.IsNullOrEmpty(dependencyAttributeName)) return;

            // When the dependency changes...
            controller.GetAttributeObservable(dependencyAttributeName)
                .SelectMany(attr => attr.ReactivePropertyAccess)
                //.Skip(1) // Skip initial value to avoid immediate dirty flags during setup
                .Subscribe(_ =>
                {
                    // ...tell the attribute we are modifying to re-evaluate itself.
                    _targetAttribute?.Reevaluate();
                })
                .AddTo(_disposables);
        }

        /// <summary>
        /// Helper for ValueSource structs
        /// </summary>
        protected void WatchDependency(AttributeController controller, ValueSource source)
        {
            if (source.Type == ValueSource.SourceType.Attribute)
            {
                WatchDependency(controller, source.AttributeName);
            }
        }
    }
}