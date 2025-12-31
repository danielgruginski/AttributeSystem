using NUnit.Framework;
using ReactiveSolutions.AttributeSystem.Core;
using ReactiveSolutions.AttributeSystem.Core.Data;
using ReactiveSolutions.AttributeSystem.Core.Modifiers;
using SemanticKeys;
using System.Collections.Generic;
using UniRx;

namespace ReactiveSolutions.AttributeSystem.Tests
{
    public class ModifierTests
    {
        private AttributeProcessor _processor;
        private IModifierFactory _factory;

        // --- Helpers ---
        private ValueSource Const(float val)
            => new ValueSource { Mode = ValueSource.SourceMode.Constant, ConstantValue = val };

        private ValueSource Attr(string name)
        {
            return new ValueSource
            {
                Mode = ValueSource.SourceMode.Attribute,
                AttributeRef = new AttributeReference(TestKeys.Mock(name))
            };
        }

        private AttributeModifierSpec CreateSpec(string id, params ValueSource[] args)
        {
            return new AttributeModifierSpec
            {
                SourceId = id,
                Type = ModifierType.Additive,
                Priority = 0,
                Arguments = new List<ValueSource>(args),
                LogicType = TestKeys.Mock(id) // For testing, we mock the logic type key
            };
        }

        // Helper specifically for factory creation where LogicType matters
        private AttributeModifierSpec CreateSpecForFactory(string logicTypeId, string sourceId, params ValueSource[] args)
        {
            return new AttributeModifierSpec
            {
                SourceId = sourceId,
                Type = ModifierType.Additive,
                Priority = 0,
                Arguments = new List<ValueSource>(args),
                LogicType = TestKeys.Mock(logicTypeId)
            };
        }


        [SetUp]
        public void Setup()
        {
            _processor = new AttributeProcessor();
            _factory = new ModifierFactory(); // Instantiate the service
        }

        // ========================================================================
        // 1. STANDARD CLASS-BASED MODIFIERS
        // ========================================================================

        [Test]
        public void LinearModifier_CalculatesCorrectly()
        {
            _processor.SetOrUpdateBaseValue(TestKeys.Mock("BaseStat"), 10f);

            var spec = CreateSpec("LinearTest", Attr("BaseStat"), Const(2f), Const(5f));
            var mod = new LinearModifier(spec); // Direct instantiation using Spec

            float result = 0;
            mod.GetMagnitude(_processor).Subscribe(x => result = x);

            Assert.AreEqual(25f, result);
        }

        [Test]
        public void PolynomialModifier_CalculatesCorrectly()
        {
            _processor.SetOrUpdateBaseValue(TestKeys.Mock("BaseStat"), 2f);
            var spec = CreateSpec("PolyTest", Attr("BaseStat"), Const(3f), Const(2f), Const(1f));
            var mod = new PolynomialModifier(spec);

            float result = 0;
            mod.GetMagnitude(_processor).Subscribe(x => result = x);

            Assert.AreEqual(17f, result);
        }

        // ========================================================================
        // 2. FUNCTIONAL MODIFIERS (Using the Factory Instance)
        // ========================================================================

        [Test]
        public void Factory_Creates_ClampModifier()
        {
            // Case A: Clamped by Max
            var specHigh = CreateSpecForFactory("Clamp", "ClampTestHigh", Const(100f), Const(0f), Const(50f));
            // We need to use the actual semantic key string for the factory lookup if we are mocking keys
            // But here we use the sk.Modifiers keys which use GUIDs. 
            // Let's use the Factory Create overload that takes the ID string directly for clarity in tests
            // OR ensure our CreateSpec sets the right LogicType.

            // Refactor: ModifierFactory.Create(spec) uses spec.LogicType.
            // But for tests, we might want to use the string ID overload: factory.Create(id, spec)

            // Let's use the string overload for clarity as we did before
            var modHigh = _factory.Create(sk.Modifiers.Clamp, specHigh);

            float resultHigh = 0;
            modHigh.GetMagnitude(_processor).Subscribe(x => resultHigh = x);
            Assert.AreEqual(50f, resultHigh, "Failed to clamp to Max");

            // Case B: Clamped by Min
            var specLow = CreateSpecForFactory("Clamp", "ClampTestLow", Const(-10f), Const(0f), Const(50f));
            var modLow = _factory.Create(sk.Modifiers.Clamp, specLow);

            float resultLow = 0;
            modLow.GetMagnitude(_processor).Subscribe(x => resultLow = x);
            Assert.AreEqual(0f, resultLow, "Failed to clamp to Min");
        }

        [Test]
        public void Factory_Creates_MinMaxModifiers()
        {
            // Min
            var minSpec = CreateSpecForFactory("Min", "MinTest", Const(10f), Const(5f));
            var minMod = _factory.Create(sk.Modifiers.Min, minSpec);
            float minResult = 0;
            minMod.GetMagnitude(_processor).Subscribe(x => minResult = x);
            Assert.AreEqual(5f, minResult);

            // Max
            var maxSpec = CreateSpecForFactory("Max", "MaxTest", Const(10f), Const(5f));
            var maxMod = _factory.Create(sk.Modifiers.Max, maxSpec);
            float maxResult = 0;
            maxMod.GetMagnitude(_processor).Subscribe(x => maxResult = x);
            Assert.AreEqual(10f, maxResult);
        }

        [Test]
        public void Factory_Creates_StepModifier()
        {
            var spec0 = CreateSpecForFactory("Step", "StepTest0", Const(10f), Const(5f));
            float res0 = 0;
            _factory.Create(sk.Modifiers.Step, spec0).GetMagnitude(_processor).Subscribe(x => res0 = x);
            Assert.AreEqual(0f, res0);

            var spec1 = CreateSpecForFactory("Step", "StepTest1", Const(10f), Const(15f));
            float res1 = 0;
            _factory.Create(sk.Modifiers.Step, spec1).GetMagnitude(_processor).Subscribe(x => res1 = x);
            Assert.AreEqual(1f, res1);
        }

        // ========================================================================
        // 3. ADVANCED: HYBRID DEPENDENCIES
        // ========================================================================

        [Test]
        public void UnifiedArchitecture_Allows_AttributesAsParameters()
        {
            _processor.SetOrUpdateBaseValue(TestKeys.Mock("CurrentMana"), 150f);
            _processor.SetOrUpdateBaseValue(TestKeys.Mock("MaxMana"), 100f);

            var spec = CreateSpecForFactory("Clamp", "DynamicClamp",
                Attr("CurrentMana"), // Input
                Const(0f),           // Min
                Attr("MaxMana")      // Max
            );

            var mod = _factory.Create(sk.Modifiers.Clamp, spec);

            float result = 0;
            mod.GetMagnitude(_processor).Subscribe(x => result = x);

            Assert.AreEqual(100f, result);

            _processor.SetOrUpdateBaseValue(TestKeys.Mock("MaxMana"), 200f);
            Assert.AreEqual(150f, result);
        }

        [Test]
        public void Fallback_ToStatic_IfTypeUnknown()
        {
            var spec = CreateSpec("Unknown", Const(42f));
            // We purposely use a key that is NOT registered
            var mod = _factory.Create("ThisDoesNotExist", spec);

            Assert.IsInstanceOf<StaticAttributeModifier>(mod);

            float result = 0;
            mod.GetMagnitude(_processor).Subscribe(x => result = x);
            Assert.AreEqual(42f, result);
        }
    }
}