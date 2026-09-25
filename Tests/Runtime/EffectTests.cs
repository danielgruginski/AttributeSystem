using NUnit.Framework;
using ReactiveSolutions.AttributeSystem.Core;
using ReactiveSolutions.AttributeSystem.Core.Builders;
using ReactiveSolutions.AttributeSystem.Core.Data;
using ReactiveSolutions.AttributeSystem.Core.Modifiers;
using SemanticKeys;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;

namespace ReactiveSolutions.AttributeSystem.Tests
{
    /// <summary>A Random whose rolls are set by the test.</summary>
    public class FixedRandom : System.Random
    {
        private readonly Queue<double> _rolls;

        /// <param name="rolls">The rolls, in order; the last one repeats.</param>
        public FixedRandom(params double[] rolls) { _rolls = new Queue<double>(rolls); }

        public int Rolls { get; private set; }

        protected override double Sample()
        {
            Rolls++;
            return _rolls.Count > 1 ? _rolls.Dequeue() : _rolls.Peek();
        }
    }

    /// <summary>
    /// Effects: one-off changes from a source entity to a target (hits, spells, potions), computed from both.
    /// </summary>
    public class EffectTests
    {
        private readonly SemanticKey AttackPower = TestKeys.Mock("AttackPower");
        private readonly SemanticKey Defense = TestKeys.Mock("Defense");
        private readonly SemanticKey Health = TestKeys.Mock("Health");
        private readonly SemanticKey MaxHealth = TestKeys.Mock("MaxHealth");
        private readonly SemanticKey Mana = TestKeys.Mock("Mana");
        private readonly SemanticKey Stamina = TestKeys.Mock("Stamina");
        private readonly SemanticKey Level = TestKeys.Mock("Level");
        private readonly SemanticKey CritChance = TestKeys.Mock("CritChance");
        private readonly SemanticKey Durability = TestKeys.Mock("Durability");
        private readonly SemanticKey MainHand = TestKeys.Mock("MainHand");
        private readonly SemanticKey Silenced = TestKeys.Mock("Silenced");

        private Entity _knight;
        private Entity _goblin;

        [SetUp]
        public void SetUp()
        {
            _knight = Character(health: 100f, mana: 20f);
            _knight.SetOrUpdateBaseValue(AttackPower, 40f);
            _goblin = Character(health: 100f, mana: 0f);
            _goblin.SetOrUpdateBaseValue(Defense, 25f);
        }

        private Entity Character(float health, float mana)
        {
            var entity = new Entity();
            entity.AddPool(Health, ValueSource.Const(health));
            entity.AddPool(Mana, ValueSource.Const(mana));
            return entity;
        }

        private static float Current(Entity entity, SemanticKey pool) => entity.GetPool(pool).Current;

        /// <summary>AttackPower * 100 / (Defense + 100): the source's attack against the target's defense.</summary>
        private Effect Hit() => EffectBuilder.Create("Hit")
            .Reduce(Effect.Target(Health), new RatioLogic
            {
                Dividend = new LinearLogic { Input = Effect.Source(AttackPower), Coefficient = 100f },
                Divisor = new LinearLogic { Input = Effect.Target(Defense), Addend = 100f }
            })
            .Build();

        [Test]
        public void Damage_IsComputedFromTheSourceAndTheTarget()
        {
            var result = Hit().Apply(_knight, _goblin);

            Assert.IsTrue(result.Applied);
            Assert.AreEqual(68f, Current(_goblin, Health), 1e-4, "40 * 100 / 125 = 32 damage.");
            Assert.AreEqual(-32f, result.ChangeOf(_goblin, Health), 1e-4);
            Assert.AreEqual(100f, Current(_knight, Health), "The source isn't hurt.");

            var change = result.Changes.Single();
            Assert.AreSame(_goblin, change.Entity);
            Assert.AreEqual(Health, change.Attribute);
            Assert.AreEqual(100f, change.Before);
            Assert.AreEqual(68f, change.After, 1e-4);
            Assert.IsFalse(change.IsCost);
        }

