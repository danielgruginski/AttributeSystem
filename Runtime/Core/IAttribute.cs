using SemanticKeys;
using System;
using UniRx;

namespace ReactiveSolutions.AttributeSystem.Core
{
    /// <summary>
    /// Contract for an Attribute (implemented by Attribute).
    /// </summary>
    public interface IAttribute : IDisposable
    {
        SemanticKey Name { get; }

        /// <summary>
        /// The observable final value (Base + Modifiers).
        /// Implements IReadOnlyReactiveProperty to allow both Subscription (Push) and direct Value reading (Pull).
        /// </summary>
        IReadOnlyReactiveProperty<float> ObservableValue { get; }

        /// <summary>
        /// The base value of the attribute.
        /// </summary>
        float BaseValue { get; }

        bool IsDisposed { get; }


        /// <summary>
        /// Sets the base value.
        /// While a pointer is active, the pointer target replaces the base value as the pipeline's
        /// starting point; the base value is kept and used again once the pointer is removed.
        /// </summary>
        void SetBaseValue(float value);

        /// <summary>
        /// Adds a modifier to this attribute.
        /// Returns a disposable to remove it.
        /// </summary>
        IDisposable AddModifier(IAttributeModifier modifier);

        /// <summary>
        /// Explicitly removes a modifier.
        /// </summary>
        void RemoveModifier(IAttributeModifier modifier);
    }
}