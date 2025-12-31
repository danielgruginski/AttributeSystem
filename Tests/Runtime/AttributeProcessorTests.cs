using NUnit.Framework;
using ReactiveSolutions.AttributeSystem.Core;
using ReactiveSolutions.AttributeSystem.Core.Data;
using ReactiveSolutions.AttributeSystem.Core.Modifiers;
using SemanticKeys;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Tests
{
    public static class TestKeys
    {
        // Creates a SemanticKey where the GUID is deterministic based on the name.
        // Key("Health") will always equal Key("Health")
        public static SemanticKey Mock(string name)
        {
            // Simple trick: Use the name as the GUID for tests, or generate a hash
            // Using name as GUID is perfectly valid for testing logic!
            return new SemanticKey(name, name, "TestDomain");
        }
    }

    public class AttributeProcessorTests
    {
        private AttributeProcessor _processor;
        private IModifierFactory _factory;

        [SetUp]
        public void Setup()
        {
            _processor = new AttributeProcessor();
            _factory = new ModifierFactory(); // Instantiate the specific factory implementation for this test context
        }

        [Test]
        public void SetBaseValue_CreatesNewAttribute_IfMissing()
        {
            // Act
            _processor.SetOrUpdateBaseValue(TestKeys.Mock("Health"), 100f);
            var attr = _processor.GetAttribute(TestKeys.Mock("Health"));

            // Assert
            Assert.IsNotNull(attr);
            Assert.AreEqual(100f, attr.BaseValue);
        }

        [Test]
        public void GetAttribute_ReturnsNull_IfMissing()
        {
            var attr = _processor.GetAttribute(TestKeys.Mock("NonExistent"));
            Assert.IsNull(attr);
        }

        // --- THE CRITICAL LOGIC YOU ARE CONCERNED ABOUT ---

        [Test]
        public void ExternalProvider_CanBeAccessed_ViaDotNotation()
        {
            // 1. Create a secondary processor (e.g., the Player)
            var ownerProcessor = new AttributeProcessor();
            ownerProcessor.SetOrUpdateBaseValue(TestKeys.Mock("Strength"), 50f);

            // 2. Link it to the main processor (e.g., the Sword)
            _processor.RegisterExternalProvider(TestKeys.Mock("Owner"), ownerProcessor);

            // 3. Try to access "Owner.Strength" through the main processor

            var AttributeName = TestKeys.Mock("Strength");
            var ProviderPath = new List<SemanticKey> { TestKeys.Mock("Owner") };
            var attr = _processor.GetAttribute(AttributeName, ProviderPath);

            // Assert
            Assert.IsNotNull(attr, "Should find attribute via provider link");
            Assert.AreEqual(50f, attr.BaseValue);
        }

        [Test]
        public void ExternalProvider_ReactiveObservable_Works()
        {
            // 1. Setup
            var ownerProcessor = new AttributeProcessor();
            _processor.RegisterExternalProvider(TestKeys.Mock("Owner"), ownerProcessor);

            float lastValue = 0f;

            var AttributeName = TestKeys.Mock("Strength");
            var ProviderPath = new List<SemanticKey> { TestKeys.Mock("Owner") };
            // 2. Subscribe BEFORE the attribute even exists on the owner
            // This tests your reactive pipeline's robustness
            _processor.GetAttributeObservable(AttributeName, ProviderPath)
                .SelectMany(attr => attr.ReactivePropertyAccess)
                .Subscribe(val => lastValue = val);

            // 3. Now create/update the value on the owner
            ownerProcessor.SetOrUpdateBaseValue(TestKeys.Mock("Strength"), 99f);

            // Assert
            Assert.AreEqual(99f, lastValue);
        }

        [Test]
        public void Modifier_ModifiesValue_Correctly()
        {
            // Arrange
            _processor.SetOrUpdateBaseValue(TestKeys.Mock("Speed"), 10f);

            // Create a mock modifier (or use a concrete one if easier)
            // Using LinearAttributeModifier since we have the source
            var source = new ValueSource { Mode = ValueSource.SourceMode.Constant, ConstantValue = 5f };

            var scalingSpec = new AttributeModifierSpec
            {
                SourceId = "SpeedScaling",
                Type = ModifierType.Additive,
                Priority = 0,
                LogicType = sk.Modifiers.Linear,
                Arguments = new List<ValueSource> { source, ValueSource.Const(1f), ValueSource.Const(0f) }
            };

            var mod = new LinearModifier(scalingSpec);

            // Act
            _processor.AddModifier("TestIDforSpeed", mod, TestKeys.Mock("Speed"));

            // Assert
            // 10 (Base) + 5 (Mod) = 15
            Assert.AreEqual(15f, _processor.GetAttribute(TestKeys.Mock("Speed")).ReactivePropertyAccess.Value);
        }

        [Test]
        public void StatBlock_AppliesModifiers_Correctly()
        {
            // Arrange
            _processor.SetOrUpdateBaseValue(TestKeys.Mock("Speed"), 10f);

            // Create a StatBlock (Simulating your JSON load)
            var block = new StatBlock();

            // Replicate the +50% Multiplier Logic
            // Linear Formula: (Input * Coeff) + Addend
            // Input = 1.5, Coeff = 1.0, Addend = 0.0 -> Result = 1.5
            // Type: Multiplicative -> 10 (Base) * 1.5 (Result) = 15

            var spec = new AttributeModifierSpec
            {
                TargetAttribute = TestKeys.Mock("Speed"),
                SourceId = "HasteSpell",
                Type = ModifierType.Multiplicative,
                Priority = 100,

                // NEW ARCHITECTURE: Define LogicType and Arguments
                LogicType = sk.Modifiers.Linear,

                // Argument Order for Linear: [Input, Coeff, Addend]
                Arguments = new List<ValueSource>
                {
                    ValueSource.Const(1.5f), // Input
                    ValueSource.Const(1.0f), // Coeff
                    ValueSource.Const(0.0f)  // Addend
                }
            };

            block.Modifiers = new List<AttributeModifierSpec> { spec };

            // Act
            block.ApplyToProcessor(_processor, _factory);

            // Assert
            var finalValue = _processor.GetAttribute(TestKeys.Mock("Speed")).ReactivePropertyAccess.Value;
            Assert.AreEqual(15f, finalValue, $"Expected 15, but got {finalValue}. Check if ModifierType.Multiplicative is handled correctly.");
        }


        [Test]
        public void RaceCondition_ModifierWaitsFor_ExternalProvider()
        {
            // 1. Setup Weapon Base Damage
            _processor.SetOrUpdateBaseValue(TestKeys.Mock("Damage"), 10f);

            // 2. Add Modifier dependent on MISSING Provider ("Owner")
            // Logic: Damage += Owner.Strength * 1
            // Linear Formula: (Owner.Strength * 1.0) + 0.0

            var source = new ValueSource
            {
                Mode = ValueSource.SourceMode.Attribute,
                AttributeRef = new AttributeReference(TestKeys.Mock("Strength"), new List<SemanticKey> { TestKeys.Mock("Owner") })              
            };

            var scalingSpec = new AttributeModifierSpec
            {
                SourceId = "StrengthScaling",
                Type = ModifierType.Additive,
                Priority = 0,
                LogicType = sk.Modifiers.Linear,
                Arguments = new List<ValueSource> { source, ValueSource.Const(1f), ValueSource.Const(0f) }
            };


            var mod = new LinearModifier(scalingSpec);

            _processor.AddModifier("ScalingMod", mod, TestKeys.Mock("Damage"));

            // 3. Verify Intermediate State
            // The pipeline for "Damage" is now waiting for "Owner.Strength".
            Assert.AreEqual(10f, _processor.GetAttribute(TestKeys.Mock("Damage")).ReactivePropertyAccess.Value,
                "Value should hold steady (Base Value) while waiting for external provider");

            // 4. Create Owner and Register (The "Late Arrival")
            var ownerProcessor = new AttributeProcessor();
            ownerProcessor.SetOrUpdateBaseValue(TestKeys.Mock("Strength"), 5f); // Owner arrives with 5 Strength

            _processor.RegisterExternalProvider(TestKeys.Mock("Owner"), ownerProcessor);

            // 5. Assert Final Update
            // Now the link is established, the modifier calculates (5 * 1 + 0 = 5), and Damage becomes 10 + 5 = 15.
            Assert.AreEqual(15f, _processor.GetAttribute(TestKeys.Mock("Damage")).ReactivePropertyAccess.Value,
                "Value should automatically update once the external provider is registered");
        }

    }
}