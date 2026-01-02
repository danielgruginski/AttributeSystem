using SemanticKeys;
using System;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Core
{
    /// <summary>
    /// Abstract base class for connections that depend on a dynamic provider path.
    /// Handles the recursive resolution of the path and notifying the subclass when the final target changes.
    /// </summary>
    public abstract class PathConnection : IDisposable
    {
        protected readonly AttributeProcessor _root;
        protected readonly List<SemanticKey> _path;

        private readonly SerialDisposable _pathSubscription = new SerialDisposable();
        protected AttributeProcessor _currentTarget;

        protected PathConnection(AttributeProcessor root, List<SemanticKey> path)
        {
            _root = root;
            _path = path ?? new List<SemanticKey>();
        }

        /// <summary>
        /// Starts observing the path. Must be called by the subclass constructor.
        /// </summary>
        protected void Connect()
        {
            // Optimization: If path is empty, we are targeting the root immediately.
            if (_path.Count == 0)
            {
                ApplyToTarget(_root);
                return;
            }

            _pathSubscription.Disposable = ResolvePathRecursively(_root, 0)
                .Subscribe(ApplyToTarget);
        }

        private IObservable<AttributeProcessor> ResolvePathRecursively(AttributeProcessor current, int index)
        {
            if (index >= _path.Count)
                return Observable.Return(current);

            SemanticKey nextKey = _path[index];

            return current.ObserveProvider(nextKey)
                .Select(nextProcessor =>
                {
                    if (nextProcessor == null) return Observable.Return<AttributeProcessor>(null);
                    return ResolvePathRecursively(nextProcessor, index + 1);
                })
                .Switch();
        }

        private void ApplyToTarget(AttributeProcessor newTarget)
        {
            if (_currentTarget != newTarget)
            {
                if (_currentTarget != null)
                {
                    OnRemoveFromTarget(_currentTarget);
                }

                _currentTarget = newTarget;

                if (_currentTarget != null)
                {
                    OnApplyToTarget(_currentTarget);
                }
            }
        }

        /// <summary>
        /// Called when a new valid target is found at the end of the path.
        /// </summary>
        protected abstract void OnApplyToTarget(AttributeProcessor target);

        /// <summary>
        /// Called when the previous target is lost or replaced.
        /// </summary>
        protected abstract void OnRemoveFromTarget(AttributeProcessor target);

        public virtual void Dispose()
        {
            _pathSubscription.Dispose();
            if (_currentTarget != null)
            {
                OnRemoveFromTarget(_currentTarget);
                _currentTarget = null;
            }
        }
    }
}