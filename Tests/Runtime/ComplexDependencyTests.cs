using NUnit.Framework;
using ReactiveSolutions.AttributeSystem.Core;
using ReactiveSolutions.AttributeSystem.Core.Modifiers; // For LinearAttributeModifier
using ReactiveSolutions.AttributeSystem.Core.Data; // For ValueSource
using UnityEngine;
using System;
using System.Collections.Generic;
using SemanticKeys;

namespace ReactiveSolutions.AttributeSystem.Tests
{
    /// <summary>
    /// Implements the "Circular Dependency & Race Condition" protocol.
    /// Scenario:
    /// 1. Character adds CasterLevel to Weapon.ItemLevel
    /// 2. Weapon ItemLevel adds to Character.Strength
    /// </summary>
    public class ComplexDependencyTests
    {
        private AttributeProcessor _charProc;
        private AttributeProcessor _weapProc;

        // We wrap steps in Actions to allow easy permutation testing
        private Dictionary<string, Action> _steps;

        // Helper to wrap inputs into standard Linear Modifier Args (Input, 1, 0)
        private ModifierArgs CreateLinearArgs(string sourceId, ValueSource input)
        {
            return new ModifierArgs(
                sourceId,
                ModifierType.Additive,
                0,
                new List<ValueSource> { input, ValueSource.Const(1f), ValueSource.Const(0f) }
            );
        }
        [SetUp]
        public void Setup()
        {
            _charProc = new AttributeProcessor();
            _weapProc = new AttributeProcessor();

            _steps = new Dictionary<string, Action>
            {
                { "A", Step_A_AddCasterLevel },
                { "B", Step_B_AddCasterToWeapon },
                { "C", Step_C_AddItemLevel },
                { "D", Step_D_AddItemToStrength },
                { "E", Step_E_LinkOwner },
                { "F", Step_F_LinkEquipped }
            };
        }

        // --- THE STEPS ---

        // Step A: Add CasterLevel to Character (Base 2)
        private void Step_A_AddCasterLevel()
            => _charProc.SetOrUpdateBaseValue(TestKeys.Mock("CasterLevel"), 2f);

        // Step B: Add modifier: Add CasterLevel value to EquippedWeapon.ItemLevel
        private void Step_B_AddCasterToWeapon()
        {
            // CRITICAL NOTE: This modifier lives on the WEAPON (the target).
            // Therefore, it must find CasterLevel via the "Owner" link, not directly.
            var source = new ValueSource
            {
                Mode = ValueSource.SourceMode.Attribute,
                AttributeName = TestKeys.Mock("CasterLevel"),
                ProviderPath = new List<SemanticKey> { TestKeys.Mock("Owner") }
            };


            var mod = new LinearModifier(CreateLinearArgs("CasterScaling", source));

            var AttributeName = TestKeys.Mock("ItemLevel");
            var ProviderPath = new List<SemanticKey> { TestKeys.Mock("EquippedWeapon") };
            // Character tries to push this modifier to its 'EquippedWeapon'
            _charProc.AddModifier("CasterBonus", mod, AttributeName, ProviderPath);
        }

        // Step C: Add ItemLevel to Weapon (Base 6)
        private void Step_C_AddItemLevel()
            => _weapProc.SetOrUpdateBaseValue(TestKeys.Mock("ItemLevel"), 6f);

        // Step D: Add modifier: Add ItemLevel to Owner.Strength
        private void Step_D_AddItemToStrength()
        {
            // Modifier lives on CHARACTER (Target: Owner.Strength... wait, Weapon acts on Owner).
            // Logic: Add Weapon.ItemLevel to Character.Strength.
            // Target is "Owner.Strength". Modifier is added to "Owner.Strength".

            // Source is "ItemLevel" (Local to Weapon).
            // BUT: If we add the modifier to "Owner.Strength", the modifier LIVES ON THE OWNER.
            // So the Owner (Character) needs to read "EquippedWeapon.ItemLevel".

            // However, your protocol implies the WEAPON adds the modifier.
            // "Add modifier: Add ItemLevel to Owner.Strength"
            // If the Weapon Processor executes this, it adds a modifier to the Remote Attribute "Owner.Strength".
            // That modifier will live on the Character.
            // So the Source Path in that modifier must be "EquippedWeapon.ItemLevel" (Back reference) 
            // OR the system must support snapshotting values?

            // Let's assume standard reactive flow: The Character pulls value from the Weapon.
            // So the ValueSource is "EquippedWeapon.ItemLevel" relative to the Character.

            var source = new ValueSource
            {
                Mode = ValueSource.SourceMode.Attribute,
                AttributeName = TestKeys.Mock("ItemLevel"),
                ProviderPath = new List<SemanticKey> { TestKeys.Mock("EquippedWeapon") }
            };

            var mod = new LinearModifier(CreateLinearArgs("ItemScaling", source));

            // Weapon adds this to its Owner

            var AttributeName = TestKeys.Mock("Strength");
            var ProviderPath = new List<SemanticKey> { TestKeys.Mock("Owner") };
            _weapProc.AddModifier("ItemBonus", mod, AttributeName, ProviderPath);
        }

