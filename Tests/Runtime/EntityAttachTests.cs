using System.Collections.Generic;
using NUnit.Framework;
using ReactiveSolutions.AttributeSystem.Core;
using ReactiveSolutions.AttributeSystem.Core.Builders;
using ReactiveSolutions.AttributeSystem.Core.Data;
using ReactiveSolutions.AttributeSystem.Core.Modifiers;
using SemanticKeys;

namespace ReactiveSolutions.AttributeSystem.Tests
{
    /// <summary>Linking entities at runtime, both ways (e.g. equipping a sword), with Attach and Detach.</summary>
    public class EntityAttachTests
    {
        private readonly SemanticKey AttackPower = TestKeys.Mock("AttackPower");
        private readonly SemanticKey Damage = TestKeys.Mock("Damage");
        private readonly SemanticKey Owner = TestKeys.Mock("Owner");
        private readonly SemanticKey MainHand = TestKeys.Mock("MainHand");

        // A weapon adds its Damage to its Owner's AttackPower.
        private Entity Weapon(float damage)
        {
            var weapon = new Entity();
            weapon.ApplyProfile(ProfileBuilder.Create("Weapon")
                .SetParentKey(Owner)
                .AddBaseAttribute(Damage, damage)
                .AddInnateStatBlock(wielded => wielded.AddModifier(AttributeReference.Of(AttackPower, Owner), new ValueLogic(ValueSource.FromAttribute(Damage))))
                .Build());
            return weapon;
        }

        private static float AttackPowerOf(Entity e) => e.GetAttribute(TestKeys.Mock("AttackPower")).ObservableValue.Value;

        private Entity Hero()
        {
            var hero = new Entity();
            hero.SetOrUpdateBaseValue(AttackPower, 10f);
            return hero;
        }

        [Test]
        public void Attach_LinksBothWays_AndDetachUnlinks()
        {
            var hero = Hero();
            var sword = Weapon(12f);

            hero.Attach(MainHand, sword);

            Assert.AreSame(sword, hero.GetProvider(MainHand));
            Assert.AreSame(hero, sword.GetProvider(Owner));
            Assert.AreEqual(22f, AttackPowerOf(hero));

            Assert.AreSame(sword, hero.Detach(MainHand));

            Assert.IsNull(hero.GetProvider(MainHand));
            Assert.IsNull(sword.GetProvider(Owner));
            Assert.AreEqual(10f, AttackPowerOf(hero));
            Assert.IsNull(hero.Detach(MainHand), "Nothing left to detach.");
        }

        [Test]
        public void Attach_ReplacesWhatWasThere()
        {
            var hero = Hero();
            var sword = Weapon(12f);
            var dagger = Weapon(4f);

            hero.Attach(MainHand, sword);
            hero.Attach(MainHand, dagger);

            Assert.AreEqual(14f, AttackPowerOf(hero), "Only the dagger counts.");
            Assert.IsNull(sword.GetProvider(Owner));
        }

        [Test]
        public void ChildWithoutParentKey_IsLinkedOneWay()
        {
            var hero = Hero();
            var lantern = new Entity();

            hero.Attach(MainHand, lantern);

            Assert.AreSame(lantern, hero.GetProvider(MainHand));
            Assert.IsNull(lantern.GetProvider(Owner));
        }

        [Test]
        public void NestedEntity_CanBeSwappedAtRuntime()
        {
            var knight = new Entity();
            knight.ApplyProfile(ProfileBuilder.Create("Knight")
                .AddBaseAttribute(AttackPower, 10f)
                .AddNestedEntity(MainHand, ProfileBuilder.Create("Sword")
                    .SetParentKey(Owner)
                    .AddBaseAttribute(Damage, 12f)
                    .AddInnateStatBlock(wielded => wielded.AddModifier(AttributeReference.Of(AttackPower, Owner), new ValueLogic(ValueSource.FromAttribute(Damage))))
                    .Build())
                .Build());
            Assert.AreEqual(22f, AttackPowerOf(knight));

            var sword = knight.Detach(MainHand);
            knight.Attach(MainHand, Weapon(4f));

            Assert.AreEqual(14f, AttackPowerOf(knight));
            Assert.AreEqual(12f, sword.GetAttribute(Damage).ObservableValue.Value, "The detached sword is still there, unlinked.");
        }
    }
}
