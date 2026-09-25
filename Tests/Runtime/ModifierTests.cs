using NUnit.Framework;
using ReactiveSolutions.AttributeSystem.Core;
using ReactiveSolutions.AttributeSystem.Core.Data;
using ReactiveSolutions.AttributeSystem.Core.Modifiers;
using SemanticKeys;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;

namespace ReactiveSolutions.AttributeSystem.Tests
{
    /// <summary>
    /// A custom logic type, as a user would write it: no registration needed.
    /// </summary>
    [Serializable]
    public class TestBonusPerLevelLogic : FormulaLogic
    {
        public ValueSource Level = ValueSource.Const(0f);
        public float BonusPerLevel = 2f;

        protected override IEnumerable<ValueSource> Inputs => new[] { Level };
        protected override float Compute(IList<float> inputs) => inputs[0] * BonusPerLevel;
    }

    public class ModifierTests
    {
        private Entity _processor;

        private static ValueSource Attr(string name) => ValueSource.FromAttribute(TestKeys.Mock(name));

        private float Evaluate(ModifierLogic logic)
        {
            float result = float.NaN;
            logic.Observe(_processor).Subscribe(x => result = x);
            return result;
        }

        [SetUp]
        public void Setup()
        {
            _processor = new Entity();
        }

        // ========================================================================
        // 1. BUILT-IN LOGIC
        // ========================================================================

        [Test]
        public void Value_ReturnsItsInput()
        {
            _processor.SetOrUpdateBaseValue(TestKeys.Mock("Strength"), 7f);

            Assert.AreEqual(3f, Evaluate(new ValueLogic(3f)));
            Assert.AreEqual(7f, Evaluate(new ValueLogic(Attr("Strength"))));
        }

        [Test]
        public void Linear_CalculatesCorrectly()
        {
            _processor.SetOrUpdateBaseValue(TestKeys.Mock("BaseStat"), 10f);

            Assert.AreEqual(25f, Evaluate(new LinearLogic { Input = Attr("BaseStat"), Coefficient = 2f, Addend = 5f }));
            Assert.AreEqual(10f, Evaluate(new LinearLogic { Input = Attr("BaseStat") }), "Coefficient defaults to 1, Addend to 0.");
        }

        [Test]
        public void Polynomial_CalculatesCorrectly()
        {
            _processor.SetOrUpdateBaseValue(TestKeys.Mock("BaseStat"), 2f);

            Assert.AreEqual(17f, Evaluate(new PolynomialLogic { Input = Attr("BaseStat"), Power = 3f, Scale = 2f, Flat = 1f }));
        }

        [Test]
        public void Clamp_LimitsItsInput()
        {
            Assert.AreEqual(50f, Evaluate(new ClampLogic { Input = 100f, Min = 0f, Max = 50f }), "Failed to clamp to Max");
            Assert.AreEqual(0f, Evaluate(new ClampLogic { Input = -10f, Min = 0f, Max = 50f }), "Failed to clamp to Min");
        }

        [Test]
        public void MinMax_PickTheSmallerOrLarger()
        {
            Assert.AreEqual(5f, Evaluate(new MinLogic { A = 10f, B = 5f }));
            Assert.AreEqual(10f, Evaluate(new MaxLogic { A = 10f, B = 5f }));
        }

        [Test]
        public void Step_IsOneFromTheThreshold()
        {
            Assert.AreEqual(0f, Evaluate(new StepLogic { Input = 5f, Threshold = 10f }));
            Assert.AreEqual(1f, Evaluate(new StepLogic { Input = 10f, Threshold = 10f }));
            Assert.AreEqual(1f, Evaluate(new StepLogic { Input = 15f, Threshold = 10f }));
        }

        [Test]
        public void Floor_Ratio_Exponential()
        {
            Assert.AreEqual(3f, Evaluate(new FloorLogic { Input = 3.7f }));
            Assert.AreEqual(2.5f, Evaluate(new RatioLogic { Dividend = 5f, Divisor = 2f }));
            Assert.AreEqual(5f, Evaluate(new RatioLogic { Dividend = 5f, Divisor = 0f }), "A zero divisor returns the dividend.");
            Assert.AreEqual(8f, Evaluate(new ExponentialLogic { Base = 2f, Exponent = 3f }));
        }

        [Test]
        public void DiminishingReturns_ApproachesMaxBonus()
        {
            Assert.AreEqual(50f, Evaluate(new DiminishingReturnsLogic { Input = 100f, MaxBonus = 100f, SoftCap = 100f }));
            Assert.AreEqual(0f, Evaluate(new DiminishingReturnsLogic { Input = 0f, MaxBonus = 100f, SoftCap = 0f }), "No NaN when Input + Soft Cap is 0.");
            Assert.AreEqual(0f, Evaluate(new DiminishingReturnsLogic { Input = -5f, MaxBonus = 100f, SoftCap = 10f }), "A negative input counts as 0.");
        }

