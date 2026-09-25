using NUnit.Framework;
using ReactiveSolutions.AttributeSystem.Core;
using ReactiveSolutions.AttributeSystem.Core.Builders;
using ReactiveSolutions.AttributeSystem.Core.Data;
using ReactiveSolutions.AttributeSystem.Core.Modifiers;
using SemanticKeys;
using System.Collections.Generic;
using System.Linq;
using UniRx;

namespace ReactiveSolutions.AttributeSystem.Tests
{
    /// <summary>
    /// Status effects: conditions that last on an entity (poisoned, hasted), with a StatBlock while they last, ticks,
    /// stacking and cleanses.
    /// </summary>
    public class StatusEffectTests
    {
        private readonly SemanticKey Health = TestKeys.Mock("Health");
        private readonly SemanticKey MoveSpeed = TestKeys.Mock("MoveSpeed");
        private readonly SemanticKey Level = TestKeys.Mock("Level");
        private readonly SemanticKey Counter = TestKeys.Mock("Counter");
        private readonly SemanticKey Expired = TestKeys.Mock("Expired");
        private readonly SemanticKey Poisoned = TestKeys.Mock("Poisoned");
        private readonly SemanticKey Hasted = TestKeys.Mock("Hasted");
        private readonly SemanticKey Debuff = TestKeys.Mock("Debuff");
        private readonly SemanticKey Buff = TestKeys.Mock("Buff");

        private Entity _knight;
        private Entity _goblin;

        [SetUp]
        public void SetUp()
        {
            _knight = new Entity { Name = "Knight" };
            _knight.AddPool(Health, ValueSource.Const(100f));
            _knight.SetOrUpdateBaseValue(MoveSpeed, 5f);
            _goblin = new Entity { Name = "Goblin" };
        }

        [TearDown]
        public void TearDown()
        {
            JsonDataLoader.ReadText = JsonDataLoader.DefaultReadText;
        }

        private float Current(SemanticKey pool) => _knight.GetPool(pool).Current;
        private float Value(SemanticKey attribute) => _knight.GetAttribute(attribute)?.ObservableValue.Value ?? 0f;

        private StatBlock Slowed() => StatBlockBuilder.Create("Slowed").AddTag(Poisoned).AddMultiplierModifier(MoveSpeed, -0.2f).Build();
        private Effect Damage(float amount) => EffectBuilder.Create("Poison Tick").Reduce(Effect.Target(Health), amount).Build();
        private Effect Count(SemanticKey attribute) => EffectBuilder.Create("Count").Add(Effect.Target(attribute), 1f).Build();

        /// <summary>5 time units of -20% MoveSpeed and the Poisoned tag, and 3 damage each unit.</summary>
        private StatusEffect Poison(StatusStacking stacking = StatusStacking.Refresh, int maxStacks = 0) =>
            StatusEffectBuilder.Create("Poison")
                .AddCategory(Debuff)
                .SetDuration(5f)
                .SetStacking(stacking, maxStacks)
                .SetStatBlock(Slowed())
                .SetTick(1f, Damage(3f))
                .Build();

        [Test]
        public void WhileItLasts_ItsStatBlockApplies_AndItTicks()
        {
            var poison = Poison().Apply(_goblin, _knight);
            var ends = new List<StatusEndReason>();
            poison.Ended.Subscribe(ends.Add);

            Assert.AreSame(poison, _knight.StatusEffects.Single());
            Assert.AreSame(_goblin, poison.Source);
            Assert.IsTrue(_knight.HasTag(Poisoned));
            Assert.AreEqual(4f, Value(MoveSpeed), 1e-4);
            Assert.AreEqual(5f, poison.TimeLeft);
            Assert.AreEqual(1, poison.Stacks);

            _knight.TickStatusEffects(1f);
            Assert.AreEqual(97f, Current(Health));
            Assert.AreEqual(4f, poison.TimeLeft);

            _knight.TickStatusEffects(0.5f);
            Assert.AreEqual(97f, Current(Health), "Half a tick later: no tick yet.");
            _knight.TickStatusEffects(0.5f);
            Assert.AreEqual(94f, Current(Health));

            _knight.TickStatusEffects(10f);
            Assert.AreEqual(85f, Current(Health), "The ticks that fit in its last 3 units, and none after.");
            Assert.IsFalse(poison.IsActive);
            CollectionAssert.AreEqual(new[] { StatusEndReason.Expired }, ends);
            Assert.AreEqual(0, _knight.StatusEffects.Count);
            Assert.IsFalse(_knight.HasTag(Poisoned));
            Assert.AreEqual(5f, Value(MoveSpeed), 1e-4, "Its StatBlock ends with it.");
        }

