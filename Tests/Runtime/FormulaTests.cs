using NUnit.Framework;
using ReactiveSolutions.AttributeSystem.Core;
using ReactiveSolutions.AttributeSystem.Core.Builders;
using ReactiveSolutions.AttributeSystem.Core.Data;
using ReactiveSolutions.AttributeSystem.Core.Modifiers;
using SemanticKeys;
using System.Linq;
using UniRx;

namespace ReactiveSolutions.AttributeSystem.Tests
{
    /// <summary>
    /// Formulas: a ValueSource that is a logic with inputs of its own, so formulas nest ("AttackPower * 100 /
    /// (Defense + 100)"). They work wherever a ValueSource does: logic inputs, conditions, pools.
    /// </summary>
    public class FormulaTests
    {
        // Keys with GUIDs that differ from their names, as KeyDomain keys have.
        private static SemanticKey Key(string name) => new SemanticKey("guid-" + name.ToLowerInvariant(), name, "domain");

        private readonly SemanticKey AttackPower = Key("AttackPower");
        private readonly SemanticKey Defense = Key("Defense");
        private readonly SemanticKey Damage = Key("Damage");
        private readonly SemanticKey Health = Key("Health");
        private readonly SemanticKey MaxHealth = Key("MaxHealth");
        private readonly SemanticKey Vitality = Key("Vitality");
        private readonly SemanticKey Wounded = Key("Wounded");

        private static float Value(Entity entity, SemanticKey attribute) => entity.GetAttribute(attribute).ObservableValue.Value;

        /// <summary>AttackPower * 100 / (Defense + 100).</summary>
        private RatioLogic Mitigated() => new RatioLogic
        {
            Dividend = new LinearLogic { Input = ValueSource.FromAttribute(AttackPower), Coefficient = 100f },
            Divisor = new LinearLogic { Input = ValueSource.FromAttribute(Defense), Addend = 100f }
        };

        [Test]
        public void NestedFormula_ComputesFromItsInputs_AndFollowsThem()
        {
            var entity = new Entity();
            entity.SetOrUpdateBaseValue(AttackPower, 40f);
            entity.SetOrUpdateBaseValue(Defense, 25f);
            entity.AddModifier("Mitigated", new LogicModifier(Mitigated()), Damage);

            Assert.AreEqual(32f, Value(entity, Damage), 1e-4);

            entity.SetOrUpdateBaseValue(Defense, 100f);
            Assert.AreEqual(20f, Value(entity, Damage), 1e-4, "A change of an input deep in the formula recomputes it.");
        }

        [Test]
        public void ValueSource_ConvertsFromLogicAndFromAttributeReferences()
        {
            ValueSource formula = new FloorLogic { Input = 2.7f };
            ValueSource attribute = AttributeReference.Of(Defense);

            Assert.AreEqual(ValueSource.SourceMode.Formula, formula.Mode);
            Assert.AreEqual(ValueSource.SourceMode.Attribute, attribute.Mode);

            var entity = new Entity();
            entity.SetOrUpdateBaseValue(Defense, 5f);
            float read = -1f;
            formula.GetObservable(entity).Subscribe(v => read = v);
            Assert.AreEqual(2f, read);
            attribute.GetObservable(entity).Subscribe(v => read = v);
            Assert.AreEqual(5f, read);

            Assert.AreEqual(0f, Read(ValueSource.From(null)), "A formula that isn't set reads as 0.");
        }

        private static float Read(ValueSource source)
        {
            float value = float.NaN;
            source.GetObservable(new Entity()).Subscribe(v => value = v);
            return value;
        }

