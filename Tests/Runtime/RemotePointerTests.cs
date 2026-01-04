using NUnit.Framework;
using ReactiveSolutions.AttributeSystem.Core;
using SemanticKeys;
using System;
using System.Collections.Generic;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;

namespace ReactiveSolutions.AttributeSystem.Tests
{
    public class RemotePointerTests
    {
        private AttributeProcessor _localProcessor;
        private AttributeProcessor _remoteProcessor;

        // Keys
        private SemanticKey _pointerKey;
        private SemanticKey _targetAttrKey;
        private SemanticKey _providerKey;
        private SemanticKey _nestedProviderKey;

        [SetUp]
        public void Setup()
        {
            _localProcessor = new AttributeProcessor();
            _remoteProcessor = new AttributeProcessor();

            _pointerKey = TestKeys.Mock("RemotePointer");
            _targetAttrKey = TestKeys.Mock("TargetStat");
            _providerKey = TestKeys.Mock("RemoteProvider");
            _nestedProviderKey = TestKeys.Mock("NestedProvider");
        }

        [Test]
        public void RemotePointer_ResolvesToExternalProcessor()
        {
            // Setup Remote: Has "TargetStat" = 100
            _remoteProcessor.SetOrUpdateBaseValue(_targetAttrKey, 100);

            // Setup Local: "RemotePointer" points to "TargetStat" on "RemoteProvider"
            var path = new List<SemanticKey> { _providerKey };
            _localProcessor.SetPointer(_pointerKey, _targetAttrKey, path);

            // Initially, provider is missing, so value should be 0
            Assert.AreEqual(0, _localProcessor.GetAttribute(_pointerKey).Value.Value);

            // Act: Register Remote Processor as Provider
            _localProcessor.RegisterExternalProvider(_providerKey, _remoteProcessor);

            // Assert: Value should now mirror remote (100)
            Assert.AreEqual(100, _localProcessor.GetAttribute(_pointerKey).Value.Value);
        }

        [Test]
        public void RemotePointer_HandlesDisconnection()
        {
            // Setup Link
            _remoteProcessor.SetOrUpdateBaseValue(_targetAttrKey, 50);
            _localProcessor.RegisterExternalProvider(_providerKey, _remoteProcessor);
            _localProcessor.SetPointer(_pointerKey, _targetAttrKey, new List<SemanticKey> { _providerKey });

            Assert.AreEqual(50, _localProcessor.GetAttribute(_pointerKey).Value.Value);

            // Act: Unregister Provider
            _localProcessor.UnregisterExternalProvider(_providerKey);

            // Assert: Fallback to 0 (Missing Provider)
            Assert.AreEqual(0, _localProcessor.GetAttribute(_pointerKey).Value.Value);
        }

        [Test]
        public void RemotePointer_HandlesReconnection()
        {
            _localProcessor.SetPointer(_pointerKey, _targetAttrKey, new List<SemanticKey> { _providerKey });

            // Connect -> 100
            _remoteProcessor.SetOrUpdateBaseValue(_targetAttrKey, 100);
            _localProcessor.RegisterExternalProvider(_providerKey, _remoteProcessor);
            Assert.AreEqual(100, _localProcessor.GetAttribute(_pointerKey).Value.Value);

            // Disconnect -> 0
            _localProcessor.UnregisterExternalProvider(_providerKey);
            Assert.AreEqual(0, _localProcessor.GetAttribute(_pointerKey).Value.Value);

            // Reconnect -> 100
            _localProcessor.RegisterExternalProvider(_providerKey, _remoteProcessor);
            Assert.AreEqual(100, _localProcessor.GetAttribute(_pointerKey).Value.Value);
        }

        [Test]
        public void RemotePointer_ModifiersAreLocal()
        {
            // Setup: Remote has 10
            _remoteProcessor.SetOrUpdateBaseValue(_targetAttrKey, 10);
            _localProcessor.RegisterExternalProvider(_providerKey, _remoteProcessor);
            _localProcessor.SetPointer(_pointerKey, _targetAttrKey, new List<SemanticKey> { _providerKey });

            // Apply +5 Modifier to LOCAL pointer
            var mod = new SimpleTestModifier(5f);
            _localProcessor.AddModifier("TestMod", mod, _pointerKey);

            // 1. Verify Local reads 15 (10 Remote + 5 Local Mod)
            Assert.AreEqual(15, _localProcessor.GetAttribute(_pointerKey).Value.Value);

            // 2. Verify Remote is UNTOUCHED (Remains 10)
            // In the new Stack architecture, modifiers are applied to the result of the pointer,
            // they are not forwarded to the source.
            Assert.AreEqual(10, _remoteProcessor.GetAttribute(_targetAttrKey).Value.Value);
        }

        [Test]
        public void RemotePointer_NestedPath()
        {
            // Topology: Local -> Middle -> Remote -> TargetStat(100)
            var middleProcessor = new AttributeProcessor();

            _remoteProcessor.SetOrUpdateBaseValue(_targetAttrKey, 100);

            // Link Middle -> Remote
            middleProcessor.RegisterExternalProvider(_nestedProviderKey, _remoteProcessor);

            // Link Local -> Middle
            _localProcessor.RegisterExternalProvider(_providerKey, middleProcessor);

            // Pointer Path: Provider -> NestedProvider
            var path = new List<SemanticKey> { _providerKey, _nestedProviderKey };
            _localProcessor.SetPointer(_pointerKey, _targetAttrKey, path);

            // Verify resolution A -> B -> C -> Value
            Assert.AreEqual(100, _localProcessor.GetAttribute(_pointerKey).Value.Value);

            // Break the chain at C (Remove Remote from Middle)
            middleProcessor.UnregisterExternalProvider(_nestedProviderKey);
            Assert.AreEqual(0, _localProcessor.GetAttribute(_pointerKey).Value.Value);

            // Restore
            middleProcessor.RegisterExternalProvider(_nestedProviderKey, _remoteProcessor);
            Assert.AreEqual(100, _localProcessor.GetAttribute(_pointerKey).Value.Value);
        }

        // Helper Modifier
        private class SimpleTestModifier : IAttributeModifier
        {
            private float _val;
            public SimpleTestModifier(float val) => _val = val;
            public ModifierType Type => ModifierType.Additive;
            public string SourceId => "SourceIDMock";
            public int Priority => 0;
            public float Modify(float val) => val + _val; // Simple implementation for test
            public IObservable<float> GetMagnitude(AttributeProcessor context) => Observable.Return(_val);
        }
    }
}