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
    /// Pools: resources that are spent and restored (Health, Mana), kept between 0 and their maximum.
    /// </summary>
    public class ResourcePoolTests
    {
        private readonly SemanticKey Health = TestKeys.Mock("Health");
        private readonly SemanticKey MaxHealth = TestKeys.Mock("MaxHealth");
        private readonly SemanticKey Mana = TestKeys.Mock("Mana");
        private readonly SemanticKey MaxMana = TestKeys.Mock("MaxMana");
        private readonly SemanticKey Stamina = TestKeys.Mock("Stamina");
        private readonly SemanticKey Vitality = TestKeys.Mock("Vitality");

        private Entity _entity;

        [SetUp]
        public void SetUp()
        {
            _entity = new Entity();
        }

        private ResourcePool HealthPool(float max, PoolMaxChange onMaxChange)
        {
            _entity.SetOrUpdateBaseValue(MaxHealth, max);
            return _entity.AddPool(Health, ValueSource.FromAttribute(MaxHealth), onMaxChange);
        }

        [Test]
        public void NewPool_IsFull_AndFollowsItsMaxUntilUsed()
        {
            _entity.SetOrUpdateBaseValue(Vitality, 10f);
            _entity.AddModifier("Vitality", new LogicModifier(new LinearLogic { Input = ValueSource.FromAttribute(Vitality), Coefficient = 10f }), MaxHealth);
            var pool = _entity.AddPool(Health, ValueSource.FromAttribute(MaxHealth), PoolMaxChange.KeepAmount);

            Assert.AreEqual(100f, pool.Current);
            Assert.IsTrue(pool.IsFull);

            _entity.SetOrUpdateBaseValue(Vitality, 15f);
            Assert.AreEqual(150f, pool.Current, "Unused, the pool stays full while its maximum settles.");

            pool.Reduce(30f);
            _entity.SetOrUpdateBaseValue(Vitality, 20f);
            Assert.AreEqual(120f, pool.Current, "Once used, the pool follows its OnMaxChange (here: keep the amount).");
        }

        [Test]
        public void ReduceAndRestore_StayWithinBounds()
        {
            var pool = _entity.AddPool(Health, ValueSource.Const(100f));

            Assert.AreEqual(30f, pool.Reduce(30f));
            Assert.AreEqual(70f, pool.Current);
            Assert.AreEqual(30f, pool.Restore(50f), "Restore returns what was actually added.");
            Assert.AreEqual(100f, pool.Current);
            Assert.AreEqual(100f, pool.Reduce(500f));
            Assert.AreEqual(0f, pool.Current);
            Assert.IsTrue(pool.IsEmpty);
            Assert.AreEqual(0f, pool.Reduce(-5f), "A negative amount does nothing.");
            Assert.AreEqual(0f, pool.Restore(float.NaN));
        }

        [Test]
        public void WritingTheAttributeDirectly_StaysWithinBounds()
        {
            var pool = HealthPool(100f, PoolMaxChange.KeepPercent);

            _entity.SetOrUpdateBaseValue(Health, 150f);
            Assert.AreEqual(100f, _entity.GetAttribute(Health).ObservableValue.Value, "No over-healing.");

            pool.Reduce(30f);
            Assert.AreEqual(70f, _entity.GetAttribute(Health).ObservableValue.Value, "Damage after a heal at full health counts in full.");

            _entity.SetOrUpdateBaseValue(Health, -20f);
            Assert.AreEqual(0f, pool.Current);
        }

        [Test]
        public void TrySpend_OnlySpendsWhatThePoolHolds()
        {
            var mana = _entity.AddPool(Mana, ValueSource.Const(50f));
            mana.Set(20f);

            Assert.IsFalse(mana.TrySpend(30f));
            Assert.AreEqual(20f, mana.Current);
            Assert.IsTrue(mana.TrySpend(20f));
            Assert.AreEqual(0f, mana.Current);
        }

        [Test]
        public void KeepPercent_KeepsTheShareWhenMaxChanges()
        {
            var pool = HealthPool(100f, PoolMaxChange.KeepPercent);
            pool.Reduce(30f);

            _entity.SetOrUpdateBaseValue(MaxHealth, 50f);
            Assert.AreEqual(35f, pool.Current);
            _entity.SetOrUpdateBaseValue(MaxHealth, 100f);
            Assert.AreEqual(70f, pool.Current);
            Assert.AreEqual(0.7f, pool.Percent, 1e-6f);
        }

        [Test]
        public void AddDifference_MovesByAsMuchAsMax()
        {
            var pool = HealthPool(100f, PoolMaxChange.AddDifference);
            pool.Reduce(30f);

            _entity.SetOrUpdateBaseValue(MaxHealth, 120f);
            Assert.AreEqual(90f, pool.Current);
            _entity.SetOrUpdateBaseValue(MaxHealth, 50f);
            Assert.AreEqual(20f, pool.Current);
        }

        [Test]
        public void KeepAmount_StaysWithinTheNewMax()
        {
            var pool = HealthPool(100f, PoolMaxChange.KeepAmount);
            pool.Reduce(30f);

            _entity.SetOrUpdateBaseValue(MaxHealth, 50f);
            Assert.AreEqual(50f, pool.Current);
            _entity.SetOrUpdateBaseValue(MaxHealth, 100f);
            Assert.AreEqual(50f, pool.Current);
        }

        [Test]
        public void Depleted_FiresEachTimeTheAmountDropsToZero()
        {
            var pool = HealthPool(100f, PoolMaxChange.KeepPercent);
            int deaths = 0;
            pool.Depleted.Subscribe(_ => deaths++);

            pool.Reduce(60f);
            pool.Reduce(60f);
            pool.Reduce(10f);
            Assert.AreEqual(1, deaths);

            pool.Restore(10f);
            pool.Reduce(10f);
            Assert.AreEqual(2, deaths);
        }

        [Test]
        public void ObservableCurrent_EmitsTheAmount()
        {
            var pool = HealthPool(100f, PoolMaxChange.KeepPercent);
            var seen = new List<float>();
            pool.ObservableCurrent.Subscribe(seen.Add);

            pool.Reduce(25f);
            pool.Restore(5f);

            CollectionAssert.AreEqual(new[] { 100f, 75f, 80f }, seen);
        }

        [Test]
        public void PoolDefinedAgain_KeepsItsAmount()
        {
            var pool = _entity.AddPool(Health, ValueSource.Const(100f));
            pool.Reduce(40f);

            var replacement = _entity.AddPool(Health, ValueSource.Const(80f), PoolMaxChange.AddDifference);

            Assert.AreSame(replacement, _entity.GetPool(Health));
            Assert.AreEqual(60f, replacement.Current);
            Assert.AreEqual(PoolMaxChange.AddDifference, replacement.OnMaxChange);

            _entity.SetOrUpdateBaseValue(Health, 500f);
            Assert.AreEqual(80f, replacement.Current, "Only the new pool's bounds apply.");
        }

        [Test]
        public void ProfilePools_StartFull_AndTemplatesCanDefineThem()
        {
            var character = ProfileBuilder.Create("Character")
                .AddBaseAttribute(Vitality, 10f)
                .AddPool(Health, ValueSource.FromAttribute(MaxHealth))
                .AddInnateStatBlock(rules => rules.AddModifier(MaxHealth, new LinearLogic { Input = ValueSource.FromAttribute(Vitality), Coefficient = 10f }))
                .Build();
            var goblin = ProfileBuilder.Create("Goblin")
                .AddTemplate(character)
                .AddBaseAttribute(Vitality, 7f)
                .AddPool(Mana, ValueSource.Const(30f), PoolMaxChange.KeepAmount)
                .Build();

            _entity.ApplyProfile(goblin);

            Assert.AreEqual(70f, _entity.GetPool(Health).Current, "Full at the goblin's own maximum.");
            Assert.AreEqual(70f, _entity.GetPool(Health).Max);
            Assert.AreEqual(30f, _entity.GetPool(Mana).Current);
        }

        [Test]
        public void Pools_RoundTripThroughJson()
        {
            string json = @"{
              ""profile"": ""Adventurer"",
              ""pools"": {
                ""Health"": ""MaxHealth"",
                ""Mana"": { ""max"": ""MaxMana"", ""onMaxChange"": ""AddDifference"" },
                ""Stamina"": 100
              },
              ""keys"": { ""Health"": ""Health"", ""MaxHealth"": ""MaxHealth"", ""Mana"": ""Mana"", ""MaxMana"": ""MaxMana"", ""Stamina"": ""Stamina"" }
            }";

            var profile = EntityProfileJson.FromJson(json);
            string written = EntityProfileJson.ToJson(profile);

            StringAssert.Contains(@"""pools"": {
    ""Health"": ""MaxHealth"",
    ""Mana"": { ""max"": ""MaxMana"", ""onMaxChange"": ""AddDifference"" },
    ""Stamina"": 100
  }".Replace("\r\n", "\n"), written);
            Assert.AreEqual(written, EntityProfileJson.ToJson(EntityProfileJson.FromJson(written)));

            _entity.SetOrUpdateBaseValue(MaxHealth, 40f);
            _entity.SetOrUpdateBaseValue(MaxMana, 25f);
            _entity.ApplyProfile(profile);

            Assert.AreEqual(40f, _entity.GetPool(Health).Current);
            Assert.AreEqual(PoolMaxChange.AddDifference, _entity.GetPool(Mana).OnMaxChange);
            Assert.AreEqual(100f, _entity.GetPool(Stamina).Max);
        }

        [Test]
        public void PoolWithoutMax_IsAnErrorInJson()
        {
            var e = Assert.Throws<JsonFormatException>(() =>
                EntityProfileJson.FromJson(@"{ ""pools"": { ""Mana"": { ""onMaxChange"": ""KeepAmount"" } }, ""keys"": { ""Mana"": ""Mana"" } }"));

            StringAssert.StartsWith("pools.Mana: a pool needs a \"max\"", e.Message);
        }

        [Test]
        public void DisposedEntity_IgnoresPoolChanges()
        {
            var pool = HealthPool(100f, PoolMaxChange.KeepPercent);
            _entity.Dispose();

            Assert.DoesNotThrow(() => pool.Reduce(10f));
            Assert.AreEqual(100f, pool.Current);
            Assert.IsNull(_entity.GetPool(Health));
        }
    }
}
