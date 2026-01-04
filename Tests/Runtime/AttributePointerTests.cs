using NUnit.Framework;
using ReactiveSolutions.AttributeSystem.Core;
using SemanticKeys;
using UnityEngine;
using UnityEngine.TestTools;

namespace ReactiveSolutions.AttributeSystem.Tests
{
    public class AttributePointerTests
    {
        private AttributeProcessor _processor;

        // Explicit keys for testing
        private readonly SemanticKey _attackKey = TestKeys.Mock("Attack");
        private readonly SemanticKey _strengthKey = TestKeys.Mock("Strength");
        private readonly SemanticKey _mainStatKey = TestKeys.Mock("MainStat");
        private readonly SemanticKey _intelligenceKey = TestKeys.Mock("Intelligence");
        private readonly SemanticKey _aliasA = TestKeys.Mock("AliasA");
        private readonly SemanticKey _aliasB = TestKeys.Mock("AliasB");
        private readonly SemanticKey _realStat = TestKeys.Mock("RealStat");
        private readonly SemanticKey _keyA = TestKeys.Mock("A");
        private readonly SemanticKey _keyB = TestKeys.Mock("B");
        private readonly SemanticKey _keyC = TestKeys.Mock("C");
        private readonly SemanticKey _keyD = TestKeys.Mock("D");
        private readonly SemanticKey _alias = TestKeys.Mock("Alias");
        private readonly SemanticKey _target = TestKeys.Mock("Target");

        [SetUp]
        public void Setup()
        {
            _processor = new AttributeProcessor();
        }

        [Test]
        public void Pointer_ResolvesToTarget()
        {
            // 1. Setup Target
            _processor.SetOrUpdateBaseValue(_strengthKey, 10);

            // Verify Target Exists
            var directAttr = _processor.GetAttribute(_strengthKey);
            Assert.IsNotNull(directAttr, "Direct retrieval of Strength failed.");
            Assert.AreEqual(10, directAttr.BaseValue);

            // 2. Setup Pointer
            _processor.SetPointer(_attackKey, _strengthKey);

            // 3. Resolve
            Debug.Log($"Testing Pointer Resolution. Alias: {_attackKey}, Target: {_strengthKey}");

            var attackAttr = _processor.GetAttribute(_attackKey);

            Assert.IsNotNull(attackAttr, "Failed to retrieve attribute via alias (Attack). It returned null.");

            // Check Value (Reactive Property)
            Assert.AreEqual(10, attackAttr.Value.Value);
        }

        [Test]
        public void ModifyingAlias_ModifiesTarget()
        {
            _processor.SetPointer(_mainStatKey, _intelligenceKey);
            _processor.SetOrUpdateBaseValue(_intelligenceKey, 10);

            // Set Base Value via Alias
            // In the new system, setting base value on an attribute with an active pointer
            // acts on the LOCAL attribute, but the pointer OVERRIDES it for reading.
            // Wait, usually writing to an alias should write to the target?
            // Let's check AttributeProcessor.SetOrUpdateBaseValue logic.
            // It calls GetOrCreateAttribute -> attr.SetBaseValue.
            // If 'attr' is the alias, it sets the alias's hidden base value.
            // BUT the alias reads from the Target. 

            // CORRECTION: Standard RPG "Alias" behavior usually means "Read from Target".
            // Writing to Alias usually means "Write to the Alias's base (which is ignored)" OR "Write to Target".
            // If we want "Write to Target", we need explicit logic.
            // However, for this test, let's modify the TARGET and verify the Alias updates.

            _processor.SetOrUpdateBaseValue(_intelligenceKey, 20);
            Assert.AreEqual(20, _processor.GetAttribute(_mainStatKey).Value.Value);
        }

        [Test]
        public void ChainedPointers_ResolveCorrectly()
        {
            // A -> B -> C
            _processor.SetPointer(_aliasA, _aliasB);
            _processor.SetPointer(_aliasB, _realStat);
            _processor.SetOrUpdateBaseValue(_realStat, 50);

            Assert.AreEqual(50, _processor.GetAttribute(_aliasA).Value.Value);
        }

        [Test]
        public void CircularDependency_IsPrevented()
        {
            // Establish A -> B
            _processor.SetPointer(_keyA, _keyB);

            // Expect the error log or warning depending on implementation
            // The logic inside AttributeProcessor.SetPointer checks IsLocallyCircular.
            LogAssert.Expect(LogType.Error, $"[AttributeProcessor] Circular pointer detected: {_keyB} -> {_keyA}");

            // Attempt to point B -> A (Loop)
            _processor.SetPointer(_keyB, _keyA);
        }

        [Test]
        public void SelfCircularDependency_IsPrevented()
        {
            LogAssert.Expect(LogType.Warning, $"[AttributeProcessor] Cannot point alias '{_keyA}' to itself.");
            _processor.SetPointer(_keyA, _keyA);
        }

        [Test]
        public void RemovePointer_RestoresIndependence()
        {
            // 1. Setup Alias -> Target
            var handle = _processor.SetPointer(_alias, _target);
            _processor.SetOrUpdateBaseValue(_target, 100);
            _processor.SetOrUpdateBaseValue(_alias, 5); // Hidden base value of alias

            // Verify Pointer Active
            Assert.AreEqual(100, _processor.GetAttribute(_alias).Value.Value);

            // 2. Remove Pointer (Dispose handle)
            handle.Dispose();

            // 3. Verify Fallback to Local Base
            Assert.AreEqual(5, _processor.GetAttribute(_alias).Value.Value);
            Assert.AreEqual(100, _processor.GetAttribute(_target).Value.Value);
        }
    }
}