        [Test]
        public void SmallSteps_AddUpToTheSameTicks()
        {
            Poison().Apply(_goblin, _knight);
            for (int frame = 0; frame < 300; frame++) _knight.TickStatusEffects(1f / 60f);
            _knight.TickStatusEffects(1f / 60f);

            Assert.AreEqual(85f, Current(Health), 1e-4, "5 ticks in 5 seconds of frames.");
            Assert.AreEqual(0, _knight.StatusEffects.Count);
        }

        [Test]
        public void WithoutADuration_ItLastsUntilRemoved()
        {
            var regen = StatusEffectBuilder.Create("Counting").SetTick(1f, Count(Counter)).AddOnExpire(Count(Expired)).Build();
            var active = regen.Apply(null, _knight);

            _knight.TickStatusEffects(100f);
            Assert.AreEqual(100f, Value(Counter));
            Assert.IsTrue(active.LastsUntilRemoved);
            Assert.AreEqual(float.PositiveInfinity, active.TimeLeft);

            StatusEndReason? reason = null;
            active.Ended.Subscribe(r => reason = r);
            active.Remove();
            Assert.AreEqual(StatusEndReason.Removed, reason);
            Assert.AreEqual(0f, Value(Expired), "Removing it isn't running out: no on-expire effects.");
            Assert.AreEqual(0, _knight.StatusEffects.Count);
        }

        [Test]
        public void Refresh_RestartsItsDuration()
        {
            var poison = Poison();
            var first = poison.Apply(_goblin, _knight);
            _knight.TickStatusEffects(3f);

            var other = new Entity { Name = "Other Goblin" };
            var again = poison.Apply(other, _knight);

            Assert.AreSame(first, again);
            Assert.AreEqual(5f, again.TimeLeft);
            Assert.AreEqual(1, again.Stacks);
            Assert.AreSame(other, again.Source, "The last to apply it is its source.");
        }

        [Test]
        public void Extend_AddsItsDuration()
        {
            var poison = Poison(StatusStacking.Extend);
            var active = poison.Apply(_goblin, _knight);
            _knight.TickStatusEffects(3f);
            poison.Apply(_goblin, _knight);

            Assert.AreEqual(7f, active.TimeLeft, 1e-4);
            Assert.AreEqual(7f, active.Duration, 1e-4);
        }

        [Test]
        public void Stack_AddsStacks_UpToTheMost()
        {
            var poison = Poison(StatusStacking.Stack, maxStacks: 3);
            ActiveStatusEffect active = null;
            for (int i = 0; i < 4; i++)
            {
                active = poison.Apply(_goblin, _knight);
                _knight.TickStatusEffects(0.5f);
            }

            Assert.AreEqual(1, _knight.StatusEffects.Count);
            Assert.AreEqual(3, active.Stacks);
            Assert.AreEqual(4.5f, active.TimeLeft, 1e-4, "Each application restarts the duration.");
            Assert.AreEqual(5f * 0.8f * 0.8f * 0.8f, Value(MoveSpeed), 1e-4, "The StatBlock once per stack.");

            float before = Current(Health);
            _knight.TickStatusEffects(1f);
            Assert.AreEqual(before - 9f, Current(Health), 1e-4, "The tick once per stack.");

            _knight.TickStatusEffects(10f);
            Assert.AreEqual(5f, Value(MoveSpeed), 1e-4);
            Assert.IsFalse(_knight.HasTag(Poisoned), "All stacks end together.");
        }

        [Test]
        public void Independent_InstancesHaveTheirOwnDurations()
        {
            var poison = Poison(StatusStacking.Independent, maxStacks: 2);
            var first = poison.Apply(_goblin, _knight);
            _knight.TickStatusEffects(2f);
            var second = poison.Apply(_goblin, _knight);

            Assert.AreNotSame(first, second);
            Assert.AreEqual(2, _knight.StatusEffects.Count);
            Assert.AreEqual(3f, first.TimeLeft, 1e-4);
            Assert.AreEqual(5f, second.TimeLeft, 1e-4);

            _knight.TickStatusEffects(1f);
            var third = poison.Apply(_goblin, _knight);
            Assert.AreSame(first, third, "At the most instances, the one that would end first starts over.");
            Assert.AreEqual(5f, first.TimeLeft, 1e-4);
            Assert.AreEqual(2, _knight.StatusEffects.Count);
            Assert.AreSame(first, _knight.GetStatusEffect(poison));
        }

