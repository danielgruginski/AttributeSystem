using NUnit.Framework;
using ReactiveSolutions.AttributeSystem.Core;
using ReactiveSolutions.AttributeSystem.Core.Data;
using SemanticKeys;
using sk; // Using the namespace identified in your previous tests
using System.Collections.Generic;
using UniRx;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Tests
{
    public class EntityProfileTests
    {
        private ModifierFactory _modifierFactory;
        private SemanticKey _healthKey;
        private SemanticKey _undeadTag;
        private SemanticKey _inventoryGroupKey;
        private SemanticKey _rightHandKey;
        private SemanticKey _damageKey;

        [SetUp]
        public void Setup()
        {
            _modifierFactory = new ModifierFactory();
            _healthKey = new SemanticKey("Health", "Health", null);
            _undeadTag = new SemanticKey("Undead", "Undead", null);
            _inventoryGroupKey = new SemanticKey("Inventory", "Inventory", null);
            _rightHandKey = new SemanticKey("RightHand", "RightHand", null);
            _damageKey = new SemanticKey("Damage", "Damage", null);
        }

        private ValueSource Const(float val)
            => new ValueSource { Mode = ValueSource.SourceMode.Constant, ConstantValue = val };

        private StatBlock CreateStatBlock(SemanticKey targetAttr, float value)
        {
            //var statBlock = ScriptableObject.CreateInstance<StatBlock>();
            var statBlock = new StatBlock();
            statBlock.ActivationCondition = new StatBlockCondition { Type = StatBlockCondition.Mode.Always };

            var spec = new AttributeModifierSpec
            {
                LogicType = Modifiers.Static,
                TargetAttribute = targetAttr,
                Type = ModifierType.Additive,
                Arguments = new List<ValueSource> { Const(value) }
            };

            statBlock.Modifiers.Add(spec);
            return statBlock;
        }

        private EntityProfile CreateEmptyProfile()
        {
            return new EntityProfile();
        }

        [Test]
        public void ApplyProfile_SetsBaseAttributes()
        {
            var profile = CreateEmptyProfile();
            profile.BaseAttributes.Add(new BaseAttributeEntry { Attribute = _healthKey, BaseValue = 150f });

            var processor = new Entity();
            processor.ApplyProfile(profile, _modifierFactory);

            var attr = processor.GetAttribute(_healthKey);
            Assert.IsNotNull(attr);
            Assert.AreEqual(150f, attr.ObservableValue.Value);
        }

        [Test]
        public void ApplyProfile_AddsInnateTags()
        {
            var profile = CreateEmptyProfile();
            profile.InnateTags.Add(_undeadTag);

            var processor = new Entity();
            processor.ApplyProfile(profile, _modifierFactory);

            Assert.IsTrue(processor.HasTag(_undeadTag));
        }

        [Test]
        public void ApplyProfile_InitializesLinkGroups()
        {
            var profile = CreateEmptyProfile();
            profile.LinkGroups.Add(_inventoryGroupKey);

            var processor = new Entity();
            processor.ApplyProfile(profile, _modifierFactory);

            // Accessing GetLinkGroup returns the initialized group without creating a new one implicitly
            var group = processor.GetLinkGroup(_inventoryGroupKey);
            Assert.IsNotNull(group);
        }

        [Test]
        public void ApplyProfile_AppliesInnateStatBlocks()
        {
            var profile = CreateEmptyProfile();

            // Starts at 100
            profile.BaseAttributes.Add(new BaseAttributeEntry { Attribute = _healthKey, BaseValue = 100f });

            // Grants +50 Max Health
            var statBlock = CreateStatBlock(_healthKey, 50f);
            profile.InnateStatBlocks.Add(statBlock);

            var processor = new Entity();
            processor.ApplyProfile(profile, _modifierFactory);

            // Total should be 150
            Assert.AreEqual(150f, processor.GetAttribute(_healthKey).ObservableValue.Value);
        }

        [Test]
        public void ApplyProfile_SetsAttributePointers()
        {
            var profile = CreateEmptyProfile();
            var maxHealthAlias = new SemanticKey("MaxHealth", "MaxHealth", null);

            // Base Health = 200
            profile.BaseAttributes.Add(new BaseAttributeEntry { Attribute = _healthKey, BaseValue = 200f });

            // Pointer: MaxHealth -> Health
            profile.Pointers.Add(new PointerEntry
            {
                Alias = maxHealthAlias,
                TargetAttribute = _healthKey,
                ProviderPath = new List<SemanticKey>()
            });

            var processor = new Entity();
            processor.ApplyProfile(profile, _modifierFactory);

            var aliasAttr = processor.GetAttribute(maxHealthAlias);
            Assert.IsNotNull(aliasAttr);
            Assert.AreEqual(200f, aliasAttr.ObservableValue.Value);
        }

        [Test]
        public void ApplyProfile_CreatesNestedEntities()
        {
            // 1. Create Child Profile (Weapon)
            var weaponProfile = CreateEmptyProfile();
            weaponProfile.BaseAttributes.Add(new BaseAttributeEntry { Attribute = _damageKey, BaseValue = 25f });

            // 2. Create Parent Profile (Character)
            var charProfile = CreateEmptyProfile();
            charProfile.NestedEntities.Add(new NestedEntityEntry
            {
                ProviderKey = _rightHandKey,
                Profile = weaponProfile
            });

            // 3. Apply to Processor
            var processor = new Entity();
            processor.ApplyProfile(charProfile, _modifierFactory);

            // 4. Test nested attribute access via path
            var path = new List<SemanticKey> { _rightHandKey };
            var nestedAttr = processor.GetAttribute(_damageKey, path);

            Assert.IsNotNull(nestedAttr, "Nested entity was not registered as an external provider.");
            Assert.AreEqual(25f, nestedAttr.ObservableValue.Value);
        }

        [Test]
        public void Dispose_CleansUpInnateStatBlocks()
        {
            var profile = CreateEmptyProfile();
            var buffBlock = CreateStatBlock(_healthKey, 100f);
            profile.InnateStatBlocks.Add(buffBlock);

            var processor = new Entity();
            processor.ApplyProfile(profile, _modifierFactory);

            Assert.AreEqual(100f, processor.GetAttribute(_healthKey).ObservableValue.Value);

            // Act
            processor.Dispose();

            // The modifier from the StatBlock should be removed, falling back to 0
            Assert.AreEqual(0f, processor.GetAttribute(_healthKey).ObservableValue.Value);
        }

        [Test]
        public void Dispose_CascadesToNestedEntities()
        {
            // Prepare a nested child with a buff so we can verify the buff is stripped on parent disposal
            var weaponProfile = CreateEmptyProfile();
            var weaponBuff = CreateStatBlock(_damageKey, 15f);
            weaponProfile.InnateStatBlocks.Add(weaponBuff);

            var charProfile = CreateEmptyProfile();
            charProfile.NestedEntities.Add(new NestedEntityEntry
            {
                ProviderKey = _rightHandKey,
                Profile = weaponProfile
            });

            var processor = new Entity();
            processor.ApplyProfile(charProfile, _modifierFactory);

            var path = new List<SemanticKey> { _rightHandKey };

            // Child should have 15 damage due to its innate buff
            Assert.AreEqual(15f, processor.GetAttribute(_damageKey, path).ObservableValue.Value);

            // Act - Disposing the parent should automatically dispose all nested entities
            processor.Dispose();

            // Child's buff should be stripped
            Assert.AreEqual(0f, processor.GetAttribute(_damageKey, path).ObservableValue.Value);
        }
    }
}