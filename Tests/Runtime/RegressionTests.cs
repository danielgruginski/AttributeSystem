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
    /// <summary>
    /// Regression tests for defects found during the September 2026 review.
    /// Tests marked [Ignore] document known design limitations that are not fixed yet.
    /// </summary>
    public class RegressionTests
    {
        private readonly SemanticKey Strength = TestKeys.Mock("Strength");
        private readonly SemanticKey Damage = TestKeys.Mock("Damage");
        private readonly SemanticKey Health = TestKeys.Mock("Health");
        private readonly SemanticKey MaxHealth = TestKeys.Mock("MaxHealth");
        private readonly SemanticKey Level = TestKeys.Mock("Level");
        private readonly SemanticKey Speed = TestKeys.Mock("Speed");
        private readonly SemanticKey Owner = TestKeys.Mock("Owner");

        // --- helpers -------------------------------------------------------------------

        private static float Value(Entity e, SemanticKey key) => e.GetAttribute(key).ObservableValue.Value;

        private static ValueSource Const(float v) => ValueSource.Const(v);

        private static ValueSource Attr(SemanticKey name, params SemanticKey[] path) => new ValueSource
        {
            Mode = ValueSource.SourceMode.Attribute,
            AttributeRef = new AttributeReference(name, path.ToList())
        };

        private static AttributeModifierSpec LinearSpec(SemanticKey target, ValueSource input, float coeff) => new AttributeModifierSpec
        {
            TargetAttribute = target,
            Type = ModifierType.Additive,
            LogicType = sk.Modifiers.Linear,
            Arguments = new List<ValueSource> { input, Const(coeff), Const(0f) }
        };

        private static IAttributeModifier Linear(ValueSource input, float coeff) => new LinearModifier(LinearSpec(SemanticKey.None, input, coeff));

        private static IAttributeModifier Constant(float value, ModifierType type = ModifierType.Additive, int priority = 0) =>
            new StaticAttributeModifier(new AttributeModifierSpec { Type = type, Priority = priority, Arguments = new List<ValueSource> { Const(value) } });

        private class CountingModifier : IAttributeModifier
        {
            public static int GetMagnitudeCalls;
            public ModifierType Type => ModifierType.Additive;
            public int Priority => 0;
            public string SourceId => "Counting";
            public IObservable<float> GetMagnitude(Entity processor) { GetMagnitudeCalls++; return Observable.Return(1f); }
        }

        // --- Shared StatBlock data must not be mutated per entity ------------------------

        [Test]
        public void EntitiesFromSameProfile_DoNotReadEachOthersAttributes()
        {
            // One profile, innate passive "Damage += Strength", used to spawn two goblins.
            var goblinProfile = ProfileBuilder.Create("Goblin")
                .AddBaseAttribute(Strength, 10f)
                .AddInnateStatBlock(new StatBlock { Modifiers = { LinearSpec(Damage, Attr(Strength), 1f) } })
                .Build();

            var factory = new ModifierFactory();
            var goblinA = new Entity(); goblinA.ApplyProfile(goblinProfile, factory);
            var goblinB = new Entity(); goblinB.ApplyProfile(goblinProfile, factory);

            goblinB.SetOrUpdateBaseValue(Strength, 2f);            // goblin B gets weakened
            goblinA.AddModifier("Potion", Constant(5f), Damage);   // goblin A drinks a +5 damage potion

            Assert.AreEqual(15f, Value(goblinA, Damage), "Goblin A must use its own Strength (10) + 5.");
            Assert.AreEqual(2f, Value(goblinB, Damage));
        }

        [Test]
        public void LinkGroupAura_DoesNotMixUpMembers()
        {
            var aura = new StatBlock { Modifiers = { LinearSpec(Damage, Attr(Strength), 1f) } };
            var group = new LinkGroup();
            var warrior = new Entity(); warrior.SetOrUpdateBaseValue(Strength, 30f);
            var mage = new Entity(); mage.SetOrUpdateBaseValue(Strength, 5f);
            group.AddMember(warrior);
            group.AddMember(mage);
            group.ApplyStatBlock(aura, new ModifierFactory());

            warrior.AddModifier("Potion", Constant(5f), Damage);

            Assert.AreEqual(35f, Value(warrior, Damage), "The warrior's aura bonus must come from the warrior's Strength.");
        }

        // --- Missing attributes read as 0, local or remote -------------------------------

        [Test]
        public void ModifierWithMissingLocalSource_DoesNotFreezeTarget()
        {
            var e = new Entity();
            e.SetOrUpdateBaseValue(Damage, 10f);
            e.AddModifier("LevelScaling", Linear(Attr(Level), 2f), Damage);   // "Level" doesn't exist yet

            e.SetOrUpdateBaseValue(Damage, 20f);
            Assert.AreEqual(20f, Value(e, Damage), "A missing source reads as 0; it must not block the pipeline.");

            e.SetOrUpdateBaseValue(Level, 3f);
            Assert.AreEqual(26f, Value(e, Damage), "Once the source exists, the modifier picks it up.");
        }

        // --- Disposal ----------------------------------------------------------------------

        [Test]
        public void DisposedEntity_StopsReactingToOtherEntities()
        {
            var player = new Entity(); player.SetOrUpdateBaseValue(Strength, 10f);
            var sword = new Entity();
            sword.RegisterExternalProvider(Owner, player);
            sword.AddModifier("StrScaling", Linear(Attr(Strength, Owner), 1f), Damage);
            var swordDamage = sword.GetAttribute(Damage);

            sword.Dispose();
            player.SetOrUpdateBaseValue(Strength, 20f);

            Assert.IsTrue(swordDamage.IsDisposed);
            Assert.AreEqual(10f, swordDamage.ObservableValue.Value, "A disposed entity must not stay subscribed to other entities.");
        }

        // --- Circular dependencies are reported instead of overflowing the stack -----------

        [Test]
        public void SelfReferencingModifier_IsReportedInsteadOfOverflowingTheStack()
        {
            var e = new Entity();
            e.SetOrUpdateBaseValue(MaxHealth, 100f);

            LogAssert.Expect(LogType.Error, new Regex("Circular dependency on 'MaxHealth'"));
            e.AddModifier("Echo", Linear(Attr(MaxHealth), 1f), MaxHealth);   // previously: StackOverflowException

            Assert.IsFalse(float.IsInfinity(Value(e, MaxHealth)));
        }

        [Test]
        public void SelfDefeatingCondition_IsDisabledInsteadOfOscillating()
        {
            var e = new Entity();
            e.SetOrUpdateBaseValue(Health, 40f);
            var lastStand = new StatBlock
            {
                BlockName = "Last Stand",
                ActivationCondition = new StatBlockCondition
                {
                    Type = StatBlockCondition.Mode.ValueComparison,
                    ValueA = Attr(Health), CompareOp = StatBlockCondition.Comparison.Less, ValueB = Const(50f)
                },
                Modifiers = { new AttributeModifierSpec { TargetAttribute = Health, LogicType = sk.Modifiers.Static, Arguments = { Const(100f) } } }
            };

            LogAssert.Expect(LogType.Error, new Regex("'Last Stand' was disabled"));
            lastStand.ApplyToEntity(e, new ModifierFactory());

            Assert.AreEqual(0, e.GetAttribute(Health).Modifiers.Count(), "The block must not stay applied while its condition is false.");
            Assert.AreEqual(40f, Value(e, Health));

            e.SetOrUpdateBaseValue(Health, -70f);   // previously: StackOverflowException
            Assert.AreEqual(0, e.GetAttribute(Health).Modifiers.Count());
        }

        // --- Pipeline order ----------------------------------------------------------------

        [Test]
        public void Result_DoesNotDependOnModifierInsertionOrder()
        {
            var ringFirst = new Entity(); ringFirst.SetOrUpdateBaseValue(Damage, 10f);
            ringFirst.AddModifier("Ring", Constant(2f, ModifierType.Multiplicative), Damage);
            ringFirst.AddModifier("Sword", Constant(10f), Damage);

            var swordFirst = new Entity(); swordFirst.SetOrUpdateBaseValue(Damage, 10f);
            swordFirst.AddModifier("Sword", Constant(10f), Damage);
            swordFirst.AddModifier("Ring", Constant(2f, ModifierType.Multiplicative), Damage);

            Assert.AreEqual(40f, Value(ringFirst, Damage), "At equal priority, multipliers scale base + additives.");
            Assert.AreEqual(40f, Value(swordFirst, Damage));
        }

        [Test]
        public void Priority_TakesPrecedenceOverModifierType()
        {
            var e = new Entity(); e.SetOrUpdateBaseValue(Damage, 10f);
            e.AddModifier("Flat", Constant(10f, ModifierType.Additive, priority: 10), Damage);
            e.AddModifier("Early x2", Constant(2f, ModifierType.Multiplicative, priority: 0), Damage);

            Assert.AreEqual(30f, Value(e, Damage), "(10 x 2) + 10: the lower priority runs first regardless of type.");
        }

        // --- Pipeline cost -------------------------------------------------------------------

        [Test]
        public void BaseValueChanges_DoNotResubscribeModifiers()
        {
            var e = new Entity();
            e.SetOrUpdateBaseValue(Health, 0f);
            for (int i = 0; i < 5; i++) e.AddModifier("m" + i, new CountingModifier(), Health);

            CountingModifier.GetMagnitudeCalls = 0;
            for (int i = 1; i <= 1000; i++) e.SetOrUpdateBaseValue(Health, i);

            Assert.AreEqual(0, CountingModifier.GetMagnitudeCalls);
            Assert.AreEqual(1005f, Value(e, Health));
        }

        [Test]
        public void AddingModifiers_SubscribesOnlyTheNewModifier()
        {
            var e = new Entity();
            CountingModifier.GetMagnitudeCalls = 0;
            for (int i = 0; i < 50; i++) e.AddModifier("m" + i, new CountingModifier(), Health);

            Assert.AreEqual(50, CountingModifier.GetMagnitudeCalls);
            Assert.AreEqual(50f, Value(e, Health));
        }

        // --- Pointers ------------------------------------------------------------------------

        [Test]
        public void RemotePointer_IsNotMistakenForALocalCycle()
        {
            var mainStat = TestKeys.Mock("MainStat");
            var owner = new Entity(); owner.SetOrUpdateBaseValue(Strength, 12f);
            var sword = new Entity();
            sword.RegisterExternalProvider(Owner, owner);

            sword.SetPointer(Strength, mainStat);                                   // local: Strength -> MainStat
            sword.SetPointer(mainStat, Strength, new List<SemanticKey> { Owner });  // MainStat -> Owner.Strength: no cycle

            Assert.AreEqual(12f, Value(sword, mainStat));
            Assert.AreEqual(12f, Value(sword, Strength));
        }

        // --- Profiles ------------------------------------------------------------------------

        [Test]
        public void ApplyProfile_SkipsUnassignedKeys()
        {
            // SemanticKey is a struct, so an unassigned Inspector entry is SemanticKey.None, never null.
            var profile = ProfileBuilder.Create("Half-filled").Build();
            profile.BaseAttributes.Add(new BaseAttributeEntry { BaseValue = 5f });
            profile.InnateTags.Add(SemanticKey.None);
            profile.LinkGroups.Add(SemanticKey.None);

            var e = new Entity();
            e.ApplyProfile(profile, new ModifierFactory());

            Assert.AreEqual(0, e.Attributes.Count);
            Assert.AreEqual(0, e.Tags.Count);
            Assert.IsNull(e.GetLinkGroup(SemanticKey.None));
        }

        [Test]
        public void StatBlock_SkipsUnassignedKeys()
        {
            var block = new StatBlock
            {
                BaseValues = { new StatBlock.BaseValueEntry { Value = 5f } },   // Name left unassigned
                Modifiers = { new AttributeModifierSpec { LogicType = sk.Modifiers.Static, Arguments = { Const(1f) } } } // no target
            };
            var e = new Entity();

            LogAssert.Expect(LogType.Warning, new Regex("skipped a 'Static' modifier with no Target Attribute"));
            block.ApplyToEntity(e, new ModifierFactory());

            Assert.AreEqual(0, e.Attributes.Count);
        }

        [Test]
        public void ProfileBuilder_SetsProfileName()
        {
            Assert.AreEqual("Orc", ProfileBuilder.Create("Orc").Build().ProfileName);
        }

        // --- Builders and factory --------------------------------------------------------------

        [Test]
        public void AddMultiplierModifier_HalfMeansPlusFiftyPercent()
        {
            var e = new Entity();
            e.SetOrUpdateBaseValue(Speed, 10f);
            StatBlockBuilder.Create("Haste").AddMultiplierModifier(Speed, 0.5f).Build().ApplyToEntity(e, new ModifierFactory());

            Assert.AreEqual(15f, Value(e, Speed));
        }

        [Test]
        public void EveryBuiltInLogicType_ResolvesFromItsKey()
        {
            var factory = new ModifierFactory();
            var logicTypes = new[]
            {
                sk.Modifiers.Linear, sk.Modifiers.Polynomial, sk.Modifiers.Clamp, sk.Modifiers.Min, sk.Modifiers.Max,
                sk.Modifiers.Floor, sk.Modifiers.Step, sk.Modifiers.Ratio, sk.Modifiers.Exponential,
                sk.Modifiers.DiminishingReturns, sk.Modifiers.ScaledTriangular
            };

            foreach (var logicType in logicTypes)
            {
                var spec = new AttributeModifierSpec { LogicType = logicType, Arguments = { Const(1f) } };
                Assert.IsNotInstanceOf<StaticAttributeModifier>(factory.Create(spec, null), $"{logicType} fell back to Static");
                CollectionAssert.AreNotEqual(new[] { "Value" }, ModifierFactory.GetParameterNames(logicType), $"{logicType} has no parameter names");
            }
        }

        [Test]
        public void DiminishingReturns_WithZeroInputAndSoftCap_IsZeroNotNaN()
        {
            // Unset arguments are 0, and a missing input attribute also reads as 0.
            var spec = new AttributeModifierSpec { LogicType = sk.Modifiers.DiminishingReturns, Arguments = { Const(0f), Const(10f), Const(0f) } };
            float result = float.NaN;
            new ModifierFactory().Create(spec, null).GetMagnitude(new Entity()).Subscribe(v => result = v);

            Assert.AreEqual(0f, result);
        }

        [Test]
        public void UnknownLogicType_WarnsAndFallsBackToStatic()
        {
            var spec = new AttributeModifierSpec { LogicType = SemanticKey.FromRawString("Typo"), Arguments = { Const(3f) } };

            LogAssert.Expect(LogType.Warning, "[ModifierFactory] Unknown modifier logic type 'Typo'. Falling back to Static.");
            var modifier = new ModifierFactory().Create(spec, null);

            Assert.IsInstanceOf<StaticAttributeModifier>(modifier);
        }

        [Test]
        public void CreatingFromSpec_DoesNotModifyTheSpec()
        {
            var source = Attr(Strength);
            var spec = LinearSpec(Damage, source, 1f);
            var owner = new Entity(); owner.SetOrUpdateBaseValue(Strength, 7f);
            var other = new Entity(); other.SetOrUpdateBaseValue(Strength, 3f);

            new ModifierFactory().Create(spec, owner);

            float read = float.NaN;
            source.GetObservable(other).Subscribe(v => read = v);
            Assert.AreEqual(3f, read, "The shared spec's ValueSource must not have been baked to the first entity.");
        }

        // --- Retargeting observers ---------------------------------------------------------------

        [Test]
        public void RetargetedMonitor_IgnoresPreviousTarget()
        {
            var target = new ReactiveProperty<Entity>();
            float shown = -1f;
            target.ObserveAttributeValue(Health).Subscribe(v => shown = v);

            var player = new Entity(); player.SetOrUpdateBaseValue(Health, 100f);
            var enemy = new Entity(); enemy.SetOrUpdateBaseValue(Health, 50f);
            target.Value = player;
            target.Value = enemy;                          // health bar now shows the enemy
            player.SetOrUpdateBaseValue(Health, 1f);       // ...then the player gets hit

            Assert.AreEqual(50f, shown);
        }

        // --- Known design limitations (not fixed yet) ------------------------------------------

        [Test, Ignore("Design: modifiers read an attribute's FINAL value, so '+10% of self' converges to a fixed point (111.1). Needs a BaseValue source mode.")]
        public void TenPercentOfOwnValue_IsTenPercent()
        {
            var e = new Entity();
            e.SetOrUpdateBaseValue(MaxHealth, 100f);
            e.AddModifier("Toughness", Linear(Attr(MaxHealth), 0.1f), MaxHealth);

            Assert.AreEqual(110f, Value(e, MaxHealth), 0.001f);
        }

        [Test, Ignore("Design: a modifier can't act on the running pipeline value, so clamping an attribute to another one latches. Needs clamp operations in the pipeline.")]
        public void ClampOverride_LetsHealthGoDownAgain()
        {
            var e = new Entity();
            e.SetOrUpdateBaseValue(MaxHealth, 100f);
            e.SetOrUpdateBaseValue(Health, 150f);
            var clamp = new ModifierFactory().Create(sk.Modifiers.Clamp, new AttributeModifierSpec
            {
                Type = ModifierType.Override,
                Priority = 1000,
                Arguments = new List<ValueSource> { Attr(Health), Const(0f), Attr(MaxHealth) }
            });
            e.AddModifier("ClampToMax", clamp, Health);
            Assert.AreEqual(100f, Value(e, Health));

            e.SetOrUpdateBaseValue(Health, 50f);
            Assert.AreEqual(50f, Value(e, Health));
        }

        [Test, Ignore("Design: updates are pushed one dependency at a time, so diamond-shaped dependencies emit intermediate values. Needs batched/topological propagation.")]
        public void DependentValue_NeverObservesHalfUpdatedInputs()
        {
            var x = TestKeys.Mock("X"); var a = TestKeys.Mock("A"); var b = TestKeys.Mock("B"); var c = TestKeys.Mock("C");
            var e = new Entity();
            e.SetOrUpdateBaseValue(x, 1f);
            e.AddModifier("a", Linear(Attr(x), 1f), a);                // A = X
            e.AddModifier("b", Linear(Attr(x), 1f), b);                // B = X
            e.AddModifier("c+a", Linear(Attr(a), 1f), c);              // C = A - B  (always 0)
            e.AddModifier("c-b", Linear(Attr(b), -1f), c);

            var seen = new List<float>();
            e.GetAttribute(c).ObservableValue.Subscribe(seen.Add);
            seen.Clear();
            e.SetOrUpdateBaseValue(x, 10f);

            Assert.IsTrue(seen.All(v => v == 0f), $"Observers saw: [{string.Join(", ", seen)}]");
        }
    }
}
