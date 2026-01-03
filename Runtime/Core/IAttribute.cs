using SemanticKeys;
using System;
using UniRx;

namespace ReactiveSolutions.AttributeSystem.Core
{
    /// <summary>
    /// Contract for an Attribute.
    /// Allows concrete implementation (Attribute) and virtual implementation (PointerAttribute).
    /// </summary>
    public interface IAttribute : IDisposable
    {
        SemanticKey Name { get; }

        /// <summary>
        /// The observable final value (Base + Modifiers).
        /// Implements IReadOnlyReactiveProperty to allow both Subscription (Push) and direct Value reading (Pull).
        /// </summary>
        IReadOnlyReactiveProperty<float> Value { get; }

        /// <summary>
        /// The base value of the attribute.
        /// </summary>
        float BaseValue { get; }

        bool IsDisposed { get; }


        /// <summary>
        /// Sets the base value. 
        /// For pointers, this redirects to the target.
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