        [Test]
        public void AnEffect_IsDataThatCanBeAppliedAgainAndAgain()
        {
            var hit = Hit();
            string before = EffectJson.ToJson(hit);

            hit.Apply(_knight, _goblin);
            _knight.SetOrUpdateBaseValue(AttackPower, 80f);
            hit.Apply(_knight, _goblin);

            Assert.AreEqual(100f - 32f - 64f, Current(_goblin, Health), 1e-4, "Each application reads the attributes as they are then.");
            Assert.AreEqual(before, EffectJson.ToJson(hit), "Applying doesn't change the effect.");
        }

        [Test]
        public void PoolsStayInBounds_AndReduceAndAddOnlyGoTheirOwnWay()
        {
            _goblin.GetPool(Health).Set(70f);

            var result = EffectBuilder.Create("Potion").Add(Effect.Target(Health), 50f).Build().Apply(_goblin, _goblin);
            Assert.AreEqual(100f, Current(_goblin, Health), "A heal stops at the maximum.");
            Assert.AreEqual(30f, result.ChangeOf(_goblin, Health), 1e-4, "The change is what the pool really gained.");

            EffectBuilder.Create("Overkill").Reduce(Effect.Target(Health), 500f).Build().Apply(_knight, _goblin);
            Assert.AreEqual(0f, Current(_goblin, Health));

            _goblin.GetPool(Health).Fill();
            EffectBuilder.Create("Blocked").Reduce(Effect.Target(Health), new LinearLogic { Input = Effect.Target(Defense), Coefficient = -1f })
                .Build().Apply(_knight, _goblin);
            Assert.AreEqual(100f, Current(_goblin, Health), "Damage that works out negative doesn't heal.");

            EffectBuilder.Create("Set").Set(Effect.Target(Health), 250f).Build().Apply(_knight, _goblin);
            Assert.AreEqual(100f, Current(_goblin, Health), "Set keeps a pool within its maximum.");
        }

        [Test]
        public void OnAnAttributeThatIsntAPool_EffectsChangeTheBaseValue()
        {
            var levelUp = EffectBuilder.Create("Level Up").Add(Effect.Target(Level), 1f).Build();

            levelUp.Apply(null, _knight);
            Assert.AreEqual(1f, _knight.GetAttribute(Level).BaseValue, "A missing attribute is created.");
            levelUp.Apply(null, _knight);
            Assert.AreEqual(2f, _knight.GetAttribute(Level).BaseValue);

            EffectBuilder.Create("Curse").Reduce(Effect.Target(Level), 5f).Build().Apply(null, _knight);
            Assert.AreEqual(-3f, _knight.GetAttribute(Level).BaseValue, "Only pools stop at 0.");
        }

        [Test]
        public void Costs_ArePaidFirst()
        {
            var fireball = EffectBuilder.Create("Fireball")
                .AddCost(Effect.Source(Mana), 15f)
                .Reduce(Effect.Target(Health), new LinearLogic { Input = Effect.Source(Mana), Coefficient = 1f })
                .Build();

            var result = fireball.Apply(_knight, _goblin);

            Assert.IsTrue(result.Applied);
            Assert.AreEqual(5f, Current(_knight, Mana));
            Assert.AreEqual(95f, Current(_goblin, Health), "The actions come after the costs, and see them.");
            Assert.IsTrue(result.Changes[0].IsCost);
            Assert.AreEqual(-15f, result.ChangeOf(_knight, Mana));

            var again = fireball.Apply(_knight, _goblin);
            Assert.AreEqual(EffectStatus.CannotPay, again.Status);
            Assert.IsFalse(again.Applied);
            Assert.AreEqual(Mana, again.UnpaidCost.Name);
            Assert.AreEqual(5f, Current(_knight, Mana), "Nothing is spent when the effect can't be paid for.");
            Assert.AreEqual(95f, Current(_goblin, Health), "And nothing happens.");
            Assert.AreEqual(0, again.Changes.Count);
        }

