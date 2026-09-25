using NUnit.Framework;
using ReactiveSolutions.AttributeSystem.Core;
using ReactiveSolutions.AttributeSystem.Core.Builders;
using ReactiveSolutions.AttributeSystem.Core.Data;
using ReactiveSolutions.AttributeSystem.Core.Modifiers;
using SemanticKeys;
using System;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Tests
{
    public enum TestMode { Off, Low, High }

    [Serializable]
    public class TestBand
    {
        public float From;
        public float To = 1f;
        public string Label;
    }

    /// <summary>A logic with a field of every kind the JSON files support.</summary>
    [Serializable]
    public class TestKitchenSinkLogic : ModifierLogic
    {
        public int Count = 3;
        public long Big;
        public bool Enabled = true;
        public string Label = "default";
        public TestMode Mode = TestMode.Low;
        public double Precise;
        public float Scale = 1f;
        public SemanticKey Tag;
        public AttributeReference Reference;
        public ValueSource Input = ValueSource.Const(0f);
        public List<TestBand> Bands = new List<TestBand>();
        public float[] Weights = new float[0];
        public AnimationCurve Curve = new AnimationCurve();
        [SerializeField] private float _hidden = 2f;
        [SerializeReference] public ModifierLogic Inner;
        [SerializeReference] public List<ModifierLogic> Parts = new List<ModifierLogic>();

        // Not saved, by Unity or in JSON:
        [NonSerialized] public float Cache = 7f;
        public Dictionary<string, float> Lookup = new Dictionary<string, float>();

        public float Hidden { get => _hidden; set => _hidden = value; }

        public override IObservable<float> Observe(Entity context) => Observable.Return((float)Count);
    }

    /// <summary>A logic with a field JSON files can't hold (a Unity type).</summary>
    [Serializable]
    public class TestVectorLogic : ModifierLogic
    {
        public Vector2 Offset;
        public override IObservable<float> Observe(Entity context) => Observable.Return(Offset.x);
    }

    /// <summary>
    /// The JSON format of StatBlocks and entity profiles: what is written, what is read back, and the errors a
    /// hand-edited file gets.
    /// </summary>
    public class JsonFormatTests
    {
        // Keys with GUIDs that differ from their names, as KeyDomain keys have.
        private static SemanticKey Key(string name) => new SemanticKey("guid-" + name.ToLowerInvariant(), name, "domain");

        private readonly SemanticKey Damage = Key("Damage");
        private readonly SemanticKey Strength = Key("Strength");
        private readonly SemanticKey Health = Key("Health");
        private readonly SemanticKey MaxHealth = Key("MaxHealth");
        private readonly SemanticKey Owner = Key("Owner");
        private readonly SemanticKey RightHand = Key("RightHand");
        private readonly SemanticKey Magical = Key("Magical");
        private readonly SemanticKey Equipped = Key("Equipped");
        private readonly SemanticKey Stunned = Key("Stunned");
        private readonly SemanticKey Undead = Key("Undead");
        private readonly SemanticKey Inventory = Key("Inventory");
        private readonly SemanticKey MainStat = Key("MainStat");
        private readonly SemanticKey Durability = Key("Durability");

        private static string Lines(params string[] lines) => string.Join("\n", lines) + "\n";

        private static Core.Attribute Get(Entity e, SemanticKey key, params SemanticKey[] path) =>
            e.GetAttribute(key, new List<SemanticKey>(path));

        private StatBlock IronSword() => StatBlockBuilder.Create("Iron Sword")
            .SetCondition(StatBlockCondition.HasTag(Equipped))
            .AddTag(Magical)
            .AddModifier(Damage, new ValueLogic(5f))
            .AddModifier(Damage, new LinearLogic { Input = ValueSource.FromAttribute(Strength, Owner), Coefficient = 0.5f })
            .AddModifier(AttributeReference.Of(Health, Owner), new ValueLogic(ValueSource.FromAttribute(MaxHealth, Owner)), ModifierType.ClampMax, 1000)
            .Build();

        // ---------------------------------------------------------------- What is written

        [Test]
        public void StatBlock_IsWrittenAsBuilderCallsWithAKeyTable()
        {
            string expected = Lines(
                "{",
                "  \"statBlock\": \"Iron Sword\",",
                "  \"condition\": { \"hasTag\": \"Equipped\" },",
                "  \"tags\": [\"Magical\"],",
                "  \"modifiers\": [",
                "    { \"target\": \"Damage\", \"value\": 5 },",
                "    { \"target\": \"Damage\", \"linear\": { \"input\": \"Owner/Strength\", \"coefficient\": 0.5 } },",
                "    { \"target\": \"Owner/Health\", \"type\": \"ClampMax\", \"priority\": 1000, \"value\": \"Owner/MaxHealth\" }",
                "  ],",
                "  \"keys\": {",
                "    \"Equipped\": \"guid-equipped\",",
                "    \"Magical\": \"guid-magical\",",
                "    \"Damage\": \"guid-damage\",",
                "    \"Owner\": \"guid-owner\",",
                "    \"Strength\": \"guid-strength\",",
                "    \"Health\": \"guid-health\",",
                "    \"MaxHealth\": \"guid-maxhealth\"",
                "  }",
                "}");

            Assert.AreEqual(expected, StatBlockJson.ToJson(IronSword()));
        }

        [Test]
        public void Profile_IsWrittenAsBuilderCallsWithAKeyTable()
        {
            var goblin = ProfileBuilder.Create("Goblin")
                .AddBaseAttribute(Health, 40f)
                .AddInnateTag(Undead)
                .AddLinkGroup(Inventory)
                .AddNestedEntity(RightHand, "Weapons/RustySword")
                .AddPointer(MainStat, Damage, RightHand)
                .AddInnateStatBlock("Passives/Brittle")
                .AddInnateStatBlock(block => block.AddFlatModifier(Health, -5f))
                .Build();

            string expected = Lines(
                "{",
                "  \"profile\": \"Goblin\",",
                "  \"baseAttributes\": { \"Health\": 40 },",
                "  \"innateTags\": [\"Undead\"],",
                "  \"linkGroups\": [\"Inventory\"],",
                "  \"nestedEntities\": { \"RightHand\": \"Weapons/RustySword\" },",
                "  \"pointers\": { \"MainStat\": \"RightHand/Damage\" },",
                "  \"innateStatBlocks\": [",
                "    \"Passives/Brittle\",",
                "    {",
                "      \"statBlock\": \"NewStatBlock\",",
                "      \"modifiers\": [",
                "        { \"target\": \"Health\", \"value\": -5 }",
                "      ]",
                "    }",
                "  ],",
                "  \"keys\": {",
                "    \"Health\": \"guid-health\",",
                "    \"Undead\": \"guid-undead\",",
                "    \"Inventory\": \"guid-inventory\",",
                "    \"RightHand\": \"guid-righthand\",",
                "    \"MainStat\": \"guid-mainstat\",",
                "    \"Damage\": \"guid-damage\"",
                "  }",
                "}");

            Assert.AreEqual(expected, EntityProfileJson.ToJson(goblin));
        }

        [Test]
        public void LogicFields_WithTheirDefaultValue_AreLeftOut()
        {
            var block = StatBlockBuilder.Create("Defaults")
                .AddModifier(Damage, new LinearLogic())
                .AddModifier(Damage, new PolynomialLogic { Power = 2f })
                .Build();

            string json = StatBlockJson.ToJson(block);

            StringAssert.Contains("{ \"target\": \"Damage\", \"linear\": {} }", json);
            StringAssert.Contains("{ \"target\": \"Damage\", \"polynomial\": { \"power\": 2 } }", json);
        }

        [Test]
        public void ALogicWithOneField_IsWrittenAsItsValue()
        {
            var block = StatBlockBuilder.Create("Short")
                .AddModifier(Damage, new FloorLogic { Input = ValueSource.FromAttribute(Strength) })
                .AddModifier(Damage, new ValueLogic(0f))
                .Build();

            string json = StatBlockJson.ToJson(block);

            StringAssert.Contains("{ \"target\": \"Damage\", \"floor\": \"Strength\" }", json);
            StringAssert.Contains("{ \"target\": \"Damage\", \"value\": 0 }", json);
        }

        [Test]
        public void EntriesWithNoKey_AreLeftOut()
        {
            var block = StatBlockBuilder.Create("Half edited")
                .AddTag(SemanticKey.None)
                .AddBaseValue(SemanticKey.None, 3f)
                .AddModifier(SemanticKey.None, new ValueLogic(1f))
                .Build();

            var copy = StatBlockJson.FromJson(StatBlockJson.ToJson(block));

            Assert.IsEmpty(copy.Tags);
            Assert.IsEmpty(copy.BaseValues);
            Assert.AreEqual(1, copy.Modifiers.Count, "A modifier without a target is kept: it may be half edited.");
            Assert.AreEqual(SemanticKey.None, copy.Modifiers[0].TargetAttribute);
        }

        [Test]
        public void KeysWithTheSameName_GetDistinctNames()
        {
            var poisonStat = new SemanticKey("guid-a", "Poison", "stats");
            var poisonTag = new SemanticKey("guid-b", "Poison", "tags");
            var slashed = new SemanticKey("guid-c", "Fire/Ice", "stats");
            var block = StatBlockBuilder.Create("Venom")
                .AddTag(poisonTag)
                .AddFlatModifier(poisonStat, 3f)
                .AddFlatModifier(slashed, 1f)
                .Build();

            string json = StatBlockJson.ToJson(block);
            var copy = StatBlockJson.FromJson(json);

            StringAssert.Contains("\"Poison\": \"guid-b\"", json);
            StringAssert.Contains("\"Poison#2\": \"guid-a\"", json);
            StringAssert.Contains("\"Fire_Ice\": \"guid-c\"", json);
            Assert.AreEqual(poisonTag, copy.Tags[0]);
            Assert.AreEqual(poisonStat, copy.Modifiers[0].TargetAttribute);
            Assert.AreEqual(slashed, copy.Modifiers[1].TargetAttribute);
        }

        // ---------------------------------------------------------------- What is read

        [Test]
        public void HandWrittenFile_IsBuiltWithTheBuilders()
        {
            string json = @"{
              ""statBlock"": ""Iron Sword"",
              ""tags"": [""Magical""],
              ""modifiers"": [
                { ""target"": ""Damage"", ""value"": 5 },
                { ""target"": ""Damage"", ""linear"": { ""input"": ""Owner/Strength"", ""coefficient"": 0.5 } },
                { ""target"": ""Health"", ""type"": ""ClampMax"", ""priority"": 1000, ""value"": ""MaxHealth"" }
              ],
              ""keys"": {
                ""Magical"": ""guid-magical"", ""Damage"": ""guid-damage"", ""Owner"": ""guid-owner"",
                ""Strength"": ""guid-strength"", ""Health"": ""guid-health"", ""MaxHealth"": ""guid-maxhealth""
              }
            }";

            var block = StatBlockJson.FromJson(json);

            Assert.AreEqual("Iron Sword", block.BlockName);
            Assert.AreEqual(Magical, block.Tags[0]);
            Assert.AreEqual(3, block.Modifiers.Count);

            Assert.AreEqual(Damage, block.Modifiers[0].TargetAttribute);
            Assert.AreEqual(5f, ((ValueLogic)block.Modifiers[0].Logic).Value.ConstantValue);

            var linear = (LinearLogic)block.Modifiers[1].Logic;
            Assert.AreEqual(Strength, linear.Input.AttributeRef.Name);
            CollectionAssert.AreEqual(new[] { Owner }, linear.Input.AttributeRef.Path);
            Assert.AreEqual(0.5f, linear.Coefficient.ConstantValue);
            Assert.AreEqual(0f, linear.Addend.ConstantValue, "Fields that aren't in the file keep their default.");

            Assert.AreEqual(ModifierType.ClampMax, block.Modifiers[2].Type);
            Assert.AreEqual(1000, block.Modifiers[2].Priority);
            Assert.AreEqual(MaxHealth, ((ValueLogic)block.Modifiers[2].Logic).Value.AttributeRef.Name);
        }

        [Test]
        public void KeysFromTheTable_MatchTheirKeysByGuid()
        {
            var block = StatBlockJson.FromJson(@"{ ""tags"": [""Shiny""], ""keys"": { ""Shiny"": ""guid-magical"" } }");

            Assert.AreEqual(Magical, block.Tags[0], "Keys match by GUID, whatever the file calls them.");
            Assert.AreEqual("Shiny", block.Tags[0].Value);
        }

        [Test]
        public void LoadedFile_WorksOnEntities()
        {
            string json = @"{
              ""statBlock"": ""Sword"",
              ""condition"": { ""all"": [{ ""hasTag"": ""Owner/Equipped"" }, { ""compare"": [""Owner/Strength"", "">="", 10] }] },
              ""baseValues"": { ""Durability"": 50 },
              ""tags"": [""Magical""],
              ""remoteTags"": [""Owner/Stunned""],
              ""pointers"": { ""MainStat"": ""Owner/Strength"" },
              ""modifiers"": [
                { ""target"": ""Owner/Damage"", ""linear"": { ""input"": ""MainStat"", ""coefficient"": 2 }, ""source"": ""Sword"" }
              ],
              ""keys"": {
                ""Owner"": ""guid-owner"", ""Equipped"": ""guid-equipped"", ""Strength"": ""guid-strength"",
                ""Durability"": ""guid-durability"", ""Magical"": ""guid-magical"", ""Stunned"": ""guid-stunned"",
                ""MainStat"": ""guid-mainstat"", ""Damage"": ""guid-damage""
              }
            }";
            var owner = new Entity();
            owner.SetOrUpdateBaseValue(Strength, 12f);
            owner.SetOrUpdateBaseValue(Damage, 1f);
            var sword = new Entity();
            sword.RegisterExternalProvider(Owner, owner);

            StatBlockJson.FromJson(json).ApplyToEntity(sword);

            Assert.AreEqual(50f, Get(sword, Durability).ObservableValue.Value, "Base values apply whatever the condition.");
            Assert.AreEqual(1f, Get(owner, Damage).ObservableValue.Value, "The owner isn't equipped yet.");
            Assert.IsFalse(sword.HasTag(Magical));

            owner.AddTag(Equipped);

            Assert.AreEqual(1f + 12f * 2f, Get(owner, Damage).ObservableValue.Value);
            Assert.IsTrue(sword.HasTag(Magical));
            Assert.IsTrue(owner.HasTag(Stunned));
            Assert.AreEqual(12f, Get(sword, MainStat).ObservableValue.Value);

            owner.SetOrUpdateBaseValue(Strength, 8f);

            Assert.AreEqual(1f, Get(owner, Damage).ObservableValue.Value, "Strength 8 fails the comparison.");
            Assert.IsFalse(owner.HasTag(Stunned));
        }

        [Test]
        public void EveryStatBlockField_RoundTrips()
        {
            var block = StatBlockBuilder.Create("Everything")
                .SetCondition(StatBlockCondition.Any(
                    StatBlockCondition.LacksTag(Stunned, Owner),
                    StatBlockCondition.Compare(ValueSource.FromAttribute(Health), StatBlockCondition.Comparison.Equal, 3f, tolerance: 0.5f),
                    StatBlockCondition.All()))
                .AddBaseValue(Durability, 75f)
                .AddTag(Magical)
                .AddRemoteTag(Equipped, Owner, RightHand)
                .AddPointer(MainStat, Strength, Owner)
                .AddModifier(AttributeReference.Of(Damage, Owner), new ClampLogic { Input = 3f, Min = ValueSource.FromAttribute(Strength), Max = 9f },
                    ModifierType.Override, priority: -2, sourceId: "Clamp \"quoted\"\n")
                .AddModifier(Damage, new SegmentedLogic
                {
                    Input = ValueSource.FromAttribute(Strength),
                    Default = 0.5f,
                    Segments = { new SegmentedLogic.Segment { Threshold = 10f, Value = 2f }, new SegmentedLogic.Segment { Threshold = 20f, Value = 3f } }
                }, ModifierType.Multiplicative)
                .Build();

            string json = StatBlockJson.ToJson(block);
            var copy = StatBlockJson.FromJson(json);

            Assert.AreEqual(json, StatBlockJson.ToJson(copy), "Writing what was read gives the same file.");

            var condition = copy.ActivationCondition;
            Assert.AreEqual(StatBlockCondition.Mode.Composite, condition.Type);
            Assert.AreEqual(StatBlockCondition.Operator.Or, condition.GroupOp);
            Assert.IsTrue(condition.SubConditions[0].InvertTag);
            CollectionAssert.AreEqual(new[] { Owner }, condition.SubConditions[0].TagTarget);
            Assert.AreEqual(0.5f, condition.SubConditions[1].Tolerance);
            Assert.AreEqual(3f, condition.SubConditions[1].ValueB.ConstantValue);
            Assert.AreEqual(StatBlockCondition.Operator.And, condition.SubConditions[2].GroupOp);

            Assert.AreEqual(75f, copy.BaseValues[0].Value);
            CollectionAssert.AreEqual(new[] { Owner, RightHand }, copy.RemoteTags[0].TargetPath);
            Assert.AreEqual(Strength, copy.Pointers[0].Target.Name);

            var clamp = copy.Modifiers[0];
            CollectionAssert.AreEqual(new[] { Owner }, clamp.TargetPath);
            Assert.AreEqual(ModifierType.Override, clamp.Type);
            Assert.AreEqual(-2, clamp.Priority);
            Assert.AreEqual("Clamp \"quoted\"\n", clamp.SourceId);

            var segmented = (SegmentedLogic)copy.Modifiers[1].Logic;
            Assert.AreEqual(0.5f, segmented.Default);
            Assert.AreEqual(2, segmented.Segments.Count);
            Assert.AreEqual(3f, segmented.Segments[1].Value);
        }

        [Test]
        public void EveryBuiltInLogic_RoundTrips()
        {
            var logics = new ModifierLogic[]
            {
                new ValueLogic(2f), new LinearLogic { Addend = 1f }, new PolynomialLogic { Scale = 2f }, new ClampLogic { Max = 4f },
                new MinLogic { B = 1f }, new MaxLogic { A = 2f }, new FloorLogic { Input = 1.5f }, new StepLogic { Threshold = 3f },
                new RatioLogic { Divisor = 4f }, new ExponentialLogic { Base = 2f }, new DiminishingReturnsLogic { SoftCap = 5f },
                new ScaledTriangularLogic { Scale = 3f }, new SegmentedLogic { Default = 2f }
            };
            var builder = StatBlockBuilder.Create("All logic");
            foreach (var logic in logics) builder.AddModifier(Damage, logic);

            var copy = StatBlockJson.FromJson(StatBlockJson.ToJson(builder.Build()));

            var context = new Entity();
            for (int i = 0; i < logics.Length; i++)
            {
                Assert.AreEqual(logics[i].GetType(), copy.Modifiers[i].Logic.GetType());
                float expected = float.NaN, actual = float.NaN;
                logics[i].Observe(context).Subscribe(x => expected = x);
                copy.Modifiers[i].Logic.Observe(context).Subscribe(x => actual = x);
                Assert.AreEqual(expected, actual, logics[i].GetType().Name);
            }
        }

        [Test]
        public void Profile_RoundTripsWithInlineNestedProfiles()
        {
            var knight = ProfileBuilder.Create("Knight")
                .AddBaseAttribute(Strength, 10f)
                .AddNestedEntity(RightHand, sword => sword
                    .AddBaseAttribute(Damage, 7f)
                    .AddBaseAttribute(Strength, 3f)
                    .AddInnateStatBlock(block => block.AddModifier(Damage, new LinearLogic { Input = ValueSource.FromAttribute(Strength), Coefficient = 2f })))
                .AddNestedEntity(Owner, "Somewhere/Else")
                .Build();

            string json = EntityProfileJson.ToJson(knight);
            var copy = EntityProfileJson.FromJson(json);

            Assert.AreEqual(json, EntityProfileJson.ToJson(copy));
            Assert.AreEqual("Somewhere/Else", copy.NestedEntities[1].ProfileId);
            Assert.AreEqual(7f, copy.NestedEntities[0].Profile.BaseAttributes[0].BaseValue);

            copy.NestedEntities.RemoveAt(1); // There is no "Somewhere/Else" file here.
            var e = new Entity();
            e.ApplyProfile(copy);

            Assert.AreEqual(7f + 3f * 2f, Get(e, Damage, RightHand).ObservableValue.Value);
        }

        [Test]
        public void ProfileNestedInItself_CantBeWritten()
        {
            var golem = ProfileBuilder.Create("Golem").Build();
            golem.NestedEntities.Add(new NestedEntityEntry { ProviderKey = RightHand, Profile = golem });

            var e = Assert.Throws<InvalidOperationException>(() => EntityProfileJson.ToJson(golem));
            StringAssert.Contains("nestedEntities.RightHand", e.Message);
            StringAssert.Contains("nested in itself", e.Message);
        }

        [Test]
        public void FindKey_ResolvesNamesMissingFromTheTable()
        {
            string json = @"{ ""modifiers"": [{ ""target"": ""Mana"", ""value"": 5 }] }";
            var mana = Key("Mana");

            var block = StatBlockJson.FromJson(json, name => name == "Mana" ? mana : SemanticKey.None);

            Assert.AreEqual(mana, block.Modifiers[0].TargetAttribute);
            StringAssert.Contains("\"Mana\": \"guid-mana\"", StatBlockJson.ToJson(block), "Saving adds the key to the table.");
        }

        // ---------------------------------------------------------------- Custom logic

        [Test]
        public void CustomLogicFields_RoundTrip()
        {
            var sink = new TestKitchenSinkLogic
            {
                Count = 12,
                Big = 1L << 40,
                Enabled = false,
                Label = "Tab\tand é",
                Mode = TestMode.High,
                Precise = 0.1,
                Scale = float.NaN,
                Tag = Magical,
                Reference = AttributeReference.Of(Strength, Owner),
                Input = ValueSource.FromAttribute(Health),
                Bands = { new TestBand { From = 1f, To = 2f, Label = "low" }, null },
                Weights = new[] { 0.25f, 4f },
                Curve = new AnimationCurve(new Keyframe(0f, 1f, 0f, 2f), new Keyframe(1f, 3f)) { postWrapMode = WrapMode.Loop },
                Hidden = 5f,
                Inner = new LinearLogic { Coefficient = 3f },
                Parts = { new ValueLogic(1f), null, new TestBonusPerLevelLogic { BonusPerLevel = 4f } },
                Cache = 99f,
                Lookup = { ["a"] = 1f }
            };
            var block = StatBlockBuilder.Create("Sink").AddModifier(Damage, sink).Build();

            string json = StatBlockJson.ToJson(block);
            var copy = (TestKitchenSinkLogic)StatBlockJson.FromJson(json).Modifiers[0].Logic;

            Assert.AreEqual(json, StatBlockJson.ToJson(StatBlockJson.FromJson(json)));
            Assert.AreEqual(12, copy.Count);
            Assert.AreEqual(1L << 40, copy.Big);
            Assert.IsFalse(copy.Enabled);
            Assert.AreEqual("Tab\tand é", copy.Label);
            Assert.AreEqual(TestMode.High, copy.Mode);
            Assert.AreEqual(0.1, copy.Precise);
            Assert.IsNaN(copy.Scale);
            Assert.AreEqual(Magical, copy.Tag);
            CollectionAssert.AreEqual(new[] { Owner }, copy.Reference.Path);
            Assert.AreEqual(Health, copy.Input.AttributeRef.Name);
            Assert.AreEqual("low", copy.Bands[0].Label);
            Assert.IsNull(copy.Bands[1]);
            CollectionAssert.AreEqual(new[] { 0.25f, 4f }, copy.Weights);
            Assert.AreEqual(2, copy.Curve.keys.Length);
            Assert.AreEqual(2f, copy.Curve.keys[0].outTangent);
            Assert.AreEqual(WrapMode.Loop, copy.Curve.postWrapMode);
            Assert.AreEqual(5f, copy.Hidden);
            Assert.AreEqual(3f, ((LinearLogic)copy.Inner).Coefficient.ConstantValue);
            Assert.IsInstanceOf<ValueLogic>(copy.Parts[0]);
            Assert.IsNull(copy.Parts[1]);
            Assert.AreEqual(4f, ((TestBonusPerLevelLogic)copy.Parts[2]).BonusPerLevel);

            StringAssert.Contains("\"hidden\": 5", json);
            StringAssert.DoesNotContain("cache", json);
            StringAssert.DoesNotContain("lookup", json);
            Assert.AreEqual(7f, copy.Cache, "A [NonSerialized] field keeps its default.");
        }

        [Test]
        public void CustomLogic_IsNamedByItsClass()
        {
            var block = StatBlockBuilder.Create("Custom").AddModifier(Damage, new TestBonusPerLevelLogic { BonusPerLevel = 3f }).Build();

            StringAssert.Contains("\"testBonusPerLevel\": { \"bonusPerLevel\": 3 }", StatBlockJson.ToJson(block));

            foreach (var name in new[] { "testBonusPerLevel", "TestBonusPerLevelLogic", typeof(TestBonusPerLevelLogic).FullName })
            {
                var read = StatBlockJson.FromJson($"{{ \"modifiers\": [{{ \"{name}\": {{}} }}] }}");
                Assert.IsInstanceOf<TestBonusPerLevelLogic>(read.Modifiers[0].Logic, name);
            }
        }

        [Test]
        public void LogicWithTheSameNameAsAnother_IsWrittenWithItsFullName()
        {
            var block = StatBlockBuilder.Create("Echoes")
                .AddModifier(Damage, new JsonNamesA.EchoLogic())
                .AddModifier(Damage, new JsonNamesB.EchoLogic())
                .Build();

            string json = StatBlockJson.ToJson(block);
            var copy = StatBlockJson.FromJson(json);

            StringAssert.Contains($"\"{typeof(JsonNamesA.EchoLogic).FullName}\": {{}}", json);
            Assert.IsInstanceOf<JsonNamesA.EchoLogic>(copy.Modifiers[0].Logic);
            Assert.IsInstanceOf<JsonNamesB.EchoLogic>(copy.Modifiers[1].Logic);

            var e = Assert.Throws<JsonFormatException>(() => StatBlockJson.FromJson("{ \"modifiers\": [{ \"echo\": {} }] }"));
            StringAssert.Contains("could be any of these logic classes", e.Message);
        }

        [Test]
        public void FieldJsonCantHold_IsAnErrorOnlyWhenChanged()
        {
            var unchanged = StatBlockBuilder.Create("Fine").AddModifier(Damage, new TestVectorLogic()).Build();
            StringAssert.Contains("\"testVector\": {}", StatBlockJson.ToJson(unchanged));

            var changed = StatBlockBuilder.Create("Not fine").AddModifier(Damage, new TestVectorLogic { Offset = new Vector2(1f, 2f) }).Build();
            var e = Assert.Throws<InvalidOperationException>(() => StatBlockJson.ToJson(changed));
            StringAssert.Contains("modifiers[0].testVector.offset", e.Message);
            StringAssert.Contains("[NonSerialized]", e.Message);
        }

        [Test]
        public void EmptyEntryInAProviderPath_CantBeWritten()
        {
            var block = StatBlockBuilder.Create("Broken").AddModifier(AttributeReference.Of(Damage, SemanticKey.None), new ValueLogic(1f)).Build();

            var e = Assert.Throws<InvalidOperationException>(() => StatBlockJson.ToJson(block));
            StringAssert.Contains("modifiers[0].target", e.Message);
        }

        // ---------------------------------------------------------------- Errors in hand-edited files

        private static JsonFormatException ReadError(string json) =>
            Assert.Throws<JsonFormatException>(() => StatBlockJson.FromJson(json));

        [Test]
        public void SyntaxErrors_SayWhereAndWhat()
        {
            var comma = ReadError("{\n  \"tags\": [],\n  \"statBlock\": \"X\",\n}");
            StringAssert.Contains("comma after the last property", comma.Message);
            Assert.AreEqual(4, comma.Line);
            Assert.AreEqual(1, comma.Column);

            StringAssert.Contains("comments", ReadError("{ // note\n}").Message);
            StringAssert.Contains("double quotes", ReadError("{ tags: [] }").Message);
            StringAssert.Contains("double quotes", ReadError("{ 'tags': [] }").Message);
            StringAssert.Contains("closing", ReadError("{ \"tags\": [\"A] }").Message);
            StringAssert.Contains("after the end", ReadError("{} {}").Message);
        }

        [Test]
        public void ContentErrors_GiveThePathInTheFile()
        {
            const string keys = ", \"keys\": { \"Damage\": \"guid-damage\" } }";

            var field = ReadError("{ \"modifiers\": [{ \"target\": \"Damage\", \"linear\": { \"coefficient\": true } }]" + keys);
            StringAssert.StartsWith("modifiers[0].linear.coefficient: expected a number", field.Message);
            StringAssert.Contains("found true", field.Message);
            Assert.AreEqual(1, field.Line);

            var typo = ReadError("{ \"modifiers\": [{ \"target\": \"Damage\", \"linear\": { \"cofficient\": 1 } }]" + keys);
            StringAssert.Contains("has no field 'cofficient' (its fields are input, coefficient, addend)", typo.Message);

            var logic = ReadError("{ \"modifiers\": [{ \"target\": \"Damage\", \"lineer\": {} }]" + keys);
            StringAssert.Contains("'lineer' is neither a modifier property", logic.Message);
            StringAssert.Contains("linear", logic.Message);

            var two = ReadError("{ \"modifiers\": [{ \"target\": \"Damage\", \"value\": 1, \"floor\": 2 }]" + keys);
            StringAssert.Contains("has both 'value' and 'floor'", two.Message);

            var property = ReadError("{ \"modifier\": []" + keys);
            StringAssert.Contains("unknown property 'modifier'", property.Message);
            StringAssert.Contains("modifiers", property.Message);

            var profile = ReadError("{ \"profile\": \"Goblin\" }");
            StringAssert.Contains("is this an entity profile file?", profile.Message);

            var twice = ReadError("{ \"tags\": [], \"Tags\": [] }");
            StringAssert.Contains("'Tags' appears twice", twice.Message);

            var compare = ReadError("{ \"condition\": { \"compare\": [\"Damage\", \"<\"] }" + keys);
            StringAssert.Contains("expected [value, operator, value]", compare.Message);

            var op = ReadError("{ \"condition\": { \"compare\": [\"Damage\", \"=<\", 3] }" + keys);
            StringAssert.Contains("condition.compare[1]: expected a comparison", op.Message);

            var whole = ReadError("{ \"modifiers\": [{ \"target\": \"Damage\", \"priority\": 1.5, \"value\": 1 }]" + keys);
            StringAssert.Contains("expected a whole number", whole.Message);

            var type = ReadError("{ \"modifiers\": [{ \"target\": \"Damage\", \"type\": \"Multiply\", \"value\": 1 }]" + keys);
            StringAssert.Contains("expected one of Additive, Multiplicative, Override, ClampMin, ClampMax", type.Message);

            var path = ReadError("{ \"tags\": [\"Owner/\"]" + keys);
            StringAssert.Contains("expected a key name, found the path", path.Message);
        }

        [Test]
        public void UnknownKey_SaysHowToAddIt()
        {
            var e = ReadError("{ \"modifiers\": [{ \"target\": \"Mana\", \"value\": 5 }] }");

            StringAssert.StartsWith("modifiers[0].target: unknown key 'Mana'", e.Message);
            StringAssert.Contains("\"keys\"", e.Message);
        }

        [Test]
        public void Numbers_AndTextEscapes_AreRead()
        {
            var block = StatBlockJson.FromJson(
                (char)0xFEFF + "{ \"statBlock\": \"Caf\\u00e9 \\ud83d\\ude00 \\\"q\\\"\", " +
                "\"modifiers\": [{ \"target\": \"Damage\", \"priority\": 1e3, \"value\": -2.5E-1 }], \"keys\": { \"Damage\": \"guid-damage\" } }");

            Assert.AreEqual("Café 😀 \"q\"", block.BlockName);
            Assert.AreEqual(1000, block.Modifiers[0].Priority);
            Assert.AreEqual(-0.25f, ((ValueLogic)block.Modifiers[0].Logic).Value.ConstantValue);
        }

        [Test]
        public void StatBlockConditions_FromTheFactories_Evaluate()
        {
            var e = new Entity();
            e.SetOrUpdateBaseValue(Health, 30f);
            var block = StatBlockBuilder.Create("Enraged")
                .SetCondition(StatBlockCondition.All(
                    StatBlockCondition.LacksTag(Stunned),
                    StatBlockCondition.Compare(ValueSource.FromAttribute(Health), StatBlockCondition.Comparison.Less, 50f)))
                .AddFlatModifier(Damage, 10f)
                .Build();
            e.SetOrUpdateBaseValue(Damage, 1f);

            block.ApplyToEntity(e);
            Assert.AreEqual(11f, Get(e, Damage).ObservableValue.Value);

            e.AddTag(Stunned);
            Assert.AreEqual(1f, Get(e, Damage).ObservableValue.Value);

            e.RemoveTag(Stunned);
            e.SetOrUpdateBaseValue(Health, 60f);
            Assert.AreEqual(1f, Get(e, Damage).ObservableValue.Value);
        }
    }
}

namespace ReactiveSolutions.AttributeSystem.Tests.JsonNamesA
{
    [Serializable]
    public class EchoLogic : ModifierLogic
    {
        public override IObservable<float> Observe(Entity context) => Observable.Return(1f);
    }
}

namespace ReactiveSolutions.AttributeSystem.Tests.JsonNamesB
{
    [Serializable]
    public class EchoLogic : ModifierLogic
    {
        public override IObservable<float> Observe(Entity context) => Observable.Return(2f);
    }
}
