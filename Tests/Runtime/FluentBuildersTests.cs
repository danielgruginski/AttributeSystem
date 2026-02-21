using NUnit.Framework;
using ReactiveSolutions.AttributeSystem.Core;
using ReactiveSolutions.AttributeSystem.Core.Builders;
using ReactiveSolutions.AttributeSystem.Core.Data;
using SemanticKeys;
using sk;
using System.Linq;

namespace ReactiveSolutions.AttributeSystem.Tests
{
    public class FluentBuildersTests
    {
        private SemanticKey _healthKey;
        private SemanticKey _speedKey;
        private SemanticKey _strengthKey;
        private SemanticKey _undeadTag;
        private SemanticKey _inventoryGroup;
        private SemanticKey _rightHandKey;
        private SemanticKey _damageKey;
        private SemanticKey _maxHealthAlias;

        [SetUp]
        public void Setup()
        {
            // Initialize dummy keys for testing
            _healthKey = new SemanticKey("Health", "Health", null);
            _speedKey = new SemanticKey("Speed", "Speed", null);
            _strengthKey = new SemanticKey("Strength", "Strength", null);
            _undeadTag = new SemanticKey("Undead", "Undead", null);
            _inventoryGroup = new SemanticKey("Inventory", "Inventory", null);
            _rightHandKey = new SemanticKey("RightHand", "RightHand", null);
            _damageKey = new SemanticKey("Damage", "Damage", null);
            _maxHealthAlias = new SemanticKey("MaxHealth", "MaxHealth", null);
        }

        // --- ProfileBuilder Tests ---

        [Test]
        public void ProfileBuilder_CreatesProfileWithCorrectName()
        {
            var profile = ProfileBuilder.Create("OrcBlueprint").Build();

            Assert.IsNotNull(profile);
            Assert.AreEqual("OrcBlueprint", profile.name);
        }

        [Test]
        public void ProfileBuilder_AddsBaseAttribute()
        {
            var profile = ProfileBuilder.Create()
                .AddBaseAttribute(_healthKey, 150f)
                .Build();

            Assert.AreEqual(1, profile.BaseAttributes.Count);
            Assert.AreEqual(_healthKey, profile.BaseAttributes[0].Attribute);
            Assert.AreEqual(150f, profile.BaseAttributes[0].BaseValue);
        }

        [Test]
        public void ProfileBuilder_AddsMultipleBaseAttributes()
        {
            var profile = ProfileBuilder.Create()
                .AddBaseAttributes(12f, _strengthKey, _speedKey)
                .Build();

            Assert.AreEqual(2, profile.BaseAttributes.Count);
            Assert.IsTrue(profile.BaseAttributes.Any(a => a.Attribute == _strengthKey && a.BaseValue == 12f));
            Assert.IsTrue(profile.BaseAttributes.Any(a => a.Attribute == _speedKey && a.BaseValue == 12f));
        }

        [Test]
        public void ProfileBuilder_AddsInnateTagAndLinkGroup()
        {
            var profile = ProfileBuilder.Create()
                .AddInnateTag(_undeadTag)
                .AddLinkGroup(_inventoryGroup)
                .Build();

            Assert.AreEqual(1, profile.InnateTags.Count);
            Assert.AreEqual(_undeadTag, profile.InnateTags[0]);

            Assert.AreEqual(1, profile.LinkGroups.Count);
            Assert.AreEqual(_inventoryGroup, profile.LinkGroups[0]);
        }

        [Test]
        public void ProfileBuilder_AddsPointerWithProviderPath()
        {
            var profile = ProfileBuilder.Create()
                .AddPointer(_maxHealthAlias, _healthKey, _rightHandKey)
                .Build();

            Assert.AreEqual(1, profile.Pointers.Count);
            Assert.AreEqual(_maxHealthAlias, profile.Pointers[0].Alias);
            Assert.AreEqual(_healthKey, profile.Pointers[0].TargetAttribute);
            Assert.AreEqual(1, profile.Pointers[0].ProviderPath.Count);
            Assert.AreEqual(_rightHandKey, profile.Pointers[0].ProviderPath[0]);
        }

        [Test]
        public void ProfileBuilder_AddsNestedEntity_ViaDirectProfile()
        {
            var weaponProfile = ProfileBuilder.Create("Weapon").AddBaseAttribute(_damageKey, 20f).Build();

            var charProfile = ProfileBuilder.Create()
                .AddNestedEntity(_rightHandKey, weaponProfile)
                .Build();

            Assert.AreEqual(1, charProfile.NestedEntities.Count);
            Assert.AreEqual(_rightHandKey, charProfile.NestedEntities[0].ProviderKey);
            Assert.AreEqual(weaponProfile, charProfile.NestedEntities[0].Profile);
        }