        [Test]
        public void Ignore_CantBeAppliedAgainWhileItLasts()
        {
            var stun = StatusEffectBuilder.Create("Stun").SetDuration(3f).SetStacking(StatusStacking.Ignore).AddOnApply(Count(Counter)).Build();
            var active = stun.Apply(_goblin, _knight);
            _knight.TickStatusEffects(2f);

            Assert.AreSame(active, stun.Apply(_goblin, _knight));
            Assert.AreEqual(1f, active.TimeLeft, 1e-4);
            Assert.AreEqual(1f, Value(Counter), "Nothing happened the second time.");
        }

        [Test]
        public void TheCondition_GatesTheStatus_AndEndsIt()
        {
            var bleed = StatusEffectBuilder.Create("Bleed")
                .SetCondition(StatBlockCondition.Compare(Effect.Target(Health), StatBlockCondition.Comparison.Greater, 0f))
                .SetDuration(10f)
                .SetStatBlock(Slowed())
                .SetTick(1f, Damage(40f))
                .AddOnExpire(Count(Expired))
                .Build();

            var active = bleed.Apply(_goblin, _knight);
            StatusEndReason? reason = null;
            active.Ended.Subscribe(r => reason = r);

            _knight.TickStatusEffects(10f);

            Assert.AreEqual(0f, Current(Health), "The third tick empties Health...");
            Assert.AreEqual(StatusEndReason.ConditionFailed, reason, "...which ends the status, in the middle of the step.");
            Assert.IsFalse(_knight.HasTag(Poisoned));
            Assert.AreEqual(0f, Value(Expired), "It didn't run out: no on-expire effects.");

            Assert.IsNull(bleed.Apply(_goblin, _knight), "Its condition doesn't hold: it isn't applied.");
            Assert.AreEqual(0, _knight.StatusEffects.Count);
        }

        [Test]
        public void AStatusWhoseStatBlockFailsItsCondition_EndsAtOnce()
        {
            // Only while not Poisoned, and it poisons: it can't last.
            var paradox = StatusEffectBuilder.Create("Paradox")
                .SetCondition(StatBlockCondition.LacksTag(Poisoned, EffectRoles.Target))
                .SetStatBlock(Slowed())
                .Build();

            var active = paradox.Apply(_goblin, _knight);

            Assert.IsFalse(active.IsActive);
            StatusEndReason? reason = null;
            active.Ended.Subscribe(r => reason = r);
            Assert.AreEqual(StatusEndReason.ConditionFailed, reason);
            Assert.IsFalse(_knight.HasTag(Poisoned));
            Assert.AreEqual(0, _knight.StatusEffects.Count);
        }

        [TestCase(StatusStacking.Stack)]
        [TestCase(StatusStacking.Independent)]
        public void AStatusThatAppliesItself_DoesSoOnce(StatusStacking stacking)
        {
            var echo = StatusEffectBuilder.Create("Echo").SetDuration(5f).SetStacking(stacking).Build();
            echo.OnApply.Add(new EffectEntry { Effect = EffectBuilder.Create("Echo Again").Add(Effect.Target(Counter), 1f).ApplyStatus(echo).Build() });

            var active = echo.Apply(_goblin, _knight);

            Assert.AreSame(active, _knight.StatusEffects.Single(), "Applying it from its own on-apply effect does nothing.");
            Assert.AreEqual(1, active.Stacks);
            Assert.AreEqual(1f, Value(Counter));

            // Through another status, and after removing itself: still once.
            var flicker = StatusEffectBuilder.Create("Flicker").AddCategory(Buff).SetStacking(stacking).Build();
            var spark = StatusEffectBuilder.Create("Spark").Build();
            spark.OnApply.Add(new EffectEntry { Effect = EffectBuilder.Create("Flicker Again").ApplyStatus(flicker).Build() });
            flicker.OnApply.Add(new EffectEntry { Effect = EffectBuilder.Create("Flicker Out").Add(Effect.Target(Counter), 1f).RemoveStatuses(Buff).ApplyStatus(spark).Build() });

            flicker.Apply(_goblin, _knight);

            Assert.AreEqual(2f, Value(Counter));
            CollectionAssert.AreEquivalent(new[] { "Echo", "Spark" }, _knight.StatusEffects.Select(status => status.Name));
        }

        [Test]
        public void ItsDuration_CanBeAFormula_ReadWhenItIsApplied()
        {
            _goblin.SetOrUpdateBaseValue(Level, 3f);
            var curse = StatusEffectBuilder.Create("Curse").SetDuration(new LinearLogic { Input = Effect.Source(Level), Coefficient = 2f }).Build();

            Assert.AreEqual(6f, curse.Apply(_goblin, _knight).TimeLeft);
        }