        // Step E: Link Character to Weapon as "Owner" (Weapon.Owner = Char)
        private void Step_E_LinkOwner() => _weapProc.RegisterExternalProvider(TestKeys.Mock("Owner"), _charProc);

        // Step F: Link Weapon to Character as "EquipedWeapon" (Char.EquippedWeapon = Weap)
        private void Step_F_LinkEquipped() => _charProc.RegisterExternalProvider(TestKeys.Mock("EquippedWeapon"), _weapProc);


        // --- THE SCENARIOS ---

        [Test]
        public void Scenario_HappyPath_LinksFirst()
        {
            // Valid Order: Create Attributes -> Link -> Add Modifiers
            // Steps: Start -> A, C -> E, F -> B, D
            RunSequence("A", "C", "E", "F", "B", "D");

            AssertFinalValues();
        }

        [Test]
        public void Scenario_RaceCondition_ModifiersBeforeLinks()
        {
            // The "Bad" Order: Modifiers try to attach to providers that don't exist yet.
            // Steps: Start -> A, C -> B (Fail?) -> D (Fail?) -> E, F

            // Expected Behavior: The AddModifier call should ideally QUEUE the modifier,
            // or at least not crash.

            try
            {
                RunSequence("A", "C", "B", "D", "E", "F");
                AssertFinalValues();
            }
            catch (System.Exception e)
            {
                // If this fails, it confirms your suspicion: The code does not handle this race condition yet.
                Assert.Fail($"Race Condition Failure: {e.GetType().Name} - {e.Message}\n" +
                            "This likely means AttributeProcessor.AddModifier tried to access a missing provider immediately.");
            }
        }

        [Test]
        public void Scenario_Mixed_Interleaved()
        {
            // A semi-random realistic loading order
            // Char loads (A), Weapon links to Char (E), Weapon loads (C), Weapon applies mod to Char (D),
            // Char applies mod to Weapon (B), Char links to Weapon (F).

            try
            {
                RunSequence("A", "E", "C", "D", "B", "F");
                AssertFinalValues();
            }
            catch (System.Exception e)
            {
                Assert.Fail($"Interleaved Failure: {e.Message}");
            }
        }