        [Test]
        public void ScaledTriangular_FollowsTriangularNumbers()
        {
            // Scale 1: 1 + 2 + 3 = 6 points reach level 3.
            Assert.AreEqual(3f, Evaluate(new ScaledTriangularLogic { Input = 6f, Scale = 1f }), 0.0001f);
            Assert.AreEqual(0f, Evaluate(new ScaledTriangularLogic { Input = -1f, Scale = 1f }));
        }

        [Test]
        public void Segmented_UsesTheHighestThresholdReached()
        {
            var logic = new SegmentedLogic
            {
                Input = Attr("Strength"),
                Default = 1f,
                Segments =
                {
                    new SegmentedLogic.Segment { Threshold = 20f, Value = 2f },
                    new SegmentedLogic.Segment { Threshold = 10f, Value = 1.5f },
                }
            };
            float result = float.NaN;
            logic.Observe(_processor).Subscribe(x => result = x);

            var strength = TestKeys.Mock("Strength");
            _processor.SetOrUpdateBaseValue(strength, 5f);
            Assert.AreEqual(1f, result);
            _processor.SetOrUpdateBaseValue(strength, 10f);
            Assert.AreEqual(1.5f, result);
            _processor.SetOrUpdateBaseValue(strength, 25f);
            Assert.AreEqual(2f, result);
        }

        // ========================================================================
        // 2. ATTRIBUTES AS INPUTS
        // ========================================================================

        [Test]
        public void AttributeInputs_UpdateTheValue()
        {
            _processor.SetOrUpdateBaseValue(TestKeys.Mock("CurrentMana"), 150f);
            _processor.SetOrUpdateBaseValue(TestKeys.Mock("MaxMana"), 100f);

            float result = 0;
            new ClampLogic { Input = Attr("CurrentMana"), Min = 0f, Max = Attr("MaxMana") }
                .Observe(_processor).Subscribe(x => result = x);

            Assert.AreEqual(100f, result);

            _processor.SetOrUpdateBaseValue(TestKeys.Mock("MaxMana"), 200f);
            Assert.AreEqual(150f, result);
        }

        // ========================================================================
        // 3. CUSTOM LOGIC AND SPECS
        // ========================================================================

        [Test]
        public void CustomLogic_WorksInAStatBlockWithoutRegistration()
        {
            var level = TestKeys.Mock("Level");
            var damage = TestKeys.Mock("Damage");
            _processor.SetOrUpdateBaseValue(level, 3f);

            var block = new StatBlock
            {
                Modifiers =
                {
                    new AttributeModifierSpec
                    {
                        TargetAttribute = damage,
                        Logic = new TestBonusPerLevelLogic { Level = ValueSource.FromAttribute(level), BonusPerLevel = 5f }
                    }
                }
            };
            block.ApplyToEntity(_processor);

            Assert.AreEqual(15f, _processor.GetAttribute(damage).ObservableValue.Value);

            _processor.SetOrUpdateBaseValue(level, 4f);
            Assert.AreEqual(20f, _processor.GetAttribute(damage).ObservableValue.Value);
        }

        [Test]
        public void LogicModifier_CarriesTypeAndPriority()
        {
            var health = TestKeys.Mock("Health");
            _processor.SetOrUpdateBaseValue(health, 100f);

            _processor.AddModifier("Blessing", new LogicModifier(new ValueLogic(1.5f), ModifierType.Multiplicative, sourceId: "Blessing"), health);
            _processor.AddModifier("Ring", new LogicModifier(new ValueLogic(20f)), health);

            // Adds apply before multipliers at the same priority: (100 + 20) * 1.5
            Assert.AreEqual(180f, _processor.GetAttribute(health).ObservableValue.Value);
        }

        [Test]
        public void SpecWithoutLogic_IsSkippedWithAWarning()
        {
            var damage = TestKeys.Mock("Damage");
            var block = new StatBlock
            {
                BlockName = "Broken",
                Modifiers = { new AttributeModifierSpec { TargetAttribute = damage, Logic = null } }
            };
            LogAssert.Expect(LogType.Warning, new Regex("'Broken': skipped a modifier on 'Damage' with no Logic"));

            block.ApplyToEntity(_processor);

            Assert.IsNull(_processor.GetAttribute(damage));
        }
    }
}