        [Test]
        public void Costs_AreAllOrNothing_AndAddUpPerResource()
        {
            _knight.SetOrUpdateBaseValue(Stamina, 5f); // Not a pool: its base value is spent.

            var dash = EffectBuilder.Create("Dash")
                .AddCost(Effect.Source(Mana), 10f)
                .AddCost(Effect.Source(Stamina), 10f)
                .Build();
            var result = dash.Apply(_knight, _goblin);
            Assert.AreEqual(Stamina, result.UnpaidCost.Name);
            Assert.AreEqual(20f, Current(_knight, Mana), "The Mana that could be paid isn't spent either.");

            var twice = EffectBuilder.Create("Twice").AddCost(Effect.Source(Mana), 12f).AddCost(Effect.Source(Mana), 12f).Build();
            Assert.AreEqual(EffectStatus.CannotPay, twice.Apply(_knight, _goblin).Status, "12 + 12 Mana is more than 20.");

            _knight.SetOrUpdateBaseValue(Stamina, 15f);
            Assert.IsTrue(dash.Apply(_knight, _goblin).Applied);
            Assert.AreEqual(5f, _knight.GetAttribute(Stamina).BaseValue);

            Assert.AreEqual(EffectStatus.CannotPay, dash.Apply(null, _goblin).Status, "Without a source, there is no one to pay.");
        }

        [Test]
        public void TheCondition_IsCheckedFirst()
        {
            var spell = EffectBuilder.Create("Spell")
                .SetCondition(StatBlockCondition.LacksTag(Silenced, EffectRoles.Source))
                .AddCost(Effect.Source(Mana), 5f)
                .Reduce(Effect.Target(Health), 10f)
                .Build();

            _knight.AddTag(Silenced);
            var result = spell.Apply(_knight, _goblin);
            Assert.AreEqual(EffectStatus.ConditionNotMet, result.Status);
            Assert.AreEqual(20f, Current(_knight, Mana), "No cost is paid.");
            Assert.AreEqual(100f, Current(_goblin, Health));

            _knight.RemoveTag(Silenced);
            Assert.IsTrue(spell.Apply(_knight, _goblin).Applied);
            Assert.AreEqual(90f, Current(_goblin, Health));
        }

        [Test]
        public void Actions_HappenInOrder_AndSeeTheChangesBeforeThem()
        {
            // An execute: a hit, then a killing blow if the hit left the target below 50.
            var execute = EffectBuilder.Create("Execute")
                .Reduce(Effect.Target(Health), 60f)
                .Set(Effect.Target(Health), 0f, StatBlockCondition.Compare(Effect.Target(Health), StatBlockCondition.Comparison.Less, 50f))
                .Build();

            var result = execute.Apply(_knight, _goblin);

            Assert.AreEqual(0f, Current(_goblin, Health));
            Assert.AreEqual(2, result.Changes.Count);
            Assert.AreEqual(-100f, result.ChangeOf(_goblin, Health));

            _goblin.AddPool(Health, ValueSource.Const(200f)).Fill();
            execute.Apply(_knight, _goblin);
            Assert.AreEqual(140f, Current(_goblin, Health), "Above 50 after the hit: no killing blow.");
        }

        [Test]
        public void Chance_IsRolledWithTheGivenRandom()
        {
            _knight.SetOrUpdateBaseValue(CritChance, 0.25f);
            var crit = EffectBuilder.Create("Crit")
                .Reduce(Effect.Target(Health), 10f)
                .Reduce(Effect.Target(Health), 10f, chance: Effect.Source(CritChance))
                .Build();

            var miss = new FixedRandom(0.3);
            Assert.AreEqual(-10f, crit.Apply(_knight, _goblin, miss).ChangeOf(_goblin, Health), "0.3 isn't below 0.25: no crit.");
            Assert.AreEqual(1, miss.Rolls, "An action that always happens doesn't roll.");

            var hit = new FixedRandom(0.1);
            Assert.AreEqual(-20f, crit.Apply(_knight, _goblin, hit).ChangeOf(_goblin, Health), "0.1 is below 0.25: a crit.");

            _knight.SetOrUpdateBaseValue(CritChance, 0f);
            var never = new FixedRandom(0.0);
            crit.Apply(_knight, _goblin, never);
            Assert.AreEqual(0, never.Rolls, "A chance of 0 never happens, without rolling.");
        }

