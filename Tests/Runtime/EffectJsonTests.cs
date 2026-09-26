using NUnit.Framework;
using ReactiveSolutions.AttributeSystem.Core;
using ReactiveSolutions.AttributeSystem.Core.Builders;
using ReactiveSolutions.AttributeSystem.Core.Data;
using ReactiveSolutions.AttributeSystem.Core.Modifiers;
using SemanticKeys;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.TestTools;

namespace ReactiveSolutions.AttributeSystem.Tests
{
    /// <summary>
    /// Effect files: what is written, what is read back, and the errors of a hand-written file. Every path in an effect
    /// starts with its Source or its Target, written by name.
    /// </summary>
    public class EffectJsonTests
    {
        // Keys with GUIDs that differ from their names, as KeyDomain keys have.
        private static SemanticKey Key(string name) => new SemanticKey("guid-" + name.ToLowerInvariant(), name, "domain");

        private readonly SemanticKey Health = Key("Health");
        private readonly SemanticKey Mana = Key("Mana");
        private readonly SemanticKey SpellPower = Key("SpellPower");
        private readonly SemanticKey CritChance = Key("CritChance");
        private readonly SemanticKey Silenced = Key("Silenced");
        private readonly SemanticKey CurrentTarget = new SemanticKey("guid-current-target", "Target", "domain");

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

        private Effect Fireball() => EffectBuilder.Create("Fireball")
            .SetCondition(StatBlockCondition.LacksTag(Silenced, EffectRoles.Source))
            .AddCost(Effect.Source(Mana), 15f)
            .Reduce(Effect.Target(Health), new LinearLogic { Input = Effect.Source(SpellPower), Coefficient = 1.5f })
            .Reduce(Effect.Target(Health), new LinearLogic { Input = Effect.Source(SpellPower), Coefficient = 1.5f }, chance: Effect.Source(CritChance))
            .Build();

        private const string FireballJson =
@"{
  ""effect"": ""Fireball"",
  ""condition"": { ""lacksTag"": ""Source/Silenced"" },
  ""costs"": { ""Source/Mana"": 15 },
  ""actions"": [
    {
      ""target"": ""Target/Health"",
      ""type"": ""Reduce"",
      ""linear"": { ""input"": ""Source/SpellPower"", ""coefficient"": 1.5 }
    },
    {
      ""target"": ""Target/Health"",
      ""type"": ""Reduce"",
      ""chance"": ""Source/CritChance"",
      ""linear"": { ""input"": ""Source/SpellPower"", ""coefficient"": 1.5 }
    }
  ],
  ""keys"": {
    ""Silenced"": ""guid-silenced"",
    ""Mana"": ""guid-mana"",
    ""Health"": ""guid-health"",
    ""SpellPower"": ""guid-spellpower"",
    ""CritChance"": ""guid-critchance""
  }
}";

        [Test]
        public void ToJson_WritesRolesByName_AndOnlyYourKeysInTheTable()
        {
            Assert.AreEqual(FireballJson.Replace("\r\n", "\n") + "\n", EffectJson.ToJson(Fireball()).Replace("\r\n", "\n"));
        }

        [Test]
        public void FromJson_ReadsItBack()
        {
            var effect = EffectJson.FromJson(FireballJson);

            Assert.AreEqual("Fireball", effect.EffectName);
            Assert.AreEqual(EffectRoles.Source, effect.Costs[0].Resource.Path[0]);
            Assert.AreEqual(EffectRoles.Target, effect.Actions[0].Target.Path[0]);
            Assert.AreEqual(EffectActionType.Reduce, effect.Actions[1].Type);
            Assert.AreEqual(EffectRoles.Source, effect.Actions[1].Chance.AttributeRef.Path[0]);
            Assert.AreEqual(FireballJson.Replace("\r\n", "\n") + "\n", EffectJson.ToJson(effect).Replace("\r\n", "\n"));

            var mage = new Entity();
            mage.SetOrUpdateBaseValue(SpellPower, 20f);
            mage.AddPool(Mana, ValueSource.Const(50f));
            var goblin = new Entity();
            goblin.AddPool(Health, ValueSource.Const(100f));

            var result = effect.Apply(mage, goblin, new FixedRandom(0.99));
            Assert.AreEqual(35f, mage.GetPool(Mana).Current);
            Assert.AreEqual(70f, goblin.GetPool(Health).Current);
            Assert.AreEqual(-30f, result.ChangeOf(goblin, Health));
        }

