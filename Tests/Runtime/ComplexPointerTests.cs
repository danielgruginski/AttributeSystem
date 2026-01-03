using NUnit.Framework;
using ReactiveSolutions.AttributeSystem.Core;
using SemanticKeys;
using sk;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;
using Attribute = ReactiveSolutions.AttributeSystem.Core.Attribute;

namespace ReactiveSolutions.AttributeSystem.Tests
{
    public class ComplexPointerTests
    {
        private AttributeProcessor _processor;
        private IModifierFactory _factory;

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
            _factory = new ModifierFactory();

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

            // 2. Modify via Target
            _processor.SetOrUpdateBaseValue(_target, 50);
            Assert.AreEqual(50, _processor.GetAttribute(_keyA).Value.Value);
            Assert.AreEqual(50, _processor.GetAttribute(_keyB).Value.Value);

            // 3. Modify via Pointer A
            _processor.SetOrUpdateBaseValue(_keyA, 25);
            Assert.AreEqual(25, _processor.GetAttribute(_target).Value.Value);
            Assert.AreEqual(25, _processor.GetAttribute(_keyB).Value.Value, "Pointer B should see change made via Pointer A");
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
            _processor.SetPointer(_keyB, _keyE);

            // Verify A sees E
            Assert.AreEqual(999, _processor.GetAttribute(_keyA).Value.Value);

            // Verify C still sees D
            Assert.AreEqual(10, _processor.GetAttribute(_keyC).Value.Value);
        }


        private void DumpAllValuesToLog(SemanticKey key)
        {
            Debug.Log($"{key.Value}: (called internally {_processor.GetAttribute(key)?.Name}) points to value: {_processor.GetAttribute(key)?.Value?.Value}," +
                $"has base value {(_processor.GetAttribute(key) as Attribute)?.BaseValue}, and if a pointer points to {(_processor.GetAttribute(key) as PointerAttribute)?.TargetKey}");

        }

        [Test]
        public void PointerDeletion_MidChain_SelfHealing()
        {
            // Setup: A -> B -> C
            _processor.SetPointer(_keyA, _keyB);
            _processor.SetPointer(_keyB, _keyC);
            _processor.SetOrUpdateBaseValue(_keyC, 100);

            Debug.Log("Initial Values:");
            DumpAllValuesToLog(_keyA);
            DumpAllValuesToLog(_keyB);
            DumpAllValuesToLog(_keyC);
            
            Assert.AreEqual(100, _processor.GetAttribute(_keyA).Value.Value);

            // 1. Delete B (The middle link)
            _processor.RemovePointer(_keyB);

            Debug.Log("Values after B removal:");
            DumpAllValuesToLog(_keyA);
            DumpAllValuesToLog(_keyB);
            DumpAllValuesToLog(_keyC);

            _processor.SetOrUpdateBaseValue(_keyC, 30);

            Debug.Log("Set C as 30:");
            DumpAllValuesToLog(_keyA);
            DumpAllValuesToLog(_keyB);
            DumpAllValuesToLog(_keyC);

            // At this point, A points to B. B is removed. 
            // If B is completely gone from the dictionary, A (Pointer) should see "Missing Target".
            // However, GetAttribute(B) might auto-create a concrete attribute?
            // No, RemovePointer removes it.
            // PointerAttribute listens to ObserveRemove? Assuming we added that logic to PointerAttribute.cs.
            // If A sees B is removed, it should likely return 0.

            // Note: Since B is gone, accessing A should resolve to 0 (default).
            // NOTE: This assumes PointerAttribute handles target removal gracefully.
            Assert.AreEqual(0, _processor.GetAttribute(_keyA).Value.Value);

            // 2. Re-create B as a CONCRETE attribute (not a pointer) with value 50
            _processor.SetOrUpdateBaseValue(_keyB, 50);

            // Chain is now: A -> B (Concrete). (C is orphaned)
            Assert.AreEqual(50, _processor.GetAttribute(_keyA).Value.Value);
        }

        [Test]
        public void ValueSource_Integration()
        {
            // Setup: A -> B (100)
            _processor.SetPointer(_keyA, _keyB);
            _processor.SetOrUpdateBaseValue(_keyB, 100);

            // Create a ValueSource that reads from A
            var source = new ValueSource
            {
                
                Mode = ValueSource.SourceMode.Attribute,
                AttributeRef = new ReactiveSolutions.AttributeSystem.Core.Data.AttributeReference { Name = _keyA }
            };

            float observedValue = -1f;
            source.GetObservable(_processor).Subscribe(v => observedValue = v);

            Assert.AreEqual(100, observedValue);

            // Modify B
            _processor.SetOrUpdateBaseValue(_keyB, 200);
            Assert.AreEqual(200, observedValue);
        }

        [Test]
        public void Modifier_AppliedToPointer_AffectsTarget()
        {
            // Setup: A -> B (10)
            _processor.SetPointer(_keyA, _keyB);
            _processor.SetOrUpdateBaseValue(_keyB, 10);

            // Add +5 Modifier to A (Pointer)
            var modSpec = new AttributeModifierSpec
            {
                //LogicType = TestKeys.Mock("Add"),
                LogicType = Modifiers.Static,
                Type = ModifierType.Additive,
                Arguments = new List<ValueSource> { new ValueSource { ConstantValue = 5 } }
            };

            // Create Modifier Instance
            // We need a concrete modifier implementation. Assuming FunctionalAttributeModifier or similar exists.
            // For simplicity in this test, we can use a mock/stub or helper if factory is complex.
            // Using Factory assuming "Add" logic is registered (it usually is standard).
            // If not, we'll manually create a simple modifier.

            // Let's assume ModifierFactory works with "Add". If not, we fail.
            // Wait, ModifierFactory usually needs setup.
            // Let's manually implement a trivial IAttributeModifier for testing.

            var simpleMod = new SimpleTestModifier(5f); // +5

            // Apply to A
            _processor.AddModifier("TestMod", simpleMod, _keyA);

            // Verify A reads 15
            Assert.AreEqual(15, _processor.GetAttribute(_keyA).Value.Value);

            // CRITICAL: Verify B (Target) reads 15!
            // Because adding a modifier to a pointer should proxy it to the target.
            Assert.AreEqual(15, _processor.GetAttribute(_keyB).Value.Value);
        }

        // Helper Modifier for the test above
        private class SimpleTestModifier : IAttributeModifier
        {
            private float _val;
            public SimpleTestModifier(float val) => _val = val;
            public ModifierType Type => ModifierType.Additive;
            public int Priority => 0;

            public string SourceId => "SourceIDMock";

            public float GetValue() => _val;
            public IObservable<float> GetMagnitude(AttributeProcessor context) => Observable.Return(_val);
        }
    }
}