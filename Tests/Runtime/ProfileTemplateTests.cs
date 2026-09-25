using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ReactiveSolutions.AttributeSystem.Core;
using ReactiveSolutions.AttributeSystem.Core.Builders;
using ReactiveSolutions.AttributeSystem.Core.Data;
using ReactiveSolutions.AttributeSystem.Core.Modifiers;
using SemanticKeys;
using UnityEngine;
using UnityEngine.TestTools;

namespace ReactiveSolutions.AttributeSystem.Tests
{
    /// <summary>
    /// Profiles that build on templates (applied first, once per entity), and nested entities that reach their parent.
    /// The examples are a small RPG: a Character template, Caster and Warrior templates built on it, and a Weapon
    /// template that adds a weapon's Damage to its owner.
    /// </summary>
    public class ProfileTemplateTests
    {
        private readonly SemanticKey Strength = TestKeys.Mock("Strength");
        private readonly SemanticKey Vitality = TestKeys.Mock("Vitality");
        private readonly SemanticKey MaxHealth = TestKeys.Mock("MaxHealth");
        private readonly SemanticKey MaxMana = TestKeys.Mock("MaxMana");
        private readonly SemanticKey AttackPower = TestKeys.Mock("AttackPower");
        private readonly SemanticKey Damage = TestKeys.Mock("Damage");
        private readonly SemanticKey Character = TestKeys.Mock("Character");
        private readonly SemanticKey Weapon = TestKeys.Mock("Weapon");
        private readonly SemanticKey Owner = TestKeys.Mock("Owner");
        private readonly SemanticKey Wielder = TestKeys.Mock("Wielder");
        private readonly SemanticKey MainHand = TestKeys.Mock("MainHand");

        private Dictionary<string, string> _files;
        private Dictionary<string, int> _reads;

        [SetUp]
        public void SetUp()
        {
            _files = new Dictionary<string, string>();
            _reads = new Dictionary<string, int>();
            JsonDataLoader.ReadText = path =>
            {
                _reads[path] = _reads.TryGetValue(path, out int count) ? count + 1 : 1;
                return _files.TryGetValue(path, out var json) ? json : null;
            };
        }

        [TearDown]
        public void TearDown()
        {
            JsonDataLoader.ReadText = JsonDataLoader.DefaultReadText;
        }

        private void AddProfileFile(string id, EntityProfile profile) =>
            _files[$"{EntityProfileJsonLoader.ResourcesPath}/{id}"] = EntityProfileJson.ToJson(profile);

        private int Reads(string id) => _reads.TryGetValue($"{EntityProfileJsonLoader.ResourcesPath}/{id}", out int count) ? count : 0;

        private static float Value(Entity e, SemanticKey key, params SemanticKey[] path) =>
            e.GetAttribute(key, new List<SemanticKey>(path))?.ObservableValue.Value ?? float.NaN;

        /// <summary>Strength and Vitality 10, MaxHealth = Vitality x 10, and the Character tag.</summary>
        private EntityProfile CharacterTemplate() => ProfileBuilder.Create("Character")
            .AddBaseAttribute(Strength, 10f)
            .AddBaseAttribute(Vitality, 10f)
            .AddInnateTag(Character)
            .AddInnateStatBlock(rules => rules.AddModifier(MaxHealth, new LinearLogic { Input = ValueSource.FromAttribute(Vitality), Coefficient = 10f }))
            .Build();

        /// <summary>A weapon adds its Damage to its Owner's AttackPower while the Owner is a Character.</summary>
        private EntityProfile WeaponTemplate() => ProfileBuilder.Create("Weapon")
            .SetParentKey(Owner)
            .AddInnateTag(Weapon)
            .AddInnateStatBlock(wielded => wielded
                .SetCondition(StatBlockCondition.HasTag(Character, Owner))
                .AddModifier(AttributeReference.Of(AttackPower, Owner), new ValueLogic(ValueSource.FromAttribute(Damage))))
            .Build();

        [Test]
        public void TemplateValues_AreDefaultsTheProfileOverrides()
        {
            var goblin = ProfileBuilder.Create("Goblin")
                .AddTemplate(CharacterTemplate())
                .AddBaseAttribute(Strength, 6f)
                .Build();

            var e = new Entity();
            e.ApplyProfile(goblin);

            Assert.AreEqual(6f, Value(e, Strength), "The profile's own value wins.");
            Assert.AreEqual(10f, Value(e, Vitality), "The template's value is the default.");
            Assert.AreEqual(100f, Value(e, MaxHealth), "The template's formulas apply to the entity.");
            Assert.IsTrue(e.HasTag(Character));

            e.SetOrUpdateBaseValue(Vitality, 12f);
            Assert.AreEqual(120f, Value(e, MaxHealth));
        }

