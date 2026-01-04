using SemanticKeys;
using System;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using UnityEngine;
/*
namespace ReactiveSolutions.AttributeSystem.Core
{
    /// <summary>
    /// An attribute that acts as an alias (pointer) to another attribute.
    /// It maintains its own list of modifiers ("applied to the alias")
    /// and dynamically moves them to the current target attribute.
    /// </summary>
    public class PointerAttribute : Attribute
    {
        private SemanticKey _targetKey;
        public SemanticKey TargetKey => _targetKey; // Expose for Processor logic
        private List<SemanticKey> _targetPath; // Null if local

        private IAttribute _currentTarget;

        // We maintain local modifier handles to dispose them from the old target
        // when switching targets.
        // Map: Modifier Instance -> Handle on Target
        private Dictionary<IAttributeModifier, IDisposable> _appliedHandles = new Dictionary<IAttributeModifier, IDisposable>();

        // Helper to expose the *current* value of the target as a stream
        private readonly BehaviorSubject<IObservable<float>> _targetStreamSubject;

        // Connection management for remote pointers
        private SerialDisposable _remoteConnectionDisposable = new SerialDisposable();
        private bool HasValidTarget => _currentTarget != null && !_currentTarget.IsDisposed;

        public PointerAttribute(SemanticKey name, SemanticKey initialTarget, AttributeProcessor processor, List<SemanticKey> path = null)
            : base(name, 0f, processor)
        {
            _targetKey = initialTarget;
            _targetPath = path;

            _targetStreamSubject = new BehaviorSubject<IObservable<float>>(Observable.Return(0f));

            _targetStreamSubject
                .Switch()
                .Subscribe(val => _finalValue.Value = val)
                .AddTo(_calculationDisposable);

            // Add remote connection disposable to lifecycle
            _remoteConnectionDisposable.AddTo(_calculationDisposable);

            InitializeLinking();
        }

        public void SetTarget(SemanticKey newTarget, List<SemanticKey> newPath = null)
        {
            // If target AND path are same, return
            // Simple check:
            bool samePath = (newPath == null && _targetPath == null) ||
                            (newPath != null && _targetPath != null && newPath.SequenceEqual(_targetPath)); // Requires System.Linq

            if (newTarget.Equals(_targetKey) && samePath) return;

            CleanupHandles();
            _targetKey = newTarget;
            _targetPath = newPath;
            InitializeLinking();
        }

        private void InitializeLinking()
        {
            if (_targetPath == null || _targetPath.Count == 0)
            {
                // LOCAL POINTER LOGIC
                // (Existing logic + Local Reactive Fixes)
                _remoteConnectionDisposable.Disposable = null; // Clear remote watcher

                UpdateLocalTargetLink();

                // Reactive Re-linking for Local
                _processor.Attributes.ObserveReplace()
                    .Where(evt => evt.Key.Equals(_targetKey))
                    .Subscribe(_ => UpdateLocalTargetLink())
                    .AddTo(_calculationDisposable); // Note: This accumulates subscriptions if SetTarget called multiple times. 
                                                    // Ideal: Put in a SerialDisposable too. For now, acceptable overhead or fixable.

                _processor.Attributes.ObserveAdd()
                    .Where(evt => evt.Key.Equals(_targetKey))
                    .Subscribe(_ => UpdateLocalTargetLink())
                    .AddTo(_calculationDisposable);

                _processor.Attributes.ObserveRemove()
                   .Where(evt => evt.Key.Equals(_targetKey))
                   .Subscribe(_ => LinkTo(null))
                   .AddTo(_calculationDisposable);
            }
            else
            {
                // REMOTE POINTER LOGIC
                // We need to observe the provider at the end of the path.
                // AttributeProcessor.ObserveProvider logic or simple "GetAttributeObservable" logic?
                // GetAttributeObservable returns IObservable<Attribute>.

                // We want the Attribute object itself so we can SetBaseValue/AddModifier.
                // The processor allows "GetAttribute(key, path)" but that's a one-time fetch.

                // We need a persistent observation of the "Provider".
                // Let's use a helper that resolves the processor at the path end.
                Debug.Log($"[PointerAttribute] Initializing Remote Link for {_targetKey}. Path: {string.Join(", ", _targetPath)}");

                var pathObservable = ResolvePathRecursively(_processor, _targetPath);

                _remoteConnectionDisposable.Disposable = pathObservable
                    .DistinctUntilChanged()
                    .Subscribe(remoteProcessor =>
                    {
                        if (remoteProcessor != null)
                        {
                            // We found the remote processor!
                            // Now bind to the attribute on it.
                            Debug.Log($"[PointerAttribute] Connecting to Remote Attribute: {_targetKey}"); // TRACE 2
                                                                                                           
                            var attr = remoteProcessor.GetOrCreateAttribute(_targetKey);
                            LinkTo(attr);
                        }
                        else
                        {
                            // Remote processor lost
                            Debug.Log("[PointerAttribute] Remote Processor lost/null. Unlinking."); // TRACE 3
                            LinkTo(null);
                        }
                    });
            }
        }

        private static IObservable<AttributeProcessor> ResolvePathRecursively(AttributeProcessor current, List<SemanticKey> path)
        {
            if (path == null || path.Count == 0)
                return Observable.Return(current);

            SemanticKey nextKey = path[0];
            Debug.Log($"[ResolvePath] Looking for provider '{nextKey}' on Processor {current.GetHashCode()}"); // TRACE 4
            // Note: GetRange throws if count is 0, handled by check above
            List<SemanticKey> remaining = path.Count > 1 ? path.GetRange(1, path.Count - 1) : new List<SemanticKey>();

            return current.ObserveProvider(nextKey)
                .Do(p => Debug.Log($"[ResolvePath] Provider '{nextKey}' emitted: {(p != null ? "Found" : "Null")}")) // TRACE 5
                .Select(provider =>
                {
                    if (provider == null) return Observable.Return<AttributeProcessor>(null);

                    // Recurse
                    return ResolvePathRecursively(provider, remaining);
                })
                .Switch();
        }


        private void UpdateLocalTargetLink()
        {
            // Use GetAttribute (no create) to see if it exists locally
            var attr = _processor.GetAttribute(_targetKey);
            LinkTo(attr);
        }


        private void LinkTo(IAttribute newTarget)
        {
            _currentTarget = newTarget;

            if (HasValidTarget)
            {
                // If the target stream completes (because target was disposed), 
                // fallback to 0 immediately. This covers cases where ObserveRemove might trigger late
                // or if we miss the event but the object dies.
                var safeStream = _currentTarget.Value.Concat(Observable.Return(0f));

                _targetStreamSubject.OnNext(safeStream);

                // Re-apply modifiers to the new instance
                // First, ensure we don't have stale handles
                CleanupHandles();

                foreach (var mod in _modifiers)
                {
                    ApplyModifierToTarget(mod);
                }
            }
            else
            {
                _targetStreamSubject.OnNext(Observable.Return(0f));
                CleanupHandles();
            }
        }


        private void ApplyModifierToTarget(IAttributeModifier modifier)
        {
            if (HasValidTarget)
            {
                // We add the modifier to the TARGET, but track the handle here
                var handle = _currentTarget.AddModifier(modifier);
                _appliedHandles[modifier] = handle;
            }
        }

        private void CleanupHandles()
        {
            foreach (var kvp in _appliedHandles)
            {
                kvp.Value.Dispose();
            }
            _appliedHandles.Clear();
        }

        // --- Overrides ---

        public override float BaseValue
        {
            get => HasValidTarget ? _currentTarget.BaseValue : 0f;
        }

        public override void SetBaseValue(float value)
        {
            // Redirect write to target
            if (HasValidTarget)
            {
                _currentTarget.SetBaseValue(value);
            }
            else
            {
                if (_targetPath == null || _targetPath.Count == 0)
                {
                    var attr = _processor.GetOrCreateAttribute(_targetKey);
                    LinkTo(attr);
                    attr.SetBaseValue(value);
                }
                else
                {
                    Debug.LogWarning($"[PointerAttribute] Cannot set value. Target '{_targetKey}' unreachable.");
                }
            }
        }

        public override IDisposable AddModifier(IAttributeModifier modifier)
        {
            Debug.Assert(modifier != null, "[PointerAttribute] Modifier cannot be null");
            // 1. Store locally (Source of Truth for the Pointer)
            // This adds to the base class _modifiers collection
            _modifiers.Add(modifier);

            // 2. Apply to current target
            ApplyModifierToTarget(modifier);

            // 3. Return disposable that removes from BOTH
            return Disposable.Create(() =>
            {
                RemoveModifier(modifier);
            });
        }

        public override void RemoveModifier(IAttributeModifier modifier)
        {
            // Remove from local
            _modifiers.Remove(modifier);

            // Remove from target
            if (_appliedHandles.TryGetValue(modifier, out var handle))
            {
                handle.Dispose();
                _appliedHandles.Remove(modifier);
            }
        }

        // Pointer doesn't calculate anything itself, it just proxies.
        protected override void RebuildCalculationChain() { }
    }
}*/