        [Test]
        public void OnApply_EachApplication_OnExpire_WhenItRunsOut()
        {
            var shield = StatusEffectBuilder.Create("Shield").SetDuration(2f)
                .AddOnApply(Count(Counter))
                .AddOnExpire(Count(Expired))
                .Build();

            shield.Apply(_knight, _knight);
            shield.Apply(_knight, _knight);
            Assert.AreEqual(2f, Value(Counter));
            Assert.AreEqual(0f, Value(Expired));

            _knight.TickStatusEffects(2f);
            Assert.AreEqual(1f, Value(Expired));
        }

        [Test]
        public void Categories_AreForCleanses()
        {
            Poison().Apply(_goblin, _knight);
            var haste = StatusEffectBuilder.Create("Haste").AddCategory(Buff).SetDuration(5f)
                .SetStatBlock(StatBlockBuilder.Create("Hasted").AddTag(Hasted).Build()).Build();
            haste.Apply(_knight, _knight);

            Assert.AreEqual(1, _knight.RemoveStatusEffects(Debuff));
            Assert.IsFalse(_knight.HasTag(Poisoned));
            Assert.IsTrue(_knight.HasTag(Hasted));
            Assert.AreEqual(1, _knight.RemoveStatusEffect(haste));
            Assert.AreEqual(0, _knight.RemoveStatusEffects());
        }

        [Test]
        public void StatusEffects_CanBeObserved()
        {
            var added = new List<string>();
            var removed = new List<string>();
            _knight.StatusEffects.ObserveAdd().Subscribe(e => added.Add(e.Value.Name));
            _knight.StatusEffects.ObserveRemove().Subscribe(e => removed.Add(e.Value.Name));

            Poison().Apply(_goblin, _knight);
            _knight.TickStatusEffects(5f);

            CollectionAssert.AreEqual(new[] { "Poison" }, added);
            CollectionAssert.AreEqual(new[] { "Poison" }, removed);
        }

        [Test]
        public void DisposingTheEntity_RemovesItsStatuses()
        {
            var active = Poison().Apply(_goblin, _knight);
            StatusEndReason? reason = null;
            active.Ended.Subscribe(r => reason = r);

            _knight.Dispose();

            Assert.AreEqual(StatusEndReason.Removed, reason);
            Assert.IsNull(Poison().Apply(_goblin, _knight), "A disposed entity gets no statuses.");
        }

        [Test]
        public void StatusesFromTheSameFile_AreTheSameStatus()
        {
            string json = StatusEffectJson.ToJson(Poison());
            JsonDataLoader.ReadText = path => path == StatusEffectJsonLoader.ResourcesPath + "/Debuffs/Poison" ? json : null;

            var first = StatusEffectJsonLoader.Load("Debuffs/Poison").Apply(_goblin, _knight);
            var second = StatusEffectJsonLoader.Load("Debuffs/Poison.json").Apply(_goblin, _knight);
            Assert.AreSame(first, second, "Loaded twice, still one status: it refreshed.");

            Poison().Apply(_goblin, _knight);
            Assert.AreEqual(2, _knight.StatusEffects.Count, "A status built in code is only itself.");
        }

        [Test]
        public void Effects_ApplyAndRemoveStatuses()
        {
            var poison = Poison();
            var venom = EffectBuilder.Create("Venomous Stab")
                .Reduce(Effect.Target(Health), 5f)
                .ApplyStatus(poison, chance: 0.3f)
                .Build();

            var miss = venom.Apply(_goblin, _knight, new FixedRandom(0.5));
            Assert.AreEqual(0, miss.Statuses.Count, "0.5 isn't below 0.3.");

            var hit = venom.Apply(_goblin, _knight, new FixedRandom(0.1));
            Assert.AreSame(_knight.GetStatusEffect(poison), hit.Statuses.Single());
            Assert.AreSame(_goblin, hit.Statuses[0].Source);

            var rally = EffectBuilder.Create("Rally")
                .ApplyStatus(StatusEffectBuilder.Create("Haste").SetDuration(5f).Build(), EffectRole.Source)
                .Build();
            Assert.AreSame(_goblin, rally.Apply(_goblin, _knight).Statuses.Single().Target, "To the source: a self-buff.");

            var antidote = EffectBuilder.Create("Antidote").RemoveStatuses(Debuff).Build();
            Assert.AreEqual(1, antidote.Apply(_knight, _knight).StatusesRemoved);
            Assert.IsNull(_knight.GetStatusEffect(poison));
        }

        [Test]
        public void ApplyingToNothing_DoesNothing()
        {
            Assert.IsNull(Poison().Apply(_goblin, null));
            _knight.TickStatusEffects(-1f);
            _knight.TickStatusEffects(float.NaN);
            Assert.AreEqual(100f, Current(Health));
        }
    }
}