        [Test]
        public void ATemplateSeveralTemplatesBuildOn_IsAppliedOnce()
        {
            var character = CharacterTemplate();
            var caster = ProfileBuilder.Create("Caster").AddTemplate(character).AddBaseAttribute(MaxMana, 50f).Build();
            var warrior = ProfileBuilder.Create("Warrior").AddTemplate(character).AddBaseAttribute(AttackPower, 5f).Build();
            var spellblade = ProfileBuilder.Create("Spellblade").AddTemplate(caster).AddTemplate(warrior).Build();

            var e = new Entity();
            e.ApplyProfile(spellblade);

            Assert.AreEqual(100f, Value(e, MaxHealth), "One MaxHealth formula, not two.");
            Assert.AreEqual(1, e.Tags[Character], "One Character tag, not two.");
            Assert.AreEqual(50f, Value(e, MaxMana));
            Assert.AreEqual(5f, Value(e, AttackPower));
            Assert.IsTrue(e.Implements(character) && e.Implements(caster) && e.Implements(warrior) && e.Implements(spellblade));
        }

        [Test]
        public void TemplatesOfProfilesAppliedSeparately_AreAppliedOnce()
        {
            var character = CharacterTemplate();

            var e = new Entity();
            e.ApplyProfile(ProfileBuilder.Create("Caster").AddTemplate(character).Build());
            e.ApplyProfile(ProfileBuilder.Create("Warrior").AddTemplate(character).Build());

            Assert.AreEqual(100f, Value(e, MaxHealth));
        }

        [Test]
        public void TemplatesByFileId_AreLoadedOncePerEntity()
        {
            AddProfileFile("Templates/Character", CharacterTemplate());
            AddProfileFile("Templates/Caster", ProfileBuilder.Create("Caster").AddTemplate("Templates/Character").AddBaseAttribute(MaxMana, 50f).Build());
            AddProfileFile("Templates/Warrior", ProfileBuilder.Create("Warrior").AddTemplate("Templates/Character").Build());
            var spellblade = ProfileBuilder.Create("Spellblade")
                .AddTemplate("Templates/Caster")
                .AddTemplate("Templates/Warrior.json")
                .AddBaseAttribute(Vitality, 8f)
                .Build();

            var e = new Entity();
            e.ApplyProfile(spellblade);

            Assert.AreEqual(80f, Value(e, MaxHealth));
            Assert.AreEqual(50f, Value(e, MaxMana));
            Assert.AreEqual(1, Reads("Templates/Character"), "The second reference is skipped before loading the file.");
            Assert.IsTrue(e.Implements("Templates/Character"));
            Assert.IsTrue(e.Implements("Data/EntityProfiles/Templates/Warrior.json"));
            Assert.IsFalse(e.Implements("Templates/Rogue"));
        }

        [Test]
        public void ApplyingAProfileAgain_IsSkippedWithAWarning()
        {
            var goblin = ProfileBuilder.Create("Goblin").AddTemplate(CharacterTemplate()).Build();
            var e = new Entity();
            e.ApplyProfile(goblin);

            LogAssert.Expect(LogType.Warning, "[Entity] Skipped profile 'Goblin': it is already applied to this entity.");
            e.ApplyProfile(goblin);

            Assert.AreEqual(100f, Value(e, MaxHealth));
        }

        [Test]
        public void TemplateBuildingOnItself_IsReported()
        {
            AddProfileFile("Templates/Loop", ProfileBuilder.Create("Loop")
                .AddTemplate("Templates/Loop")
                .AddInnateStatBlock(block => block.AddFlatModifier(MaxHealth, 10f))
                .Build());
            LogAssert.Expect(LogType.Error, new Regex("Skipped template 'Templates/Loop' of profile 'Loop': it is already being applied"));

            var e = new Entity();
            e.ApplyProfile(ProfileBuilder.Create("Looper").AddTemplate("Templates/Loop").Build());

            Assert.AreEqual(10f, Value(e, MaxHealth));
        }

        [Test]
        public void MissingTemplateFile_IsReportedAndSkipped()
        {
            LogAssert.Expect(LogType.Error,
                "[EntityProfileJsonLoader] Could not load EntityProfile with ID: Templates/Missing (Attempted path: Data/EntityProfiles/Templates/Missing)");

            var e = new Entity();
            e.ApplyProfile(ProfileBuilder.Create("Orphan").AddTemplate("Templates/Missing").AddBaseAttribute(Strength, 3f).Build());

            Assert.AreEqual(3f, Value(e, Strength));
        }

