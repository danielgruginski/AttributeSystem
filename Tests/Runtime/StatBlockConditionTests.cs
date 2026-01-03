using NUnit.Framework;
using ReactiveSolutions.AttributeSystem.Core;
using ReactiveSolutions.AttributeSystem.Core.Data;
using SemanticKeys;
using sk;
using System.Collections.Generic;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Tests
{
    public class StatBlockConditionTests
    {
        private AttributeProcessor _processor;
        private IModifierFactory _factory;

        private SemanticKey Strength = TestKeys.Mock("Strength");
        private SemanticKey LogicTypeStatic = Modifiers.Static;
        private SemanticKey IsEnraged = TestKeys.Mock("IsEnraged");
        private SemanticKey IsQuiet = TestKeys.Mock("IsQuiet");
        private SemanticKey Stealth = TestKeys.Mock("Stealth");
        private SemanticKey Health = TestKeys.Mock("Health");
        private SemanticKey DamageBonus = TestKeys.Mock("DamageBonus");
        private SemanticKey Stunned = TestKeys.Mock("Stunned");
        private SemanticKey Frozen = TestKeys.Mock("Frozen");
        private SemanticKey Defense = TestKeys.Mock("Defense");

        private ValueSource Const(float val)
    => new ValueSource { Mode = ValueSource.SourceMode.Constant, ConstantValue = val };

        [SetUp]
        public void Setup()
        {
            _processor = new AttributeProcessor();
            _factory = new ModifierFactory();
        }

        [Test]
        public void Test_AlwaysCondition()
        {
            var statBlock = new StatBlock();
            statBlock.ActivationCondition = new StatBlockCondition { Type = StatBlockCondition.Mode.Always };
            statBlock.Modifiers.Add(new AttributeModifierSpec
            {
                LogicType = LogicTypeStatic,
                TargetAttribute = Strength,
                Type = ModifierType.Additive,
                Arguments = new List<ValueSource> { Const(5f) }
            });

            // Act
            statBlock.ApplyToProcessor(_processor, _factory);


            _processor.SetOrUpdateBaseValue(Strength, 0);

            // Assert
            Assert.AreEqual(5, _processor.GetAttribute(Strength).ReactivePropertyAccess.Value);
        }

        [Test]
        public void Test_TagCondition_Active()
        {
            var tag = IsEnraged;
            _processor.AddTag(tag);

            var statBlock = new StatBlock();
            statBlock.ActivationCondition = new StatBlockCondition
            {
                Type = StatBlockCondition.Mode.Tag,
                Tag = tag,
                TagTarget = new List<SemanticKey>()
            };
            statBlock.Modifiers.Add(new AttributeModifierSpec
            {
                LogicType = LogicTypeStatic,
                Type = ModifierType.Additive,
                TargetAttribute = Strength,
                Arguments = new List<ValueSource> { Const(10) }
            });

            statBlock.ApplyToProcessor(_processor, _factory);


            _processor.SetOrUpdateBaseValue(Strength, 0);

            Assert.AreEqual(10, _processor.GetAttribute(Strength).ReactivePropertyAccess.Value);
        }

        [Test]
        public void Test_TagCondition_Inactive()
        {
            var tag = IsEnraged;
            // Tag NOT added

            var statBlock = new StatBlock();
            statBlock.ActivationCondition = new StatBlockCondition
            {
                Type = StatBlockCondition.Mode.Tag,
                Tag = tag,
                TagTarget = new List<SemanticKey>()
            };
            statBlock.Modifiers.Add(new AttributeModifierSpec
            {
                LogicType = LogicTypeStatic,
                Type = ModifierType.Additive,
                TargetAttribute = Strength,
                TargetPath = new List<SemanticKey>(),
                Arguments = new List<ValueSource> { Const(10) }
            });

            _processor.SetOrUpdateBaseValue(Strength, 0);

            var activeStatBlock = statBlock.ApplyToProcessor(_processor, _factory);

            Assert.AreEqual(0, _processor.GetAttribute(Strength).ReactivePropertyAccess.Value);
        }

        [Test]
        public void Test_TagCondition_Inverted()
        {
            var tag = IsQuiet;
            // Tag NOT added, condition is inverted (True if missing)

            var statBlock = new StatBlock();
            statBlock.ActivationCondition = new StatBlockCondition
            {
                Type = StatBlockCondition.Mode.Tag,
                Tag = tag,
                InvertTag = true,
                TagTarget = new List<SemanticKey>()
            };
            statBlock.Modifiers.Add(new AttributeModifierSpec
            {
                LogicType = LogicTypeStatic,
                Type = ModifierType.Additive,
                TargetAttribute = Stealth,
                Arguments = new List<ValueSource> { Const(5) }
            });

            statBlock.ApplyToProcessor(_processor, _factory);

            _processor.SetOrUpdateBaseValue(Stealth, 0);

            Assert.AreEqual(5, _processor.GetAttribute(Stealth).ReactivePropertyAccess.Value);

            // Add tag -> Should deactivate
            _processor.AddTag(tag);
            Assert.AreEqual(0, _processor.GetAttribute(Stealth).ReactivePropertyAccess.Value);
        }

        [Test]
        public void Test_ValueComparison_Greater()
        {
            _processor.SetOrUpdateBaseValue(Health, 50);

            var statBlock = new StatBlock();
            statBlock.ActivationCondition = new StatBlockCondition
            {
                Type = StatBlockCondition.Mode.ValueComparison,
                ValueA = new ValueSource { Mode = ValueSource.SourceMode.Attribute, AttributeRef = new AttributeReference { Name = Health } },
                CompareOp = StatBlockCondition.Comparison.Greater,
                ValueB = Const(30)
            };
            statBlock.Modifiers.Add(new AttributeModifierSpec
            {
                LogicType = LogicTypeStatic,
                Type = ModifierType.Additive,
                TargetAttribute = DamageBonus,
                Arguments = new List<ValueSource> { Const(10) }
            });

            statBlock.ApplyToProcessor(_processor, _factory);


            _processor.SetOrUpdateBaseValue(DamageBonus, 0);

            // 50 > 30 -> Active
            Assert.AreEqual(10, _processor.GetAttribute(DamageBonus).ReactivePropertyAccess.Value);

            // Lower Health -> Inactive
            _processor.SetOrUpdateBaseValue(Health, 20);
            Assert.AreEqual(0, _processor.GetAttribute(DamageBonus).ReactivePropertyAccess.Value);
        }

        [Test]
        public void Test_Composite_OR_Condition()
        {
            var tagA = Stunned;
            var tagB = Frozen;

            var statBlock = new StatBlock();
            statBlock.ActivationCondition = new StatBlockCondition
            {
                Type = StatBlockCondition.Mode.Composite,
                GroupOp = StatBlockCondition.Operator.Or,
                SubConditions = new List<StatBlockCondition>
                {
                    new StatBlockCondition { Type = StatBlockCondition.Mode.Tag, Tag = tagA,
                TagTarget = new List<SemanticKey>() },
                    new StatBlockCondition { Type = StatBlockCondition.Mode.Tag, Tag = tagB,
                TagTarget = new List<SemanticKey>() }
                }
            };
            statBlock.Modifiers.Add(new AttributeModifierSpec
            {
                LogicType = LogicTypeStatic,
                Type = ModifierType.Additive,
                TargetAttribute = Defense,
                Arguments = new List<ValueSource> { Const(100) }
            });

            statBlock.ApplyToProcessor(_processor, _factory);


            _processor.SetOrUpdateBaseValue(Defense, 0);

            // Initially false
            Assert.AreEqual(0, _processor.GetAttribute(Defense).ReactivePropertyAccess.Value);

            // Add A -> True
            _processor.AddTag(tagA);
            Assert.AreEqual(100, _processor.GetAttribute(Defense).ReactivePropertyAccess.Value);

            // Remove A -> False
            _processor.RemoveTag(tagA);
            Assert.AreEqual(0, _processor.GetAttribute(Defense).ReactivePropertyAccess.Value);

            // Add B -> True
            _processor.AddTag(tagB);
            Assert.AreEqual(100, _processor.GetAttribute(Defense).ReactivePropertyAccess.Value);
        }
    }
}