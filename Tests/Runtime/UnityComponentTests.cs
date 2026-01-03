using NUnit.Framework;
using ReactiveSolutions.AttributeSystem.Core;
using ReactiveSolutions.AttributeSystem.Core.Data;
using ReactiveSolutions.AttributeSystem.Core.Modifiers;
using ReactiveSolutions.AttributeSystem.Unity;
using SemanticKeys;
using System.Collections.Generic;
using System.Collections;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;
using sk;

namespace ReactiveSolutions.AttributeSystem.Tests
{
    public class UnityComponentTests
    {
        private GameObject _playerGO;
        private GameObject _swordGO;
        private AttributeController _playerController;
        private AttributeController _swordController;

        [SetUp]
        public void Setup()
        {
            _playerGO = new GameObject("Player");
            _swordGO = new GameObject("Sword");

            _playerController = _playerGO.AddComponent<AttributeController>();
            _swordController = _swordGO.AddComponent<AttributeController>();
        }

        [TearDown]
        public void Teardown()
        {
            if (_playerGO != null) Object.DestroyImmediate(_playerGO);
            if (_swordGO != null) Object.DestroyImmediate(_swordGO);
        }

        // ========================================================================
        // ATTRIBUTE CONTROLLER TESTS
        // ========================================================================

        [Test]
        public void Controller_Initializes_Processor()
        {
            Assert.IsNotNull(_playerController.Processor, "Processor should be auto-created.");
        }

        [Test]
        public void Controller_AddAttribute_SetsBaseValue()
        {
            var key = TestKeys.Mock("Health");
            _playerController.AddAttribute(key, 100f);

            var attr = _playerController.GetAttribute(key);
            Assert.IsNotNull(attr);
            Assert.AreEqual(100f, attr.BaseValue);
        }

        [Test]
        public void Controller_GetAttributeObservable_ResolvesReactiveValues()
        {
            var key = TestKeys.Mock("Mana");
            _playerController.AddAttribute(key, 50f);

            float lastValue = 0f;
            _playerController.GetAttributeObservable(key)
                .SelectMany(a => a.ReactivePropertyAccess)
                .Subscribe(v => lastValue = v);

            Assert.AreEqual(50f, lastValue);

            // Update value
            _playerController.Processor.SetOrUpdateBaseValue(key, 75f);
            Assert.AreEqual(75f, lastValue);
        }

        [Test]
        public void Controller_LinkProvider_EnablesRemoteModifiers()
        {
            // 1. Setup Player Strength
            var strKey = TestKeys.Mock("Strength");
            _playerController.AddAttribute(strKey, 10f);

            // 2. Setup Sword Damage (Base 5)
            var dmgKey = TestKeys.Mock("Damage");
            _swordController.AddAttribute(dmgKey, 5f);

            // 3. Add Modifier to Sword: Damage += Owner.Strength * 2
            // We use the raw Processor API as requested (no extensions)
            var ownerKey = TestKeys.Mock("Owner");

            var source = new ValueSource
            {
                Mode = ValueSource.SourceMode.Attribute,
                AttributeRef = new AttributeReference(strKey, new List<SemanticKey> { ownerKey })
            };

            var spec = new AttributeModifierSpec() { 
                TargetAttribute = dmgKey,
                SourceId = "StrengthScaling",
                Type = ModifierType.Additive,
                Priority = 0,
                LogicType = Modifiers.Linear,
                Arguments = new List<ValueSource> { source, ValueSource.Const(2f), ValueSource.Const(0f) }
            };


            // Note: Assuming 'LinearModifier' logic is available via manual instantiation or Factory
            var mod = new LinearModifier(spec);

            _swordController.Processor.AddModifier("Scaling", mod, dmgKey);

            // 4. Verify initial state (Link missing, so Modifier adds 0 or waits)
            // 5 + 0 = 5
            Assert.AreEqual(5f, _swordController.GetAttribute(dmgKey).ReactivePropertyAccess.Value, "Should be base value before linking");

            // 5. LINK PROVIDERS
            _swordController.LinkProvider(ownerKey, _playerController);

            // 6. Verify Resolved State
            // 5 + (10 * 2) = 25
            Assert.AreEqual(25f, _swordController.GetAttribute(dmgKey).ReactivePropertyAccess.Value, "Should include Strength scaling after linking");
        }

        // ========================================================================
        // STAT BLOCK LINKER TESTS
        // ========================================================================

        [Test]
        public void StatBlockLinker_HandlesMissingID_Gracefully()
        {
            var linker = _swordGO.AddComponent<StatBlockLinker>();
            linker.SetTarget(_swordController);
            linker.AddStatBlock(new StatBlockID { ID = "NonExistentBlock_99999" });

            // Should verify this logs an error but does not throw exception
            LogAssert.Expect(LogType.Error, "[StatBlockJsonLoader] Could not load StatBlock with ID: NonExistentBlock_99999 (Attempted path: Data/StatBlocks/NonExistentBlock_99999)");

            // This call should not crash
            linker.ApplyStatBlocks();
        }

        [UnityTest]
        public IEnumerator StatBlockLinker_DisposesHandle_OnDestroy()
        {
            // This test simulates the "Cleanup" lifecycle. 
            // Since we can't easily load a real Resource StatBlock in Unit Tests without setup,
            // we will Manually inject the ActiveStatBlock (via reflection or by subclassing for test, 
            // BUT here we will test the 'Unequip' behavior indirectly if possible, or verify components don't leak).

            // Actually, we can test the `ActiveStatBlock` logic directly as a proxy for the Linker's behavior.

            // 1. Setup
            var key = TestKeys.Mock("TestStat");
            _playerController.AddAttribute(key, 10f);

            // 2. Create a "Fake" applied stat block (Manual simulation of Linker internals)
            var activeBlock = new ActiveStatBlock();

            var spec = new AttributeModifierSpec()
            {
                TargetAttribute = key,
                SourceId = "TestSource",
                Type = ModifierType.Additive,
                Priority = 0,
                LogicType = Modifiers.Static,
                Arguments = new List<ValueSource> { ValueSource.Const(5f) }
            };

            var mod = new StaticAttributeModifier(spec);
            var handle = _playerController.Processor.AddModifier("TestSource", mod, key);

            activeBlock.AddHandle(handle);

            // 3. Verify Modifier Applied (10 + 5 = 15)
            Assert.AreEqual(15f, _playerController.GetAttribute(key).ReactivePropertyAccess.Value);

            // 4. Simulate Linker Destroy/Unequip
            activeBlock.Dispose();

            yield return null; // Wait a frame (Rx updates are usually immediate, but good practice in UnityTest)

            // 5. Verify Modifier Removed (10 + 0 = 10)
            Assert.AreEqual(10f, _playerController.GetAttribute(key).ReactivePropertyAccess.Value, "Modifier should be removed after disposal");
        }
    }
}