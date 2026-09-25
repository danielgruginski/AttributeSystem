using System.Collections.Generic;
using NUnit.Framework;
using ReactiveSolutions.AttributeSystem.Core;
using ReactiveSolutions.AttributeSystem.Core.Builders;
using ReactiveSolutions.AttributeSystem.Core.Data;
using ReactiveSolutions.AttributeSystem.Core.Modifiers;
using SemanticKeys;
using UniRx;

namespace ReactiveSolutions.AttributeSystem.Tests
{
    /// <summary>
    /// Totals over the members of a link group: inventory weight, party size, and so on.
    /// </summary>
    public class GroupTotalLogicTests
    {
        private readonly SemanticKey Inventory = TestKeys.Mock("Inventory");
        private readonly SemanticKey Party = TestKeys.Mock("Party");
        private readonly SemanticKey Weight = TestKeys.Mock("Weight");
        private readonly SemanticKey Level = TestKeys.Mock("Level");
        private readonly SemanticKey CarriedWeight = TestKeys.Mock("CarriedWeight");
        private readonly SemanticKey CarryCapacity = TestKeys.Mock("CarryCapacity");
        private readonly SemanticKey PartySize = TestKeys.Mock("PartySize");
        private readonly SemanticKey Encumbered = TestKeys.Mock("Encumbered");
        private readonly SemanticKey Owner = TestKeys.Mock("Owner");

        private static Entity Item(SemanticKey attribute, float value)
        {
            var item = new Entity();
            item.SetOrUpdateBaseValue(attribute, value);
            return item;
        }

        private static float Value(Entity e, SemanticKey key) => e.GetAttribute(key)?.ObservableValue.Value ?? float.NaN;

        private void AddTotal(Entity e, SemanticKey target, GroupTotalLogic logic) =>
            StatBlockBuilder.Create("Totals").AddModifier(target, logic).Build().ApplyToEntity(e);

        [Test]
        public void Sum_FollowsMembersJoiningLeavingAndChanging()
        {
            var hero = new Entity();
            AddTotal(hero, CarriedWeight, new GroupTotalLogic { Group = new AttributeReference(Inventory), Attribute = Weight });
            var inventory = hero.GetLinkGroup(Inventory);

            Assert.AreEqual(0f, Value(hero, CarriedWeight), "An empty group totals 0.");

            var sword = Item(Weight, 3f);
            var shield = Item(Weight, 5f);
            inventory.AddMember(sword);
            inventory.AddMember(shield);
            Assert.AreEqual(8f, Value(hero, CarriedWeight));

            shield.SetOrUpdateBaseValue(Weight, 6f);
            Assert.AreEqual(9f, Value(hero, CarriedWeight));

            inventory.RemoveMember(sword);
            Assert.AreEqual(6f, Value(hero, CarriedWeight));

            inventory.AddMember(new Entity());
            Assert.AreEqual(6f, Value(hero, CarriedWeight), "A member without the attribute counts as 0.");
        }

        [Test]
        public void Operations_CombineTheValues()
        {
            var party = new Entity();
            var group = party.GetOrCreateLinkGroup(Party);
            group.AddMember(Item(Level, 2f));
            group.AddMember(Item(Level, 6f));
            group.AddMember(Item(Level, 4f));

            float Total(GroupOperation operation)
            {
                float result = float.NaN;
                new GroupTotalLogic { Group = new AttributeReference(Party), Attribute = Level, Operation = operation }
                    .Observe(party).Subscribe(value => result = value);
                return result;
            }

            Assert.AreEqual(12f, Total(GroupOperation.Sum));
            Assert.AreEqual(4f, Total(GroupOperation.Average));
            Assert.AreEqual(2f, Total(GroupOperation.Min));
            Assert.AreEqual(6f, Total(GroupOperation.Max));
            Assert.AreEqual(3f, Total(GroupOperation.Count));
        }

        [Test]
        public void GroupThroughAPath_IsFollowed()
        {
            var leader = new Entity();
            var party = leader.GetOrCreateLinkGroup(Party);
            var banner = new Entity();
            AddTotal(banner, PartySize, new GroupTotalLogic { Group = AttributeReference.Of(Party, Owner), Operation = GroupOperation.Count });

            Assert.AreEqual(0f, Value(banner, PartySize), "No Owner yet.");

            banner.RegisterExternalProvider(Owner, leader);
            party.AddMember(new Entity());
            party.AddMember(new Entity());
            Assert.AreEqual(2f, Value(banner, PartySize));

            banner.UnregisterExternalProvider(Owner);
            Assert.AreEqual(0f, Value(banner, PartySize));
        }

        [Test]
        public void CarryingTooMuch_TagsTheHeroEncumbered()
        {
            var hero = new Entity();
            hero.SetOrUpdateBaseValue(CarryCapacity, 10f);
            AddTotal(hero, CarriedWeight, new GroupTotalLogic { Group = new AttributeReference(Inventory), Attribute = Weight });
            StatBlockBuilder.Create("Encumbrance")
                .SetCondition(StatBlockCondition.Compare(ValueSource.FromAttribute(CarriedWeight), StatBlockCondition.Comparison.Greater,
                    ValueSource.FromAttribute(CarryCapacity)))
                .AddTag(Encumbered)
                .Build()
                .ApplyToEntity(hero);

            var anvil = Item(Weight, 12f);
            hero.GetLinkGroup(Inventory).AddMember(anvil);
            Assert.IsTrue(hero.HasTag(Encumbered));

            hero.GetLinkGroup(Inventory).RemoveMember(anvil);
            Assert.IsFalse(hero.HasTag(Encumbered));
        }

        [Test]
        public void GroupTotal_RoundTripsThroughJson()
        {
            var block = StatBlockBuilder.Create("Weight")
                .AddModifier(CarriedWeight, new GroupTotalLogic { Group = new AttributeReference(Inventory), Attribute = Weight })
                .AddModifier(PartySize, new GroupTotalLogic { Group = AttributeReference.Of(Party, Owner), Operation = GroupOperation.Count })
                .Build();

            string json = StatBlockJson.ToJson(block);

            StringAssert.Contains("{ \"target\": \"CarriedWeight\", \"groupTotal\": { \"group\": \"Inventory\", \"attribute\": \"Weight\" } }", json);
            StringAssert.Contains("{ \"target\": \"PartySize\", \"groupTotal\": { \"group\": \"Owner/Party\", \"operation\": \"Count\" } }", json);
            Assert.AreEqual(json, StatBlockJson.ToJson(StatBlockJson.FromJson(json)));
        }
    }
}
