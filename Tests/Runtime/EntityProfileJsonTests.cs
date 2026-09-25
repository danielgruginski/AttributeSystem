using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ReactiveSolutions.AttributeSystem.Core;
using ReactiveSolutions.AttributeSystem.Core.Builders;
using ReactiveSolutions.AttributeSystem.Core.Data;
using SemanticKeys;
using UnityEngine;
using UnityEngine.TestTools;

namespace ReactiveSolutions.AttributeSystem.Tests
{
    /// <summary>
    /// EntityProfiles saved as JSON, and profiles or StatBlocks referenced by ID.
    /// The JSON files are served from memory instead of a Resources folder.
    /// </summary>
    public class EntityProfileJsonTests
    {
        private readonly SemanticKey Health = TestKeys.Mock("Health");
        private readonly SemanticKey Damage = TestKeys.Mock("Damage");
        private readonly SemanticKey Undead = TestKeys.Mock("Undead");
        private readonly SemanticKey Inventory = TestKeys.Mock("Inventory");
        private readonly SemanticKey RightHand = TestKeys.Mock("RightHand");
        private readonly SemanticKey LeftHand = TestKeys.Mock("LeftHand");
        private readonly SemanticKey MainStat = TestKeys.Mock("MainStat");

        private Dictionary<string, string> _files;

        [SetUp]
        public void SetUp()
        {
            _files = new Dictionary<string, string>();
            JsonDataLoader.ReadText = path => _files.TryGetValue(path, out var json) ? json : null;
        }

        [TearDown]
        public void TearDown()
        {
            JsonDataLoader.ReadText = JsonDataLoader.DefaultReadText;
        }

        private void AddProfileFile(string id, EntityProfile profile) =>
            _files[$"{EntityProfileJsonLoader.ResourcesPath}/{id}"] = JsonUtility.ToJson(profile);

        private void AddStatBlockFile(string id, StatBlock block) =>
            _files[$"{StatBlockJsonLoader.ResourcesPath}/{id}"] = JsonUtility.ToJson(block);

        private static StatBlock Bonus(SemanticKey target, float value) => new StatBlock
        {
            Modifiers =
            {
                new AttributeModifierSpec
                {
                    TargetAttribute = target,
                    LogicType = sk.Modifiers.Static,
                    Arguments = new List<ValueSource> { ValueSource.Const(value) }
                }
            }
        };

        private static Attribute Get(Entity e, SemanticKey key, params SemanticKey[] path) =>
            e.GetAttribute(key, new List<SemanticKey>(path));

        [Test]
        public void Profile_RoundTripsThroughJson()
        {
            var profile = ProfileBuilder.Create("Skeleton")
                .AddBaseAttribute(Health, 40f)
                .AddInnateTag(Undead)
                .AddLinkGroup(Inventory)
                .AddPointer(MainStat, Health)
                .AddNestedEntity(RightHand, "Weapons/RustySword")
                .AddInnateStatBlock("Passives/Brittle")
                .AddInnateStatBlock(Bonus(Damage, 3f))
                .Build();

            var copy = new EntityProfile();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(profile), copy);

            Assert.AreEqual("Skeleton", copy.ProfileName);
            Assert.AreEqual(Health, copy.BaseAttributes[0].Attribute);
            Assert.AreEqual(40f, copy.BaseAttributes[0].BaseValue);
            CollectionAssert.AreEqual(new[] { Undead }, copy.InnateTags);
            CollectionAssert.AreEqual(new[] { Inventory }, copy.LinkGroups);
            Assert.AreEqual(MainStat, copy.Pointers[0].Alias);
            Assert.AreEqual(Health, copy.Pointers[0].TargetAttribute);
            Assert.AreEqual(RightHand, copy.NestedEntities[0].ProviderKey);
            Assert.AreEqual("Weapons/RustySword", copy.NestedEntities[0].ProfileId);
            Assert.AreEqual("Passives/Brittle", (string)copy.InnateStatBlockIds[0]);
            Assert.AreEqual(Damage, copy.InnateStatBlocks[0].Modifiers[0].TargetAttribute);
        }

        [Test]
        public void NestedEntity_ByProfileId_IsCreatedFromJson()
        {
            AddProfileFile("Weapons/Sword", ProfileBuilder.Create("Sword").AddBaseAttribute(Damage, 12f).Build());
            var knight = ProfileBuilder.Create("Knight").AddNestedEntity(RightHand, "Weapons/Sword").Build();

            var e = new Entity();
            e.ApplyProfile(knight, new ModifierFactory());

            Assert.AreEqual(12f, Get(e, Damage, RightHand).ObservableValue.Value);
        }