        [Test]
        public void ProfileBuilder_AddsNestedEntity_ViaInlineAction()
        {
            var charProfile = ProfileBuilder.Create()
                .AddNestedEntity(_rightHandKey, child => child
                    .AddBaseAttribute(_damageKey, 50f))
                .Build();

            Assert.AreEqual(1, charProfile.NestedEntities.Count);
            Assert.AreEqual(_rightHandKey, charProfile.NestedEntities[0].ProviderKey);

            var nestedProfile = charProfile.NestedEntities[0].Profile;
            Assert.IsNotNull(nestedProfile);
            Assert.AreEqual(1, nestedProfile.BaseAttributes.Count);
            Assert.AreEqual(_damageKey, nestedProfile.BaseAttributes[0].Attribute);
            Assert.AreEqual(50f, nestedProfile.BaseAttributes[0].BaseValue);
        }

        // --- StatBlockBuilder Tests ---

        [Test]
        public void StatBlockBuilder_CreatesStatBlockWithCorrectNameAndDefaultCondition()
        {
            var statBlock = StatBlockBuilder.Create("Blessing").Build();

            Assert.IsNotNull(statBlock);
            Assert.AreEqual("Blessing", statBlock.BlockName);
            Assert.AreEqual(StatBlockCondition.Mode.Always, statBlock.ActivationCondition.Type);
        }

        [Test]
        public void StatBlockBuilder_SetsConditionCorrectly()
        {
            var statBlock = StatBlockBuilder.Create()
                .SetCondition(StatBlockCondition.Mode.Tag, _undeadTag, true)
                .Build();

            Assert.AreEqual(StatBlockCondition.Mode.Tag, statBlock.ActivationCondition.Type);
            Assert.AreEqual(_undeadTag, statBlock.ActivationCondition.Tag);
            Assert.IsTrue(statBlock.ActivationCondition.InvertTag);
        }

        [Test]
        public void StatBlockBuilder_AddsFlatModifier()
        {
            var statBlock = StatBlockBuilder.Create()
                .AddFlatModifier(_healthKey, 100f)
                .Build();

            Assert.AreEqual(1, statBlock.Modifiers.Count);

            var spec = statBlock.Modifiers[0];
            Assert.AreEqual(_healthKey, spec.TargetAttribute);
            Assert.AreEqual(Modifiers.Static, spec.LogicType);
            Assert.AreEqual(ModifierType.Additive, spec.Type);
            Assert.AreEqual(1, spec.Arguments.Count);
            Assert.AreEqual(100f, spec.Arguments[0].ConstantValue);
        }

        [Test]
        public void StatBlockBuilder_AddsMultiplierModifier()
        {
            var statBlock = StatBlockBuilder.Create()
                .AddMultiplierModifier(_speedKey, 0.25f)
                .Build();

            Assert.AreEqual(1, statBlock.Modifiers.Count);

            var spec = statBlock.Modifiers[0];
            Assert.AreEqual(_speedKey, spec.TargetAttribute);
            Assert.AreEqual(Modifiers.Static, spec.LogicType);
            Assert.AreEqual(ModifierType.Multiplicative, spec.Type);
            Assert.AreEqual(0.25f, spec.Arguments[0].ConstantValue);
        }

        [Test]
        public void StatBlockBuilder_AddsTag()
        {
            var statBlock = StatBlockBuilder.Create()
                .AddTag(_undeadTag)
                .Build();

            Assert.AreEqual(1, statBlock.Tags.Count);
            Assert.AreEqual(_undeadTag, statBlock.Tags[0]);
        }

        // --- Integration Test ---

        [Test]
        public void ProfileBuilder_AddsInnateStatBlock_ViaInlineAction()
        {
            var profile = ProfileBuilder.Create()
                .AddInnateStatBlock(statBlock => statBlock
                    .AddFlatModifier(_healthKey, 25f)
                    .AddTag(_undeadTag))
                .Build();

            Assert.AreEqual(1, profile.InnateStatBlocks.Count);

            var sb = profile.InnateStatBlocks[0];
            Assert.IsNotNull(sb);
            Assert.AreEqual(1, sb.Modifiers.Count);
            Assert.AreEqual(_healthKey, sb.Modifiers[0].TargetAttribute);
            Assert.AreEqual(1, sb.Tags.Count);
            Assert.AreEqual(_undeadTag, sb.Tags[0]);
        }
    }
}