        [Test]
        public void Formula_InACondition()
        {
            var entity = new Entity();
            entity.SetOrUpdateBaseValue(MaxHealth, 100f);
            entity.SetOrUpdateBaseValue(Health, 80f);

            // Below half health, without a helper attribute for the percentage.
            var wounded = StatBlockBuilder.Create("Wounded")
                .SetCondition(StatBlockCondition.Compare(
                    new RatioLogic { Dividend = ValueSource.FromAttribute(Health), Divisor = ValueSource.FromAttribute(MaxHealth) },
                    StatBlockCondition.Comparison.Less, 0.5f))
                .AddTag(Wounded)
                .Build();
            wounded.ApplyToEntity(entity);

            Assert.IsFalse(entity.HasTag(Wounded));
            entity.SetOrUpdateBaseValue(Health, 40f);
            Assert.IsTrue(entity.HasTag(Wounded));
        }

        [Test]
        public void Formula_AsAPoolMaximum()
        {
            var entity = new Entity();
            entity.SetOrUpdateBaseValue(Vitality, 10f);
            var pool = entity.AddPool(Health, new LinearLogic { Input = ValueSource.FromAttribute(Vitality), Coefficient = 10f, Addend = 50f });

            Assert.AreEqual(150f, pool.Max);
            Assert.AreEqual(150f, pool.Current);
        }

        // --- JSON ------------------------------------------------------------------------------

        [Test]
        public void Json_WritesAFormulaAsItsLogic_AndReadsItBack()
        {
            var block = StatBlockBuilder.Create("Armor Math").AddModifier(Damage, Mitigated()).Build();

            string json = StatBlockJson.ToJson(block);
            StringAssert.Contains(
                "{ \"target\": \"Damage\", \"ratio\": { \"dividend\": { \"linear\": { \"input\": \"AttackPower\", \"coefficient\": 100 } }, " +
                "\"divisor\": { \"linear\": { \"input\": \"Defense\", \"addend\": 100 } } } }", Flatten(json));

            var copy = StatBlockJson.FromJson(json);
            Assert.AreEqual(json, StatBlockJson.ToJson(copy));

            var entity = new Entity();
            entity.SetOrUpdateBaseValue(AttackPower, 40f);
            entity.SetOrUpdateBaseValue(Defense, 25f);
            copy.ApplyToEntity(entity);
            Assert.AreEqual(32f, Value(entity, Damage), 1e-4);
        }

        [Test]
        public void Json_AOneFieldLogicHoldingAFormula_IsWrittenAsAnObject()
        {
            // "value": { ... } would read as the Value logic's fields, so the field is named.
            var block = StatBlockBuilder.Create("Wrapped").AddModifier(Damage, new ValueLogic(new FloorLogic { Input = ValueSource.FromAttribute(Defense) })).Build();

            string json = StatBlockJson.ToJson(block);
            StringAssert.Contains("\"value\": { \"value\": { \"floor\": \"Defense\" } }", Flatten(json));
            Assert.AreEqual(json, StatBlockJson.ToJson(StatBlockJson.FromJson(json)));
        }

        [Test]
        public void Json_APoolWhoseMaximumIsAFormula_UsesTheSettingsObject()
        {
            var profile = ProfileBuilder.Create("Hero")
                .AddPool(Health, new LinearLogic { Input = ValueSource.FromAttribute(Vitality), Coefficient = 10f })
                .Build();

            string json = EntityProfileJson.ToJson(profile);
            StringAssert.Contains("\"Health\": { \"max\": { \"linear\": { \"input\": \"Vitality\", \"coefficient\": 10 } } }", Flatten(json));
            Assert.AreEqual(json, EntityProfileJson.ToJson(EntityProfileJson.FromJson(json)));

            var error = Assert.Throws<JsonFormatException>(() => EntityProfileJson.FromJson(
                "{ \"pools\": { \"Health\": { \"linear\": { \"input\": \"Vitality\" } } }, " +
                "\"keys\": { \"Health\": \"h\", \"Vitality\": \"v\" } }"));
            StringAssert.Contains("unknown property 'linear' (a formula as the maximum goes in \"max\"", error.Message);
        }