        [Test]
        public void NestedWeapon_ReachesItsOwner_AndAddsItsDamage()
        {
            var sword = ProfileBuilder.Create("Iron Sword").AddTemplate(WeaponTemplate()).AddBaseAttribute(Damage, 12f).Build();
            var knight = ProfileBuilder.Create("Knight")
                .AddTemplate(CharacterTemplate())
                .AddBaseAttribute(AttackPower, 3f)
                .AddNestedEntity(MainHand, sword)
                .Build();

            var e = new Entity();
            e.ApplyProfile(knight);

            Assert.AreEqual(3f + 12f, Value(e, AttackPower), "The sword adds its Damage to its Owner.");
            Assert.AreEqual(10f, Value(e, Strength, MainHand, Owner), "The sword reaches the knight as its Owner.");

            e.SetOrUpdateBaseValue(Damage, 99f);
            Assert.AreEqual(15f, Value(e, AttackPower), "The sword reads its own Damage, not the knight's.");
        }

        [Test]
        public void WeaponHeldByANonCharacter_AddsNothing()
        {
            var sword = ProfileBuilder.Create("Iron Sword").AddTemplate(WeaponTemplate()).AddBaseAttribute(Damage, 12f).Build();
            var rack = ProfileBuilder.Create("Weapon Rack").AddBaseAttribute(AttackPower, 0f).AddNestedEntity(MainHand, sword).Build();

            var e = new Entity();
            e.ApplyProfile(rack);

            Assert.AreEqual(0f, Value(e, AttackPower), "The rack isn't a Character, so the sword's block stays inactive.");
            Assert.IsTrue(e.GetAttribute(Damage, new List<SemanticKey> { MainHand }) != null);
        }

        [Test]
        public void ParentKeyOfTheProfile_OverridesItsTemplates()
        {
            var cursedBlade = ProfileBuilder.Create("Cursed Blade").AddTemplate(WeaponTemplate()).SetParentKey(Wielder).Build();
            var knight = ProfileBuilder.Create("Knight").AddBaseAttribute(Strength, 7f).AddNestedEntity(MainHand, cursedBlade).Build();

            var e = new Entity();
            e.ApplyProfile(knight);

            Assert.AreEqual(7f, Value(e, Strength, MainHand, Wielder));
            Assert.IsNull(e.GetAttribute(Strength, new List<SemanticKey> { MainHand, Owner }));
        }

        [Test]
        public void EntityName_ComesFromTheProfile_NotItsTemplates()
        {
            var sword = ProfileBuilder.Create("Iron Sword").AddTemplate(WeaponTemplate()).Build();
            var knight = ProfileBuilder.Create("Knight").AddTemplate(CharacterTemplate()).AddNestedEntity(MainHand, sword).Build();

            var e = new Entity();
            e.ApplyProfile(knight);
            e.ApplyProfile(ProfileBuilder.Create("Knight Extras").Build());

            Assert.AreEqual("Knight", e.Name, "The first profile applied names the entity.");
            Assert.AreEqual("Iron Sword", e.GetProvider(MainHand).Name);
            Assert.AreEqual("Sir Galahad", new Entity { Name = "Sir Galahad" }.Name);
        }

        [Test]
        public void TemplatesAndParentKey_RoundTripThroughJson()
        {
            var profile = ProfileBuilder.Create("Goblin Shaman")
                .AddTemplate("Templates/Caster")
                .AddTemplate(ProfileBuilder.Create("Goblin").AddBaseAttribute(Strength, 6f).Build())
                .SetParentKey(Owner)
                .Build();

            string json = EntityProfileJson.ToJson(profile);
            var copy = EntityProfileJson.FromJson(json);

            StringAssert.Contains("\"templates\": [\n    \"Templates/Caster\",\n    {\n      \"profile\": \"Goblin\"", json);
            StringAssert.Contains("\"parentKey\": \"Owner\"", json);
            Assert.AreEqual("Templates/Caster", copy.Templates[0].ProfileId);
            Assert.AreEqual(6f, copy.Templates[1].Profile.BaseAttributes[0].BaseValue);
            Assert.AreEqual(Owner, copy.ParentKey);
            Assert.AreEqual(json, EntityProfileJson.ToJson(copy));
        }

        [Test]
        public void TemplateInItself_CantBeWritten()
        {
            var loop = ProfileBuilder.Create("Loop").Build();
            loop.Templates.Add(new TemplateEntry { Profile = loop });

            var e = Assert.Throws<System.InvalidOperationException>(() => EntityProfileJson.ToJson(loop));
            StringAssert.Contains("templates[0]", e.Message);
        }
    }
}
