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

        private ModifierArgs Args(string id, params ValueSource[] args)
        {
            return new ModifierArgs(
                id,
                ModifierType.Additive,
                0,
                new List<ValueSource>(args)
            );
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

            var args = Args("LinearTest", Attr("BaseStat"), Const(2f), Const(5f));
            var mod = new LinearModifier(args); // Direct instantiation still works for unit tests

            float result = 0;
            mod.GetMagnitude(_processor).Subscribe(x => result = x);

            Assert.AreEqual(25f, result);
        }

        [Test]
        public void PolynomialModifier_CalculatesCorrectly()
        {
            _processor.SetOrUpdateBaseValue(TestKeys.Mock("BaseStat"), 2f);
            var args = Args("PolyTest", Attr("BaseStat"), Const(3f), Const(2f), Const(1f));
            var mod = new PolynomialModifier(args);

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
            var argsHigh = Args("ClampTest", Const(100f), Const(0f), Const(50f));
            var modHigh = _factory.Create(sk.Modifiers.Clamp, argsHigh); // Use instance

            float resultHigh = 0;
            modHigh.GetMagnitude(_processor).Subscribe(x => resultHigh = x);
            Assert.AreEqual(50f, resultHigh, "Failed to clamp to Max");

            // Case B: Clamped by Min
            var argsLow = Args("ClampTest", Const(-10f), Const(0f), Const(50f));
            var modLow = _factory.Create(sk.Modifiers.Clamp, argsLow);

            float resultLow = 0;
            modLow.GetMagnitude(_processor).Subscribe(x => resultLow = x);
            Assert.AreEqual(0f, resultLow, "Failed to clamp to Min");
        }

        [Test]
        public void Factory_Creates_MinMaxModifiers()
        {
            // Min
            var minArgs = Args("MinTest", Const(10f), Const(5f));
            var minMod = _factory.Create(sk.Modifiers.Min, minArgs);
            float minResult = 0;
            minMod.GetMagnitude(_processor).Subscribe(x => minResult = x);
            Assert.AreEqual(5f, minResult);

            // Max
            var maxArgs = Args("MaxTest", Const(10f), Const(5f));
            var maxMod = _factory.Create(sk.Modifiers.Max, maxArgs);
            float maxResult = 0;
            maxMod.GetMagnitude(_processor).Subscribe(x => maxResult = x);
            Assert.AreEqual(10f, maxResult);
        }

        [Test]
        public void Factory_Creates_StepModifier()
        {
            var args0 = Args("StepTest", Const(10f), Const(5f));
            float res0 = 0;
            _factory.Create(sk.Modifiers.Step, args0).GetMagnitude(_processor).Subscribe(x => res0 = x);
            Assert.AreEqual(0f, res0);

            var args1 = Args("StepTest", Const(10f), Const(15f));
            float res1 = 0;
            _factory.Create(sk.Modifiers.Step, args1).GetMagnitude(_processor).Subscribe(x => res1 = x);
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

            var args = Args("DynamicClamp",
                Attr("CurrentMana"), // Input
                Const(0f),           // Min
                Attr("MaxMana")      // Max
            );

            var mod = _factory.Create(sk.Modifiers.Clamp, args);

            float result = 0;
            mod.GetMagnitude(_processor).Subscribe(x => result = x);

            Assert.AreEqual(100f, result);

            _processor.SetOrUpdateBaseValue(TestKeys.Mock("MaxMana"), 200f);
            Assert.AreEqual(150f, result);
        }

        [Test]
        public void Fallback_ToStatic_IfTypeUnknown()
        {
            var args = Args("Unknown", Const(42f));
            var mod = _factory.Create(TestKeys.Mock("ThisDoesNotExist"), args);

            Assert.IsInstanceOf<StaticAttributeModifier>(mod);

            float result = 0;
            mod.GetMagnitude(_processor).Subscribe(x => result = x);
            Assert.AreEqual(42f, result);
        }
    }
}