        [Test]
        public void HandWritten_RolesNeedNoKeys_AndIgnoreCase()
        {
            var effect = EffectJson.FromJson(
                "{ \"actions\": [{ \"target\": \"target/Health\", \"value\": \"SOURCE/SpellPower\" }] }",
                name => name == "Health" ? Health : name == "SpellPower" ? SpellPower : SemanticKey.None);

            Assert.AreEqual(new List<SemanticKey> { EffectRoles.Target }, effect.Actions[0].Target.Path);
            Assert.AreEqual(EffectActionType.Add, effect.Actions[0].Type, "Add is the default.");
            var value = ((ValueLogic)effect.Actions[0].Logic).Value;
            Assert.AreEqual(new List<SemanticKey> { EffectRoles.Source }, value.AttributeRef.Path);
        }

        [Test]
        public void KeysNamedLikeTheRoles_AreYoursAfterTheFirstStep()
        {
            // A "Target" link of your own: the monster the source is fighting.
            var effect = EffectBuilder.Create("Taunt").Reduce(Effect.Source(Health, CurrentTarget), 1f).Build();

            string json = EffectJson.ToJson(effect);
            StringAssert.Contains("\"target\": \"Source/Target/Health\"", json);
            StringAssert.Contains("\"Target\": \"guid-current-target\"", json);

            var copy = EffectJson.FromJson(json);
            Assert.AreEqual(new List<SemanticKey> { EffectRoles.Source, CurrentTarget }, copy.Actions[0].Target.Path);
            Assert.AreEqual("guid-current-target", copy.Actions[0].Target.Path[1].Guid);

            // In a StatBlock, "Target" is just your key.
            var block = StatBlockJson.FromJson(
                "{ \"modifiers\": [{ \"target\": \"Target/Health\", \"value\": 5 }], " +
                "\"keys\": { \"Target\": \"guid-current-target\", \"Health\": \"guid-health\" } }");
            Assert.AreEqual(CurrentTarget, block.Modifiers[0].TargetPath[0]);
        }

        [Test]
        public void ActionsWithoutATargetOrAnAmount_AreKept()
        {
            // As in a half-done effect saved from the editor: applying it skips them, with a warning.
            var effect = EffectJson.FromJson("{ \"effect\": \"Draft\", \"actions\": [{ \"type\": \"Set\" }] }");

            Assert.AreEqual(SemanticKey.None, effect.Actions[0].Target.Name);
            Assert.IsNull(effect.Actions[0].Logic);
            StringAssert.Contains("\"actions\": [{ \"type\": \"Set\" }]", FormulaTests.Flatten(EffectJson.ToJson(effect)));
        }

