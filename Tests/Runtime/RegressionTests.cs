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
            Logic = new LinearLogic { Input = input, Coefficient = coeff }
        };

        private static IAttributeModifier Linear(ValueSource input, float coeff) => new LogicModifier(new LinearLogic { Input = input, Coefficient = coeff });

        private static IAttributeModifier Constant(float value, ModifierType type = ModifierType.Additive, int priority = 0) =>
            new LogicModifier(new ValueLogic(value), type, priority);

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

            var goblinA = new Entity(); goblinA.ApplyProfile(goblinProfile);
            var goblinB = new Entity(); goblinB.ApplyProfile(goblinProfile);

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
            group.ApplyStatBlock(aura);

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
                Modifiers = { new AttributeModifierSpec { TargetAttribute = Health, Logic = new ValueLogic(100f) } }
            };

            LogAssert.Expect(LogType.Error, new Regex("'Last Stand' was disabled"));
            lastStand.ApplyToEntity(e);

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
            e.ApplyProfile(profile);

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
                Modifiers = { new AttributeModifierSpec { Logic = new ValueLogic(1f) } } // no target
            };
            var e = new Entity();

            LogAssert.Expect(LogType.Warning, new Regex("skipped a 'Value' modifier with no Target Attribute"));
            block.ApplyToEntity(e);

            Assert.AreEqual(0, e.Attributes.Count);
        }

        [Test]
        public void ProfileBuilder_SetsProfileName()
        {
            Assert.AreEqual("Orc", ProfileBuilder.Create("Orc").Build().ProfileName);
        }

        // --- Builders and logic --------------------------------------------------------------

        [Test]
        public void AddMultiplierModifier_HalfMeansPlusFiftyPercent()
        {
            var e = new Entity();
            e.SetOrUpdateBaseValue(Speed, 10f);
            StatBlockBuilder.Create("Haste").AddMultiplierModifier(Speed, 0.5f).Build().ApplyToEntity(e);

            Assert.AreEqual(15f, Value(e, Speed));
        }

        [Test]
        public void EveryBuiltInLogic_CanBeCreatedByTheEditor()
        {
            // The Logic dropdown lists [Serializable] ModifierLogic classes with a parameterless constructor.
            var logicTypes = typeof(ModifierLogic).Assembly.GetTypes()
                .Where(t => typeof(ModifierLogic).IsAssignableFrom(t) && !t.IsAbstract)
                .ToList();
            Assert.GreaterOrEqual(logicTypes.Count, 13);

            foreach (var type in logicTypes)
            {
                Assert.IsTrue(type.IsDefined(typeof(SerializableAttribute), false), $"{type.Name} is not [Serializable]");
                Assert.IsNotNull(type.GetConstructor(Type.EmptyTypes), $"{type.Name} has no parameterless constructor");

                var logic = (ModifierLogic)Activator.CreateInstance(type);
                float result = float.NaN;
                logic.Observe(new Entity()).Subscribe(v => result = v);
                Assert.IsFalse(float.IsNaN(result), $"{type.Name} emitted no value with its default settings");
            }
        }

        [Test]
        public void DiminishingReturns_WithZeroInputAndSoftCap_IsZeroNotNaN()
        {
            // A missing input attribute reads as 0 too.
            var logic = new DiminishingReturnsLogic { Input = 0f, MaxBonus = 10f, SoftCap = 0f };
            float result = float.NaN;
            logic.Observe(new Entity()).Subscribe(v => result = v);

            Assert.AreEqual(0f, result);
        }

        [Test]
        public void CreatingFromSpec_DoesNotModifyTheSpec()
        {
            var spec = LinearSpec(Damage, Attr(Strength), 1f);
            var owner = new Entity(); owner.SetOrUpdateBaseValue(Strength, 7f);
            var other = new Entity(); other.SetOrUpdateBaseValue(Strength, 3f);

            var modifier = spec.CreateModifier(owner);

            float read = float.NaN;
            spec.Logic.Observe(other).Subscribe(v => read = v);
            Assert.AreEqual(3f, read, "The shared spec's logic must not be tied to the first entity.");

            float fromModifier = float.NaN;
            modifier.GetMagnitude(other).Subscribe(v => fromModifier = v);
            Assert.AreEqual(7f, fromModifier, "A modifier created for 'owner' reads the owner's Strength wherever it is applied.");
        }

        [Test]
        public void DuplicatedLogic_IsUnsharedAfterDeserialization()
        {
            // A list entry duplicated in the Inspector can point to the same logic object as the original.
            var shared = new LinearLogic { Input = 1f };
            var block = new StatBlock
            {
                Modifiers =
                {
                    new AttributeModifierSpec { TargetAttribute = Damage, Logic = shared },
                    new AttributeModifierSpec { TargetAttribute = Damage, Logic = shared },
                }
            };
            var profile = new EntityProfile { InnateStatBlocks = { block, new StatBlock { Modifiers = { new AttributeModifierSpec { Logic = shared } } } } };

            profile.OnAfterDeserialize();

            var logics = profile.InnateStatBlocks.SelectMany(b => b.Modifiers).Select(m => m.Logic).ToList();
            Assert.AreEqual(3, logics.Distinct().Count(), "Each modifier must get its own logic object.");
            Assert.IsTrue(logics.All(l => l is LinearLogic linear && linear.Input.ConstantValue == 1f), "The copies keep the settings.");
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

        // --- Pipeline rules ------------------------------------------------------------------------

        [Test]
        public void TenPercentMultiplier_AppliesOnce()
        {
            var e = new Entity();
            e.SetOrUpdateBaseValue(MaxHealth, 100f);
            e.AddModifier("Toughness", Constant(1.1f, ModifierType.Multiplicative), MaxHealth);

            Assert.AreEqual(110f, Value(e, MaxHealth), 0.001f);
        }

        [Test]
        public void Multipliers_Compound()
        {
            // Each modifier applies on its own: +10% and +10% make x1.21, not x1.2.
            var e = new Entity();
            e.SetOrUpdateBaseValue(Damage, 100f);
            e.AddModifier("Sword", Constant(1.1f, ModifierType.Multiplicative), Damage);
            e.AddModifier("Ring", Constant(1.1f, ModifierType.Multiplicative), Damage);

            Assert.AreEqual(121f, Value(e, Damage), 0.001f);
        }

        [Test]
        public void ModifierReadingItsOwnAttribute_SettlesAtTheFixedPoint()
        {
            // "+10% of MaxHealth" reads MaxHealth's final value, which includes this bonus: x = 100 + 0.1x settles at 111.1.
            var e = new Entity();
            e.SetOrUpdateBaseValue(MaxHealth, 100f);
            e.AddModifier("Toughness", Linear(Attr(MaxHealth), 0.1f), MaxHealth);

            Assert.AreEqual(111.111f, Value(e, MaxHealth), 0.01f);
        }

        [Test]
        public void ClampMax_KeepsHealthAtMostMaxHealth()
        {
            var e = new Entity();
            e.SetOrUpdateBaseValue(MaxHealth, 100f);
            e.SetOrUpdateBaseValue(Health, 150f);
            e.AddModifier("ClampToMax", new LogicModifier(new ValueLogic(Attr(MaxHealth)), ModifierType.ClampMax), Health);
            Assert.AreEqual(100f, Value(e, Health));

            e.SetOrUpdateBaseValue(Health, 50f);
            Assert.AreEqual(50f, Value(e, Health), "The clamp must not latch at the old maximum.");

            e.SetOrUpdateBaseValue(MaxHealth, 40f);
            Assert.AreEqual(40f, Value(e, Health), "The clamp follows MaxHealth.");
        }

        [Test]
        public void ClampMin_KeepsTheValueAtLeastTheMinimum()
        {
            var e = new Entity();
            e.SetOrUpdateBaseValue(Speed, 10f);
            e.AddModifier("Floor", Constant(2f, ModifierType.ClampMin), Speed);
            e.AddModifier("Slow", Constant(-15f), Speed);

            Assert.AreEqual(2f, Value(e, Speed));
        }

        [Test]
        public void Clamps_ApplyAfterAddsAndMultipliersOfTheSamePriority()
        {
            var e = new Entity();
            e.SetOrUpdateBaseValue(Health, 80f);
            e.AddModifier("Cap", Constant(100f, ModifierType.ClampMax), Health);     // added first...
            e.AddModifier("Vigor", Constant(1.5f, ModifierType.Multiplicative), Health);
            Assert.AreEqual(100f, Value(e, Health), "...but applies after the multiplier: min(80 * 1.5, 100).");

            e.AddModifier("Blessing", Constant(30f, ModifierType.Additive, priority: 1), Health);
            Assert.AreEqual(130f, Value(e, Health), "A later priority applies after the clamp.");
        }

        // --- Known design limitations (not fixed yet) ------------------------------------------

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