        [Test]
        public void InnateStatBlockIds_AreLoadedAndApplied()
        {
            AddStatBlockFile("Passives/Tough", Bonus(Health, 50f));
            var ogre = ProfileBuilder.Create("Ogre")
                .AddBaseAttribute(Health, 100f)
                .AddInnateStatBlock("Passives/Tough")
                .Build();

            var e = new Entity();
            e.ApplyProfile(ogre, new ModifierFactory());

            Assert.AreEqual(150f, Get(e, Health).ObservableValue.Value);

            e.Dispose();
            Assert.AreEqual(100f, Get(e, Health).ObservableValue.Value, "Disposing the entity removes its innate StatBlocks.");
        }

        [Test]
        public void Loader_AcceptsIdVariants()
        {
            AddProfileFile("Weapons/Sword", ProfileBuilder.Create("Sword").Build());

            Assert.AreEqual("Sword", EntityProfileJsonLoader.Load("Weapons/Sword").ProfileName);
            Assert.AreEqual("Sword", EntityProfileJsonLoader.Load("Weapons/Sword.json").ProfileName);
            Assert.AreEqual("Sword", EntityProfileJsonLoader.Load("Data/EntityProfiles/Weapons/Sword").ProfileName);
        }

        [Test]
        public void MissingNestedProfile_LogsErrorAndSkipsTheEntity()
        {
            var knight = ProfileBuilder.Create("Knight").AddNestedEntity(RightHand, "Weapons/Missing").Build();
            LogAssert.Expect(LogType.Error,
                "[EntityProfileJsonLoader] Could not load EntityProfile with ID: Weapons/Missing (Attempted path: Data/EntityProfiles/Weapons/Missing)");

            var e = new Entity();
            e.ApplyProfile(knight, new ModifierFactory());

            Assert.IsNull(Get(e, Damage, RightHand));
        }

        [Test]
        public void ProfileNestingItself_IsReportedInsteadOfRecursing()
        {
            var golem = ProfileBuilder.Create("Golem").AddBaseAttribute(Health, 10f).Build();
            golem.NestedEntities.Add(new NestedEntityEntry { ProviderKey = RightHand, Profile = golem });
            LogAssert.Expect(LogType.Error, new Regex("Skipped nested entity 'RightHand': profile 'Golem' is already being applied"));

            var e = new Entity();
            e.ApplyProfile(golem, new ModifierFactory());

            Assert.AreEqual(10f, Get(e, Health).ObservableValue.Value);
            Assert.IsNull(Get(e, Health, RightHand));
        }

        [Test]
        public void JsonProfileNestingItself_IsReportedInsteadOfRecursing()
        {
            AddProfileFile("Monsters/Hydra", ProfileBuilder.Create("Hydra")
                .AddBaseAttribute(Health, 10f)
                .AddNestedEntity(RightHand, "Monsters/Hydra")
                .Build());
            LogAssert.Expect(LogType.Error, new Regex("Skipped nested entity 'RightHand': profile 'Monsters/Hydra' is already being applied"));

            var e = new Entity();
            e.ApplyProfile(EntityProfileJsonLoader.Load("Monsters/Hydra.json"), new ModifierFactory());

            Assert.AreEqual(10f, Get(e, Health).ObservableValue.Value);
            Assert.IsNull(Get(e, Health, RightHand));
        }

        [Test]
        public void CopyOfAJsonProfileNestedInItself_IsReported()
        {
            AddProfileFile("Monsters/Imp", ProfileBuilder.Create("Imp").AddBaseAttribute(Health, 5f).Build());
            var imp = EntityProfileJsonLoader.Load("Monsters/Imp");
            imp.NestedEntities.Add(new NestedEntityEntry { ProviderKey = RightHand, Profile = EntityProfileJsonLoader.Load("Monsters/Imp") });
            LogAssert.Expect(LogType.Error, new Regex("Skipped nested entity 'RightHand': profile 'Imp' is already being applied"));

            var e = new Entity();
            e.ApplyProfile(imp, new ModifierFactory());

            Assert.IsNull(Get(e, Health, RightHand));
        }

        [Test]
        public void SameProfileNestedTwice_IsNotACycle()
        {
            AddProfileFile("Weapons/Dagger", ProfileBuilder.Create("Dagger").AddBaseAttribute(Damage, 4f).Build());
            var rogue = ProfileBuilder.Create("Rogue")
                .AddNestedEntity(RightHand, "Weapons/Dagger")
                .AddNestedEntity(LeftHand, "Weapons/Dagger")
                .Build();

            var e = new Entity();
            e.ApplyProfile(rogue, new ModifierFactory());

            Assert.AreEqual(4f, Get(e, Damage, RightHand).ObservableValue.Value);
            Assert.AreEqual(4f, Get(e, Damage, LeftHand).ObservableValue.Value);
        }
    }
}