        [Test]
        public void Scenario_WeaponSwap_ComplexPath()
        {
            // 1. Setup Processors
            var mainChar = new AttributeProcessor();
            var hireling = new AttributeProcessor();
            var plainSword = new AttributeProcessor();
            var magicSword = new AttributeProcessor();

            // 2. Setup Base Values
            // Char: CasterLevel = 5
            mainChar.SetOrUpdateBaseValue(TestKeys.Mock("CasterLevel"), 5f);

            // PlainSword: Damage = 3
            plainSword.SetOrUpdateBaseValue(TestKeys.Mock("Damage"), 3f);

            // MagicSword: Damage = 7, CasterLevel = 3
            magicSword.SetOrUpdateBaseValue(TestKeys.Mock("Damage"), 7f);
            magicSword.SetOrUpdateBaseValue(TestKeys.Mock("CasterLevel"), 3f);

            // 3. Setup MagicSword Modifiers

            // Mod A: Self Damage += Self CasterLevel
            // Source: CasterLevel (Local/Baked to MagicSword context)
            var sourceA = new ValueSource
            {
                Mode = ValueSource.SourceMode.Attribute,
                AttributeName = TestKeys.Mock("CasterLevel")
            };
            sourceA.BakeContext(magicSword); // Explicitly bake context so it finds CasterLevel on MagicSword

            // Manual Arg construction
            var modA = new LinearModifier(new ModifierArgs(
                "MagicSwordSelfBuff",
                ModifierType.Additive,
                0,
                new List<ValueSource> { sourceA, ValueSource.Const(1f), ValueSource.Const(0f) }
            )); magicSword.AddModifier("SelfBuff", modA, TestKeys.Mock("Damage"));

            // Mod B: Owner->Hireling->EquippedWeapon->Damage += Self CasterLevel
            var sourceB = new ValueSource
            {
                Mode = ValueSource.SourceMode.Attribute,
                AttributeName = TestKeys.Mock("CasterLevel")
            };
            sourceB.BakeContext(magicSword); // Source is still MagicSword.CasterLevel

            string shareBuffId = "MagicSwordShareBuff";
            var modB = new LinearModifier(new ModifierArgs(
                shareBuffId,
                ModifierType.Additive,
                0,
                new List<ValueSource> { sourceB, ValueSource.Const(1f), ValueSource.Const(0f) }
            ));
            
            // Target Path: Owner -> Hireling -> EquippedWeapon
            var targetPath = new List<SemanticKey>
            {
                TestKeys.Mock("Owner"),
                TestKeys.Mock("Hireling"),
                TestKeys.Mock("EquippedWeapon")
            };

            // Add the remote modifier definition to MagicSword (it will push it down the path)
            magicSword.AddModifier("ShareBuff", modB, TestKeys.Mock("Damage"), targetPath);

            // 4. Constant Links (Hireling is always linked to Char)
            mainChar.RegisterExternalProvider(TestKeys.Mock("Hireling"), hireling);

            // --- PHASE 1: Char has MagicSword, Hireling has PlainSword ---

            // Link Char <-> MagicSword
            mainChar.RegisterExternalProvider(TestKeys.Mock("EquippedWeapon"), magicSword);
            magicSword.RegisterExternalProvider(TestKeys.Mock("Owner"), mainChar);

            // Link Hireling <-> PlainSword
            hireling.RegisterExternalProvider(TestKeys.Mock("EquippedWeapon"), plainSword);
            plainSword.RegisterExternalProvider(TestKeys.Mock("Owner"), hireling);

            // Assertions Phase 1
            // MagicSword Damage: 7 + 3 = 10
            Assert.AreEqual(10f, magicSword.GetAttribute(TestKeys.Mock("Damage")).ReactivePropertyAccess.Value, "Phase 1: MagicSword Damage incorrect");

            // PlainSword Damage: 3 + 3 (Shared Buff) = 6
            // Path Trace: MagicSword(Context) -> Owner(Char) -> Hireling(Hireling) -> EquippedWeapon(PlainSword) -> Add Mod
            Assert.AreEqual(6f, plainSword.GetAttribute(TestKeys.Mock("Damage")).ReactivePropertyAccess.Value, "Phase 1: PlainSword Damage incorrect");


            // --- PHASE 2: Swap ---

            // Simulate Unequip: Remove the "ShareBuff" from PlainSword
            // FIX: Use the ID defined in the modifier constructor
            plainSword.RemoveModifiersBySource(shareBuffId);

            // Update Links
            // Link Char <-> PlainSword
            mainChar.RegisterExternalProvider(TestKeys.Mock("EquippedWeapon"), plainSword);
            plainSword.RegisterExternalProvider(TestKeys.Mock("Owner"), mainChar);

            // Link Hireling <-> MagicSword
            hireling.RegisterExternalProvider(TestKeys.Mock("EquippedWeapon"), magicSword);
            magicSword.RegisterExternalProvider(TestKeys.Mock("Owner"), hireling);

            // Re-Add Modifiers logic? 
            // In a real system, connecting MagicSword to Hireling should trigger the MagicSword to re-evaluate its modifiers.
            // Since our modifiers were "Pushed" once, we need to re-push them for the new path.
            // Simulate "OnEquip" call which re-adds the modifiers:
            magicSword.AddModifier("ShareBuff", modB, TestKeys.Mock("Damage"), targetPath);

            // Assertions Phase 2

            // MagicSword Damage: 7 + 3 = 10 (Self buff always works)
            Assert.AreEqual(10f, magicSword.GetAttribute(TestKeys.Mock("Damage")).ReactivePropertyAccess.Value, "Phase 2: MagicSword Damage incorrect");

            // PlainSword Damage: 3 (Base)
            // Path Trace: MagicSword(Context) -> Owner(Hireling) -> Hireling(MISSING on Hireling) -> ...
            // The modifier chain breaks at "Hireling" because the Hireling doesn't have a provider named "Hireling".
            // So PlainSword gets nothing.
            Assert.AreEqual(3f, plainSword.GetAttribute(TestKeys.Mock("Damage")).ReactivePropertyAccess.Value, "Phase 2: PlainSword Damage incorrect");
        }

        // --- HELPERS ---

        private void RunSequence(params string[] stepKeys)
        {
            foreach (var key in stepKeys)
            {
                if (_steps.TryGetValue(key, out var action)) action.Invoke();
                else Debug.LogError($"Unknown Step: {key}");
            }
        }

        private void AssertFinalValues()
        {
            // 1. Check Weapon ItemLevel
            // Base 6 + CasterLevel(2) = 8
            var itemLvl = _weapProc.GetAttribute(TestKeys.Mock("ItemLevel"));
            Assert.IsNotNull(itemLvl, "Weapon.ItemLevel missing");
            Assert.AreEqual(8f, itemLvl.ReactivePropertyAccess.Value, "Weapon ItemLevel Calculation Failed");

            // 2. Check Character Strength
            // Base 0 + ItemLevel(8) = 8
            // Note: GetAttribute might need to auto-create Strength if Step D was the only thing adding to it
            var str = _charProc.GetAttribute(TestKeys.Mock("Strength"));
            // If Step D failed to add the modifier, this might be null or 0
            Assert.IsNotNull(str, "Character.Strength missing");
            Assert.AreEqual(8f, str.ReactivePropertyAccess.Value, "Character Strength Calculation Failed");
        }
    }
}