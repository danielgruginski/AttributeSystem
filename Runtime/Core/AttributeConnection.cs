using SemanticKeys;
using System;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Core
{
    /// <summary>
    /// Manages the lifecycle of a modifier applied to a remote attribute.
    /// It observes the provider connectivity and re-applies the modifier if the connection is restored.
    /// </summary>
    public class AttributeConnection : IDisposable
    {
        private readonly AttributeProcessor _localProcessor;
        private readonly List<SemanticKey> _providerPath;
        private readonly SemanticKey _targetAttribute;
        private readonly IAttributeModifier _modifier;
        private readonly string _sourceId;

        private readonly SerialDisposable _modifierHandle = new SerialDisposable();
        private readonly IDisposable _topologySubscription;
        private bool _isDisposed = false;

        public AttributeConnection(
            AttributeProcessor localProcessor,
            List<SemanticKey> providerPath,
            SemanticKey targetAttribute,
            IAttributeModifier modifier,
            string sourceId)
        {
            _localProcessor = localProcessor;
            _providerPath = providerPath;
            _targetAttribute = targetAttribute;
            _modifier = modifier;
            _sourceId = sourceId;

            // Observe the first link in the chain
            SemanticKey nextKey = _providerPath[0];

            _topologySubscription = _localProcessor.ObserveProvider(nextKey)
                .Subscribe(provider =>
                {
                    if (provider == null)
                    {
                        // Provider disconnected -> Remove modifier
                        _modifierHandle.Disposable = null;
                    }
                    else
                    {
                        // Provider connected -> Apply modifier (recursively if needed)
                        var remainingPath = _providerPath.Count > 1 ? _providerPath.GetRange(1, _providerPath.Count - 1) : null;

                        // FIX: We must catch potential errors during recursion if the chain is partially formed
                        try
                        {
                            _modifierHandle.Disposable = provider.AddModifier(_sourceId, _modifier, _targetAttribute, remainingPath);
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"[AttributeConnection] Failed to apply modifier to provider '{nextKey}': {e.Message}");
                            _modifierHandle.Disposable = null;
                        }
                    }
                });
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            _topologySubscription?.Dispose();
            _modifierHandle.Dispose();
        }
    }
}