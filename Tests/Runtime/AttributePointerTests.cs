using NUnit.Framework;
using ReactiveSolutions.AttributeSystem.Core;
using SemanticKeys;
using UnityEngine;
using UnityEngine.TestTools;
using static UnityEngine.GraphicsBuffer;

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

            Debug.Log($"Resolved Attribute Object Name: {attackAttr.Name}");

            Assert.AreEqual(_attackKey.ToString(), attackAttr.Name.ToString());
            Assert.AreEqual(10, attackAttr.Value.Value);
        }

        [Test]
        public void ModifyingAlias_ModifiesTarget()
        {
            _processor.SetPointer(_mainStatKey, _intelligenceKey);
            _processor.SetOrUpdateBaseValue(_intelligenceKey, 10);

            // Set Base Value via Alias
            _processor.SetOrUpdateBaseValue(_mainStatKey, 20);

            // Check via Target
            Assert.AreEqual(20, _processor.GetAttribute(_intelligenceKey).Value.Value);
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

            // Expect the error log from the next call
            LogAssert.Expect(LogType.Error, $"[AttributeProcessor] Circular pointer detected: {_keyB} -> {_keyA}");

            // Attempt to point B -> A (Loop)
            // Should be blocked by internal check and log Error
            _processor.SetPointer(_keyB, _keyA);

            // Set values
            // Since A points to B, this sets B to 10
            _processor.SetOrUpdateBaseValue(_keyA, 10);

            // This sets B to 20
            _processor.SetOrUpdateBaseValue(_keyB, 20);

            var attrA = _processor.GetAttribute(_keyA);
            var attrB = _processor.GetAttribute(_keyB);

            Assert.IsNotNull(attrA);
            Assert.IsNotNull(attrB);

            // Logic Check:
            // 1. A is a Pointer to B. It reads 20.
            Assert.AreEqual(20, attrA.Value.Value, "A is an alias of B, so it should reflect B's value (20).");

            // 2. B is a Concrete Attribute. It stores 20.
            Assert.AreEqual(20, attrB.Value.Value, "B is the target storage, so it should hold the value 20.");

            // 3. They are NOT the same object instance.
            // attrA is the PointerAttribute wrapper. attrB is the concrete Attribute.
            Assert.AreNotSame(attrA, attrB, "Attribute A (Pointer) should be distinct from Attribute B (Concrete).");

            // 4. Verify Types
            Assert.IsInstanceOf<PointerAttribute>(attrA);
            Assert.IsInstanceOf<Attribute>(attrB);
            Assert.IsNotInstanceOf<PointerAttribute>(attrB);
        }
        [Test]
        public void LongerCircularDependency_IsPrevented()
        {
            // Establish A -> B
            _processor.SetPointer(_keyA, _keyB);

            // Expect the error log from the next call
            LogAssert.Expect(LogType.Error, $"[AttributeProcessor] Circular pointer detected: {_keyD} -> {_keyA}");

            // Attempt to point B -> A (Loop)
            // Should be blocked by internal check and log Error
            _processor.SetPointer(_keyB, _keyC);
            _processor.SetPointer(_keyC, _keyD);
            _processor.SetPointer(_keyD, _keyA);

        }
        [Test]
        public void SelfCircularDependency_IsPrevented()
        {
            // Expect the error log from the next call
            LogAssert.Expect(LogType.Warning, $"[AttributeProcessor] Cannot point alias '{_keyA}' to itself.");

            // Establish A -> B
            _processor.SetPointer(_keyA, _keyA);
        }

        [Test]
        public void RemovePointer_RestoresIndependence()
        {
            _processor.SetPointer(_alias, _target);
            _processor.SetOrUpdateBaseValue(_target, 100);

            Assert.AreEqual(100, _processor.GetAttribute(_alias).Value.Value);

            _processor.RemovePointer(_alias);

            // Reset alias to prove independence (since it might be null if not created)
            _processor.SetOrUpdateBaseValue(_alias, 0);

            Assert.AreEqual(0, _processor.GetAttribute(_alias).Value.Value);
            Assert.AreEqual(100, _processor.GetAttribute(_target).Value.Value);
        }
    }
}