        [Test]
        public void Json_AFormulaIsOneLogic()
        {
            const string keys = "\"keys\": { \"Damage\": \"d\", \"Defense\": \"f\" }";

            var empty = Assert.Throws<JsonFormatException>(() => StatBlockJson.FromJson(
                "{ \"modifiers\": [{ \"target\": \"Damage\", \"linear\": { \"input\": {} } }], " + keys + " }"));
            StringAssert.Contains("modifiers[0].linear.input: a formula is one logic, e.g. { \"linear\"", empty.Message);

            var unknown = Assert.Throws<JsonFormatException>(() => StatBlockJson.FromJson(
                "{ \"modifiers\": [{ \"target\": \"Damage\", \"linear\": { \"input\": { \"lineer\": {} } } }], " + keys + " }"));
            StringAssert.Contains("there is no logic named 'lineer'", unknown.Message);

            var compare = StatBlockJson.FromJson(
                "{ \"condition\": { \"compare\": [{ \"floor\": \"Defense\" }, \">\", 1] }, " + keys + " }");
            Assert.AreEqual(ValueSource.SourceMode.Formula, compare.ActivationCondition.ValueA.Mode);
            Assert.IsInstanceOf<FloorLogic>(compare.ActivationCondition.ValueA.Formula);
        }

        // --- Copies ----------------------------------------------------------------------------

        [Test]
        public void Clone_CopiesInputsAndNestedFormulas()
        {
            var original = Mitigated();
            var copy = (RatioLogic)original.Clone();

            ((LinearLogic)copy.Divisor.Formula).Addend = 50f;
            copy.Dividend.Mode = ValueSource.SourceMode.Constant;

            Assert.AreEqual(100f, ((LinearLogic)original.Divisor.Formula).Addend.ConstantValue, "The nested formula is the copy's own.");
            Assert.AreEqual(ValueSource.SourceMode.Formula, original.Dividend.Mode, "So are its inputs.");

            var segmented = new SegmentedLogic { Segments = { new SegmentedLogic.Segment { Threshold = 10f, Value = 2f } } };
            var segmentedCopy = (SegmentedLogic)segmented.Clone();
            segmentedCopy.Segments.Clear();
            Assert.AreEqual(1, segmented.Segments.Count, "Lists are copied too.");
        }

        [Test]
        public void DuplicatedFormulas_AreUnsharedAfterDeserialization()
        {
            // An entry duplicated in the Inspector points to the same formula object as the original.
            var shared = new LinearLogic { Input = ValueSource.FromAttribute(Defense), Addend = 100f };
            var block = new StatBlock
            {
                Modifiers =
                {
                    new AttributeModifierSpec { TargetAttribute = Damage, Logic = new RatioLogic { Divisor = shared } },
                    new AttributeModifierSpec { TargetAttribute = Damage, Logic = new RatioLogic { Divisor = shared } },
                }
            };
            var condition = StatBlockCondition.Compare(shared, StatBlockCondition.Comparison.Greater, 1f);
            block.ActivationCondition = StatBlockCondition.All(condition, condition);

            block.OnAfterDeserialize();

            var formulas = block.Modifiers.Select(m => ((RatioLogic)m.Logic).Divisor.Formula).ToList();
            Assert.AreNotSame(formulas[0], formulas[1], "Each modifier gets its own formula.");
            Assert.IsTrue(formulas.All(f => f is LinearLogic linear && linear.Addend.ConstantValue == 100f), "The copies keep the settings.");
            Assert.AreNotSame(block.ActivationCondition.SubConditions[0], block.ActivationCondition.SubConditions[1],
                "Sub-conditions ([SerializeReference]) are unshared too.");

            var pools = new EntityProfile
            {
                Pools =
                {
                    new PoolEntry { Resource = Health, Max = ValueSource.From(shared) },
                    new PoolEntry { Resource = Vitality, Max = ValueSource.From(shared) },
                }
            };
            pools.OnAfterDeserialize();
            Assert.AreNotSame(pools.Pools[0].Max.Formula, pools.Pools[1].Max.Formula);
        }

        /// <summary>The JSON on one line, so a test can look for an object whatever the line breaks.</summary>
        internal static string Flatten(string json) =>
            string.Join(" ", json.Split('\n').Select(line => line.Trim())).Replace("[ ", "[").Replace(" ]", "]");
    }
}
