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
    /// Status effect files, and the status entries of effect files. Paths start with Source or Target, except in the
    /// status's StatBlock, which applies to the entity that has the status.
    /// </summary>
    public class StatusEffectJsonTests
    {
        // Keys with GUIDs that differ from their names, as KeyDomain keys have.
        private static SemanticKey Key(string name) => new SemanticKey("guid-" + name.ToLowerInvariant(), name, "domain");

        private readonly SemanticKey Health = Key("Health");
        private readonly SemanticKey Level = Key("Level");
        private readonly SemanticKey MoveSpeed = Key("MoveSpeed");
        private readonly SemanticKey Poisoned = Key("Poisoned");
        private readonly SemanticKey Debuff = Key("Debuff");
        private readonly SemanticKey Buff = Key("Buff");

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

        private StatusEffect Poison() => StatusEffectBuilder.Create("Poison")
            .AddCategory(Debuff)
            .SetCondition(StatBlockCondition.Compare(Effect.Target(Health), StatBlockCondition.Comparison.Greater, 0f))
            .SetDuration(new LinearLogic { Input = Effect.Source(Level), Addend = 3f })
            .SetStacking(StatusStacking.Stack, maxStacks: 3)
            .SetStatBlock(StatBlockBuilder.Create("Poisoned").AddTag(Poisoned).AddMultiplierModifier(MoveSpeed, -0.2f).Build())
            .SetTick(1f, "Debuffs/PoisonTick")
            .AddOnApply(EffectBuilder.Create("").Reduce(Effect.Target(Health), 1f).Build())
            .AddOnExpire("Debuffs/PoisonBurst")
            .Build();

        private const string PoisonJson =
@"{
  ""status"": ""Poison"",
  ""categories"": [""Debuff""],
  ""condition"": { ""compare"": [""Target/Health"", "">"", 0] },
  ""duration"": { ""linear"": { ""input"": ""Source/Level"", ""addend"": 3 } },
  ""stacking"": ""Stack"",
  ""maxStacks"": 3,
  ""statBlock"": {
    ""statBlock"": ""Poisoned"",
    ""tags"": [""Poisoned""],
    ""modifiers"": [
      { ""target"": ""MoveSpeed"", ""type"": ""Multiplicative"", ""value"": 0.8 }
    ]
  },
  ""tick"": { ""every"": 1, ""effect"": ""Debuffs/PoisonTick"" },
  ""onApply"": [
    {
      ""actions"": [
        { ""target"": ""Target/Health"", ""type"": ""Reduce"", ""value"": 1 }
      ]
    }
  ],
  ""onExpire"": [""Debuffs/PoisonBurst""],
  ""keys"": {
    ""Debuff"": ""guid-debuff"",
    ""Health"": ""guid-health"",
    ""Level"": ""guid-level"",
    ""Poisoned"": ""guid-poisoned"",
    ""MoveSpeed"": ""guid-movespeed""
  }
}
";

        private static string Lines(string text) => text.Replace("\r\n", "\n");

        [Test]
        public void ToJson_WritesEveryPart()
        {
            Assert.AreEqual(Lines(PoisonJson), Lines(StatusEffectJson.ToJson(Poison())));
        }

        [Test]
        public void FromJson_ReadsItBack()
        {
            var status = StatusEffectJson.FromJson(PoisonJson);

            Assert.AreEqual("Poison", status.StatusName);
            Assert.IsFalse(status.LastsUntilRemoved);
            Assert.AreEqual(StatusStacking.Stack, status.Stacking);
            Assert.AreEqual(3, status.MaxStacks);
            Assert.AreEqual(1f, status.TickInterval);
            Assert.AreEqual("Debuffs/PoisonTick", status.TickEffect.EffectId);
            Assert.AreEqual(EffectRoles.Source, ((LinearLogic)status.Duration.Formula).Input.AttributeRef.Path[0]);
            Assert.AreEqual(0, status.StatBlock.Modifiers[0].TargetPath.Count, "The StatBlock's paths are the entity's own.");
            Assert.AreEqual(Lines(PoisonJson), Lines(StatusEffectJson.ToJson(status)));
        }

        [Test]
        public void WithoutADuration_ItLastsUntilRemoved()
        {
            var status = StatusEffectJson.FromJson("{ \"status\": \"Cursed\" }");
            Assert.IsTrue(status.LastsUntilRemoved);
            Assert.AreEqual("{\n  \"status\": \"Cursed\"\n}\n", Lines(StatusEffectJson.ToJson(status)));

            var timed = new StatusEffect { StatusName = "Timed" }; // Made in the Inspector: 10 by default.
            StringAssert.Contains("\"duration\": 10", StatusEffectJson.ToJson(timed));

            timed.Duration = ValueSource.From((ModifierLogic)null); // A formula not picked yet reads as 0, and stays a duration.
            StringAssert.Contains("\"duration\": 0", StatusEffectJson.ToJson(timed));
        }

        [Test]
        public void InTheStatBlock_TargetIsYourKey()
        {
            // A "Target" link of your own: the StatBlock applies to the entity, so it isn't the role.
            var status = StatusEffectJson.FromJson(
                "{ \"statBlock\": { \"modifiers\": [{ \"target\": \"Target/MoveSpeed\", \"value\": -1 }] }, " +
                "\"keys\": { \"Target\": \"guid-current-target\", \"MoveSpeed\": \"guid-movespeed\" } }");
            Assert.AreEqual("guid-current-target", status.StatBlock.Modifiers[0].TargetPath[0].Guid);
        }

        [Test]
        public void EffectFiles_ApplyAndRemoveStatusesByID()
        {
            var stab = EffectBuilder.Create("Venomous Stab")
                .Reduce(Effect.Target(Health), 5f)
                .RemoveStatuses(Buff)
                .ApplyStatus("Debuffs/Poison")
                .ApplyStatus("Buffs/Frenzy", EffectRole.Source, chance: 0.3f)
                .Build();

            string json = EffectJson.ToJson(stab);
            StringAssert.Contains("\"removeStatuses\": [\"Buff\"]", json);
            StringAssert.Contains("\"statuses\": [\"Debuffs/Poison\", { \"status\": \"Buffs/Frenzy\", \"to\": \"Source\", \"chance\": 0.3 }]", json);

            var copy = EffectJson.FromJson(json);
            Assert.AreEqual(EffectRole.Source, copy.Statuses[1].To);
            Assert.AreEqual(json, EffectJson.ToJson(copy));

            var inCode = EffectBuilder.Create("Rally").ApplyStatus(StatusEffectBuilder.Create("Haste").Build()).Build();
            var error = Assert.Throws<InvalidOperationException>(() => EffectJson.ToJson(inCode));
            StringAssert.Contains("the status effect 'Haste' was built in code", error.Message);
        }

        [TestCase("{ \"tick\": { \"every\": 0, \"effect\": \"Debuffs/PoisonTick\" } }",
            "tick.every: the time between ticks must be more than 0")]
        [TestCase("{ \"tick\": { \"every\": 1 } }",
            "tick: a tick needs \"every\" (the time between ticks) and \"effect\" (what each tick does)")]
        [TestCase("{ \"condition\": { \"compare\": [\"Health\", \">\", 0] } }",
            "condition.compare[0]: in an effect, \"Health\" must start with Source or Target")]
        [TestCase("{ \"maxStacks\": -1 }",
            "maxStacks: the most stacks can't be negative (0 is no limit)")]
        [TestCase("{ \"stacking\": \"Twice\" }",
            "stacking: expected one of Refresh, Extend, Stack, Independent, Ignore")]
        [TestCase("{ \"onApply\": [3] }",
            "onApply[0]: expected an effect ID such as \"Debuffs/PoisonTick\", or an effect object")]
        [TestCase("{ \"modifiers\": [] }",
            "modifiers: unknown property 'modifiers' (is this a StatBlock file?)")]
        [TestCase("{ \"onApply\": [{ \"keys\": {} }] }",
            "onApply[0].keys: the \"keys\" table belongs at the top level of the file")]
        public void HandWrittenErrors_SayWhereAndWhat(string json, string expected)
        {
            var error = Assert.Throws<JsonFormatException>(() => StatusEffectJson.FromJson(json, name => Key(name)));
            StringAssert.Contains(expected, error.Message);
        }

        [TestCase("{ \"statuses\": [{ \"status\": { \"status\": \"Haste\" } }] }",
            "statuses[0].status: an effect refers to a status effect by its file's ID")]
        [TestCase("{ \"statuses\": [{ \"chance\": 0.5 }] }",
            "statuses[0]: a status entry needs a \"status\": the ID of a status effect file")]
        [TestCase("{ \"statuses\": [{ \"status\": \"Debuffs/Poison\", \"to\": \"Owner\" }] }",
            "statuses[0].to: expected one of Target, Source")]
        [TestCase("{ \"duration\": 5 }",
            "duration: unknown property 'duration' (is this a status effect file?)")]
        public void EffectStatusErrors_SayWhereAndWhat(string json, string expected)
        {
            var error = Assert.Throws<JsonFormatException>(() => EffectJson.FromJson(json, name => Key(name)));
            StringAssert.Contains(expected, error.Message);
        }

        [Test]
        public void Loader_LoadsByID_AndEffectsFindStatusesByID()
        {
            _files[$"{StatusEffectJsonLoader.ResourcesPath}/Debuffs/Poison"] =
                StatusEffectJson.ToJson(StatusEffectBuilder.Create("Poison").SetDuration(5f).AddCategory(Debuff).Build());

            var poison = StatusEffectJsonLoader.Load("Debuffs/Poison");
            Assert.AreEqual("Poison", poison.StatusName);

            var knight = new Entity();
            var stab = EffectBuilder.Create("Venomous Stab").ApplyStatus("Debuffs/Poison").Build();
            stab.Apply(null, knight);
            Assert.IsTrue(poison.IsSameAs(knight.StatusEffects[0].Status), "The effect loaded the same file.");

            LogAssert.Expect(LogType.Error, new Regex(@"\[StatusEffectJsonLoader\] Could not load StatusEffect with ID: Debuffs/Plague"));
            Assert.IsNull(StatusEffectJsonLoader.Load("Debuffs/Plague"));
        }
    }
}
