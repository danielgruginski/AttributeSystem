using NUnit.Framework;
using ReactiveSolutions.AttributeSystem.Core;
using ReactiveSolutions.AttributeSystem.Core.Data;
using SemanticKeys;
using sk;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Tests
{
    public class LinkGroupTests
    {
        private ModifierFactory _modifierFactory;
        private SemanticKey _testAttr;
        private SemanticKey _testTag;

        [SetUp]
        public void Setup()
        {
            _modifierFactory = new ModifierFactory();
            _testAttr = new SemanticKey("Strength", "Strength", null);
            _testTag = new SemanticKey("Blessed", "Blessed", null);
        }

        private ValueSource Const(float val)
            => new ValueSource { Mode = ValueSource.SourceMode.Constant, ConstantValue = val };

        // Helper to create a StatBlock with a simple flat modifier in memory
        private StatBlock CreateStatBlock(float value)
        {
            var statBlock = new StatBlock();

            statBlock.ActivationCondition = new StatBlockCondition { Type = StatBlockCondition.Mode.Always };

            // We assume StatBlock has a public list 'Modifiers' based on the coding style of this project
            // If the field name differs, this helper will need adjustment.
            var spec = new AttributeModifierSpec
            {
                LogicType = Modifiers.Static,
                TargetAttribute = _testAttr, // Assuming 'AttributeKey' or 'Attribute' property
                Type = ModifierType.Additive, // Assuming 'ModifierType' or 'Type' enum
                Arguments = new List<ValueSource> { Const(value) }
            };

            // ADD THE SPEC TO THE STATBLOCK
            statBlock.Modifiers.Add(spec);

            // Removed statBlock.Tags.Add(_testTag) to prevent the self-sustaining feedback loop in condition tests.

            return statBlock;
        }

        [Test]
        public void LinkGroup_ManageMembers_AddsAndRemoves()
        {
            var group = new LinkGroup();
            var p1 = new AttributeProcessor();
            var p2 = new AttributeProcessor();

            group.AddMember(p1);
            Assert.IsTrue(group.Contains(p1));
            Assert.IsFalse(group.Contains(p2));

            group.AddMember(p2);
            Assert.IsTrue(group.Contains(p2));
            Assert.AreEqual(2, group.Members.Count);

            group.RemoveMember(p1);
            Assert.IsFalse(group.Contains(p1));
            Assert.AreEqual(1, group.Members.Count);
        }

        [Test]
        public void ApplyStatBlock_AppliesToExistingMembers()
        {
            var group = new LinkGroup();
            var processor = new AttributeProcessor();
            group.AddMember(processor);

            var statBlock = CreateStatBlock(10f);

            // Act
            var handle = group.ApplyStatBlock(statBlock, _modifierFactory);

            // Assert
            var attr = processor.GetAttribute(_testAttr);
            Assert.IsNotNull(attr);
            Assert.AreEqual(10f, attr.ObservableValue.Value);

            handle.Dispose();
        }

        [Test]
        public void ApplyStatBlock_AppliesToNewMembers_Automatically()
        {
            var group = new LinkGroup();
            var statBlock = CreateStatBlock(50f);

            // Act - Apply first, add member later
            using (group.ApplyStatBlock(statBlock, _modifierFactory))
            {
                var processor = new AttributeProcessor();

                // Should initially be 0 (or default)?
                Assert.AreEqual(0f, processor.GetAttribute(_testAttr)?.ObservableValue.Value ?? 0f);

                group.AddMember(processor);

                // Should now have +50
                Assert.AreEqual(50f, processor.GetAttribute(_testAttr).ObservableValue.Value);
            }
        }

        [Test]
        public void ApplyStatBlock_RemovesModifier_WhenMemberRemoved()
        {
            var group = new LinkGroup();
            var processor = new AttributeProcessor();
            group.AddMember(processor);

            var statBlock = CreateStatBlock(10f);

            using (group.ApplyStatBlock(statBlock, _modifierFactory))
            {
                Assert.AreEqual(10f, processor.GetAttribute(_testAttr).ObservableValue.Value);

                group.RemoveMember(processor);

                // Modifier should be stripped
                Assert.AreEqual(0f, processor.GetAttribute(_testAttr).ObservableValue.Value);
            }
        }

        [Test]
        public void ApplyStatBlock_WithCondition_ReactsToTagChanges()
        {
            var group = new LinkGroup();
            var processor = new AttributeProcessor();
            group.AddMember(processor);

            var statBlock = CreateStatBlock(100f);

            var condition = new StatBlockCondition
            {
                Type = StatBlockCondition.Mode.Tag,
                Tag = _testTag,
                InvertTag = false
            };

            using (group.ApplyStatBlock(statBlock, _modifierFactory, condition))
            {
                // 1. Tag missing -> No Modifier
                Assert.AreEqual(true, processor.GetAttribute(_testAttr) == null); // the default value when not found should be 0f

                //Assert.AreEqual(0f, processor.GetAttribute(_testAttr)?.Value.Value ?? 0f); // alternative test

                // 2. Add Tag -> Modifier Applied
                processor.AddTag(_testTag);
                Assert.AreEqual(100f, processor.GetAttribute(_testAttr).ObservableValue.Value);

                // 3. Remove Tag -> Modifier Removed
                processor.RemoveTag(_testTag);
                Assert.AreEqual(0f, processor.GetAttribute(_testAttr).ObservableValue.Value);
            }
        }

        [Test]
        public void ApplyStatBlock_Dispose_ClearsAllModifiers()
        {
            var group = new LinkGroup();
            var p1 = new AttributeProcessor();
            var p2 = new AttributeProcessor();
            group.AddMember(p1);
            group.AddMember(p2);

            var statBlock = CreateStatBlock(20f);
            var handle = group.ApplyStatBlock(statBlock, _modifierFactory);

            Assert.AreEqual(20f, p1.GetAttribute(_testAttr).ObservableValue.Value);
            Assert.AreEqual(20f, p2.GetAttribute(_testAttr).ObservableValue.Value);

            handle.Dispose();

            Assert.AreEqual(0f, p1.GetAttribute(_testAttr).ObservableValue.Value);
            Assert.AreEqual(0f, p2.GetAttribute(_testAttr).ObservableValue.Value);
        }

        [Test]
        public void LinkGroup_HandlesMultipleApplications()
        {
            var group = new LinkGroup();
            var processor = new AttributeProcessor();
            group.AddMember(processor);

            var sb1 = CreateStatBlock(10f);
            var sb2 = CreateStatBlock(20f); // Cumulative

            var h1 = group.ApplyStatBlock(sb1, _modifierFactory);
            var h2 = group.ApplyStatBlock(sb2, _modifierFactory);

            Assert.AreEqual(30f, processor.GetAttribute(_testAttr).ObservableValue.Value);

            h1.Dispose();
            Assert.AreEqual(20f, processor.GetAttribute(_testAttr).ObservableValue.Value);

            h2.Dispose();
            Assert.AreEqual(0f, processor.GetAttribute(_testAttr).ObservableValue.Value);
        }
    }
}