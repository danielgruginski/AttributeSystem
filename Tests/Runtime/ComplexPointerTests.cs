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
    public class ComplexPointerTests
    {
        private AttributeProcessor _processor;

        // Keys
        private SemanticKey _keyA;
        private SemanticKey _keyB;
        private SemanticKey _keyC;
        private SemanticKey _keyD;
        private SemanticKey _keyE;
        private SemanticKey _target;

        [SetUp]
        public void Setup()
        {
            _processor = new AttributeProcessor();

            _keyA = TestKeys.Mock("A");
            _keyB = TestKeys.Mock("B");
            _keyC = TestKeys.Mock("C");
            _keyD = TestKeys.Mock("D");
            _keyE = TestKeys.Mock("E");
            _target = TestKeys.Mock("Target");
        }

        [Test]
        public void SharedDependency_DiamondPattern()
        {
            // Setup: A -> Target, B -> Target
            _processor.SetOrUpdateBaseValue(_target, 100);
            _processor.SetPointer(_keyA, _target);
            _processor.SetPointer(_keyB, _target);

            // 1. Verify reading via both pointers
            Assert.AreEqual(100, _processor.GetAttribute(_keyA).Value.Value);
            Assert.AreEqual(100, _processor.GetAttribute(_keyB).Value.Value);

            // 2. Modify Target
            _processor.SetOrUpdateBaseValue(_target, 50);
            Assert.AreEqual(50, _processor.GetAttribute(_keyA).Value.Value);
            Assert.AreEqual(50, _processor.GetAttribute(_keyB).Value.Value);
        }

        [Test]
        public void LongChain_Retargeting()
        {
            // Setup: A -> B -> C -> D
            _processor.SetPointer(_keyA, _keyB);
            _processor.SetPointer(_keyB, _keyC);
            _processor.SetPointer(_keyC, _keyD);
            _processor.SetOrUpdateBaseValue(_keyD, 10);

            Assert.AreEqual(10, _processor.GetAttribute(_keyA).Value.Value);

            // Retarget B to point to E
            // New Chain: A -> B -> E.   (C -> D is isolated)
            _processor.SetOrUpdateBaseValue(_keyE, 999);

            // Note: We use SetPointer which pushes a NEW pointer to B's stack.
            // B now points to E (Top of stack).
            _processor.SetPointer(_keyB, _keyE);

            // Verify A sees E (A -> B -> E)
            Assert.AreEqual(999, _processor.GetAttribute(_keyA).Value.Value);

            // Verify C still sees D (C was untouched)
            Assert.AreEqual(10, _processor.GetAttribute(_keyC).Value.Value);
        }

        [Test]
        public void PointerDeletion_MidChain_SelfHealing()
        {
            // Setup: A -> B -> C
            _processor.SetPointer(_keyA, _keyB);
            var bPointerHandle = _processor.SetPointer(_keyB, _keyC);
            _processor.SetOrUpdateBaseValue(_keyC, 100);

            Assert.AreEqual(100, _processor.GetAttribute(_keyA).Value.Value);

            // 1. Delete B's pointer to C (The middle link)
            // This exposes B's underlying base value (default 0)
            bPointerHandle.Dispose();

            // A -> B (Local Base 0)
            Assert.AreEqual(0, _processor.GetAttribute(_keyA).Value.Value);

            // 2. Give B a base value
            _processor.SetOrUpdateBaseValue(_keyB, 50);

            // A -> B (Local Base 50)
            Assert.AreEqual(50, _processor.GetAttribute(_keyA).Value.Value);
        }

        [Test]
        public void Modifier_AppliedToPointer_AffectsOutput()
        {
            // Setup: A -> B (10)
            _processor.SetPointer(_keyA, _keyB);
            _processor.SetOrUpdateBaseValue(_keyB, 10);

            // Add +5 Modifier to A (Pointer)
            var simpleMod = new SimpleTestModifier(5f);
            _processor.AddModifier("TestMod", simpleMod, _keyA);

            // Verify A reads 15 (10 from B + 5 from Modifier on A)
            Assert.AreEqual(15, _processor.GetAttribute(_keyA).Value.Value);

            // Verify B is untouched (10)
            // Unlike the previous test where we assumed "Proxying", the new architecture
            // stacks modifiers ON TOP of the resolved pointer value.
            // So A = (B.Value) + Modifiers_On_A.
            // B itself remains 10. This is actually more flexible/correct for RPGs.
            Assert.AreEqual(10, _processor.GetAttribute(_keyB).Value.Value);
        }

        // Helper Modifier for the test above
        private class SimpleTestModifier : IAttributeModifier
        {
            private float _val;
            public SimpleTestModifier(float val) => _val = val;
            public ModifierType Type => ModifierType.Additive;
            public int Priority => 0;
            public string SourceId => "SourceIDMock";
            public float Modify(float val) => val + _val; // Simple implementation
            public IObservable<float> GetMagnitude(AttributeProcessor context) => Observable.Return(_val);
        }
    }
}