        [Test]
        public void PathsReachEntitiesLinkedToTheSourceOrTarget()
        {
            var sword = new Entity { Name = "Sword" };
            sword.SetOrUpdateBaseValue(Durability, 50f);
            _knight.Attach(MainHand, sword);

            // Parrying wears down the attacker's weapon.
            EffectBuilder.Create("Parry").Reduce(Effect.Source(Durability, MainHand), 5f).Build().Apply(_knight, _goblin);
            Assert.AreEqual(45f, sword.GetAttribute(Durability).BaseValue);

            var result = EffectBuilder.Create("Disarmed").Reduce(Effect.Target(Durability, MainHand), 5f).Build().Apply(_knight, _goblin);
            Assert.IsTrue(result.Applied);
            Assert.AreEqual(0, result.Changes.Count, "The goblin has no MainHand: the action is skipped.");
        }

        [Test]
        public void WithoutASource_ItsAttributesReadAsZero()
        {
            var trap = EffectBuilder.Create("Trap")
                .Reduce(Effect.Target(Health), new LinearLogic { Input = Effect.Source(AttackPower), Addend = 15f })
                .Add(Effect.Source(Health), 10f)
                .Build();

            var result = trap.Apply(null, _goblin);

            Assert.AreEqual(85f, Current(_goblin, Health));
            Assert.AreEqual(1, result.Changes.Count, "The action on the missing source is skipped.");
            Assert.IsNull(result.Source);
        }

        [Test]
        public void EmptyingAPool_FiresDepleted()
        {
            int falls = 0;
            _goblin.GetPool(Health).Depleted.Subscribe(_ => falls++);

            EffectBuilder.Create("Smite").Reduce(Effect.Target(Health), 1000f).Build().Apply(_knight, _goblin);

            Assert.AreEqual(1, falls);
        }

        [Test]
        public void Builder_OnlyReachesAttributesThroughSourceOrTarget()
        {
            var error = Assert.Throws<ArgumentException>(() => EffectBuilder.Create("Wrong").Reduce(AttributeReference.Of(Health), 5f));
            StringAssert.Contains("use Effect.Source(...) or Effect.Target(...)", error.Message);
            Assert.Throws<ArgumentException>(() => EffectBuilder.Create("Wrong").AddCost(AttributeReference.Of(Mana, MainHand), 5f));

            Assert.AreEqual(new List<SemanticKey> { EffectRoles.Target, MainHand }, Effect.Target(Durability, MainHand).Path);
        }

        [Test]
        public void ActionsThatCantWork_AreSkippedWithAWarning()
        {
            var effect = new Effect
            {
                EffectName = "Broken",
                Actions =
                {
                    new EffectAction { Target = AttributeReference.Of(Health), Type = EffectActionType.Reduce, Logic = new ValueLogic(10f) },
                    new EffectAction { Target = Effect.Target(Health), Logic = null },
                    new EffectAction(),
                    new EffectAction { Target = Effect.Target(Health), Type = EffectActionType.Reduce, Logic = new ValueLogic(1f) },
                }
            };

            LogAssert.Expect(LogType.Warning, new Regex("'Broken': skipped the action on 'Health': its path must start with Source or Target"));
            LogAssert.Expect(LogType.Warning, new Regex("'Broken': skipped an action on 'Health' with no Logic"));
            LogAssert.Expect(LogType.Warning, new Regex("'Broken': skipped an action with no target attribute"));

            var result = effect.Apply(_knight, _goblin);

            Assert.AreEqual(99f, Current(_goblin, Health), "The action that works still happens.");
            Assert.AreEqual(1, result.Changes.Count);
        }

        [Test]
        public void DuplicatedActions_AreUnsharedAfterDeserialization()
        {
            var shared = new LinearLogic { Input = Effect.Source(AttackPower) };
            var effect = new Effect
            {
                Actions =
                {
                    new EffectAction { Target = Effect.Target(Health), Logic = shared, Chance = ValueSource.From(shared) },
                    new EffectAction { Target = Effect.Target(Health), Logic = shared },
                }
            };

            effect.OnAfterDeserialize();

            var logics = new[] { effect.Actions[0].Logic, effect.Actions[0].Chance.Formula, effect.Actions[1].Logic };
            Assert.AreEqual(3, logics.Distinct().Count());
        }
    }
}