        [TestCase("{ \"actions\": [{ \"target\": \"Health\", \"value\": 1 }] }",
            "actions[0].target: in an effect, \"Health\" must start with Source or Target, the entity it is on: e.g. \"Target/Health\" (line 1, column 27)")]
        [TestCase("{ \"actions\": [{ \"target\": \"Target\", \"value\": 1 }] }",
            "actions[0].target: in an effect, \"Target\" must start with Source or Target, the entity it is on: e.g. \"Target/Health\"")]
        [TestCase("{ \"costs\": { \"Mana\": 5 } }",
            "costs.Mana: in an effect, \"Mana\" must start with Source or Target, the entity it is on: e.g. \"Target/Mana\" (line 1, column 14)")]
        [TestCase("{ \"actions\": [{ \"target\": \"Target/Health\", \"linear\": { \"input\": \"SpellPower\" } }] }",
            "actions[0].linear.input: in an effect, \"SpellPower\" must start with Source or Target")]
        [TestCase("{ \"condition\": { \"hasTag\": \"Silenced\" } }",
            "condition.hasTag: in an effect, \"Silenced\" must start with Source or Target")]
        [TestCase("{ \"actions\": [{ \"target\": \"Target/Health\", \"chanse\": 0.5, \"value\": 1 }] }",
            "actions[0].chanse: 'chanse' is neither an action property (target, type, condition, chance) nor a logic")]
        [TestCase("{ \"actions\": [{ \"target\": \"Target/Health\", \"value\": 1, \"linear\": {} }] }",
            "actions[0].linear: an action has one logic, but this one has both 'value' and 'linear'")]
        [TestCase("{ \"action\": [] }",
            "action: unknown property 'action'. The properties here are: effect, condition, costs, actions, removeStatuses, statuses, keys")]
        [TestCase("{ \"modifiers\": [] }",
            "modifiers: unknown property 'modifiers' (is this a StatBlock file?)")]
        [TestCase("{ \"actions\": [{ \"target\": \"Target/Health\", \"type\": \"Damage\", \"value\": 1 }] }",
            "actions[0].type: expected one of Add, Reduce, Set, found \"Damage\"")]
        public void HandWrittenErrors_SayWhereAndWhat(string json, string expected)
        {
            var error = Assert.Throws<JsonFormatException>(() => EffectJson.FromJson(json, name => Key(name)));
            StringAssert.Contains(expected, error.Message);
        }

        [Test]
        public void AnEffectFile_ReadAsAStatBlock_SaysSo()
        {
            var error = Assert.Throws<JsonFormatException>(() => StatBlockJson.FromJson("{ \"effect\": \"Fireball\" }"));
            StringAssert.Contains("unknown property 'effect' (is this an effect file?)", error.Message);
        }

        [Test]
        public void ToJson_RefusesPathsThatDontStartWithARole()
        {
            var effect = new Effect
            {
                Actions = { new EffectAction { Target = AttributeReference.Of(Health), Logic = new ValueLogic(1f) } }
            };
            var error = Assert.Throws<InvalidOperationException>(() => EffectJson.ToJson(effect));
            StringAssert.Contains("Can't save actions[0].target: 'Health' must be reached through Source or Target in an effect", error.Message);

            var twice = new Effect
            {
                Costs =
                {
                    new EffectCost { Resource = Effect.Source(Mana), Amount = 5f },
                    new EffectCost { Resource = Effect.Source(Mana), Amount = 5f },
                }
            };
            error = Assert.Throws<InvalidOperationException>(() => EffectJson.ToJson(twice));
            StringAssert.Contains("the effect has two costs of Source/Mana: add them up in one", error.Message);
        }

        [Test]
        public void Loader_LoadsByID()
        {
            _files[$"{EffectJsonLoader.ResourcesPath}/Spells/Fireball"] = FireballJson;

            var effect = EffectJsonLoader.Load("Spells/Fireball");
            Assert.AreEqual("Fireball", effect.EffectName);
            Assert.AreEqual(2, effect.Actions.Count);
            Assert.AreEqual(2, EffectJsonLoader.Load("Data/Effects/Spells/Fireball.json").Actions.Count);

            LogAssert.Expect(LogType.Error, new Regex(@"\[EffectJsonLoader\] Could not load Effect with ID: Spells/Frostbolt"));
            var missing = EffectJsonLoader.Load("Spells/Frostbolt");
            Assert.AreEqual("Spells/Frostbolt", missing.EffectName, "A missing effect does nothing, and is named after its ID.");
            Assert.AreEqual(0, missing.Actions.Count);
        }
    }
}
