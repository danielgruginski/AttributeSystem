using SemanticKeys;
using System;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Core
{
    /// <summary>
    /// Represents a persistent link that tries to apply a modifier to a target at the end of a path.
    /// It manages its own lifecycle via Rx subscriptions. 
    /// To kill it, simply call Dispose().
    /// </summary>
    public class AttributeConnection : IDisposable
    {
        private readonly AttributeProcessor _root;
        private readonly List<SemanticKey> _path;
        private readonly SemanticKey _targetAttribute;
        private readonly IAttributeModifier _modifier;
        private readonly string _sourceId;

        // The subscription to the path topology
        private readonly SerialDisposable _pathSubscription = new SerialDisposable();

        // The processor currently holding our modifier (so we can remove it later)
        private AttributeProcessor _currentTarget;

        public string SourceId => _sourceId;

        public AttributeConnection(
            AttributeProcessor root,
            List<SemanticKey> path,
            SemanticKey targetAttribute,
            IAttributeModifier modifier,
            string sourceId)
        {
            _root = root;
            _path = path ?? new List<SemanticKey>();
            _targetAttribute = targetAttribute;
            _modifier = modifier;
            _sourceId = sourceId;

            Connect();
        }

        private void Connect()
        {
            // Recursively observe the path. 
            // The resulting stream emits the 'Final Processor' whenever the path resolves,
            // or 'null' if the path is broken.
            _pathSubscription.Disposable = ResolvePathRecursively(_root, 0)
                .Subscribe(ApplyToTarget);
        }

        private IObservable<AttributeProcessor> ResolvePathRecursively(AttributeProcessor current, int index)
        {
            // Base Case: We reached the end of the list. 'current' is the target.
            if (index >= _path.Count)
                return Observable.Return(current);

            SemanticKey nextKey = _path[index];

            // Recursive Step: Observe the provider at 'nextKey'
            // If that provider changes, we switch to a new branch of recursion.
            return current.ObserveProvider(nextKey)
                .Select(nextProcessor =>
                {
                    if (nextProcessor == null)
                    {
                        // Link broken (e.g. unequipped). Path is dead.
                        return Observable.Return<AttributeProcessor>(null);
                    }
                    // Link exists. Continue traversing.
                    return ResolvePathRecursively(nextProcessor, index + 1);
                })
                .Switch(); // MAGIC: Unsubscribes from old path, subscribes to new path.
        }

        private void ApplyToTarget(AttributeProcessor newTarget)
        {
            // 1. Cleanup Old
            if (_currentTarget != null && _currentTarget != newTarget)
            {
                // We assume the target might have changed, so we remove the modifier from the old one.
                // Note: This relies on the modifier instance being the same.
                var attr = _currentTarget.GetAttribute(_targetAttribute);
                attr?.RemoveModifier(_modifier);
            }

            _currentTarget = newTarget;

            // 2. Apply New
            if (_currentTarget != null)
            {
                // We use the direct local Add (Handle-less overload) because WE are the handle.
                _currentTarget.GetOrCreateAttribute(_targetAttribute).AddModifier(_modifier);
            }
        }

        public void Dispose()
        {
            // Cutting this subscription allows GC to collect this object
            _pathSubscription.Dispose();

            // Final Cleanup of the actual modifier
            if (_currentTarget != null)
            {
                var attr = _currentTarget.GetAttribute(_targetAttribute);
                attr?.RemoveModifier(_modifier);
                _currentTarget = null;
            }
        }
    }
}