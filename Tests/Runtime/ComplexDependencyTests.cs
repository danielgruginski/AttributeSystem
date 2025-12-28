using NUnit.Framework;
using ReactiveSolutions.AttributeSystem.Core;
using ReactiveSolutions.AttributeSystem.Core.Modifiers; // For LinearAttributeModifier
using ReactiveSolutions.AttributeSystem.Core.Data; // For ValueSource
using UnityEngine;
using System;
using System.Collections.Generic;

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
            => _charProc.SetOrUpdateBaseValue("CasterLevel", 2f);

        // Step B: Add modifier: Add CasterLevel value to EquippedWeapon.ItemLevel
        private void Step_B_AddCasterToWeapon()
        {
            // CRITICAL NOTE: This modifier lives on the WEAPON (the target).
            // Therefore, it must find CasterLevel via the "Owner" link, not directly.
            var source = new ValueSource
            {
                Mode = ValueSource.SourceMode.Attribute,
                AttributeName = "CasterLevel",
                ProviderPath = new List<string> { "Owner" }
            };

            var mod = new LinearAttributeModifier("CasterScaling", ModifierType.Additive, 0, source, 1f, 0f);

            // Character tries to push this modifier to its 'EquippedWeapon'
            _charProc.AddModifier("CasterBonus", mod, "EquippedWeapon.ItemLevel");
        }

        // Step C: Add ItemLevel to Weapon (Base 6)
        private void Step_C_AddItemLevel()
            => _weapProc.SetOrUpdateBaseValue("ItemLevel", 6f);

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
                AttributeName = "ItemLevel",
                ProviderPath = new List<string> { "EquippedWeapon" }
            };

            var mod = new LinearAttributeModifier("ItemScaling", ModifierType.Additive, 0, source, 1f, 0f);

            // Weapon adds this to its Owner
            _weapProc.AddModifier("ItemBonus", mod, "Owner.Strength");
        }

        // Step E: Link Character to Weapon as "Owner" (Weapon.Owner = Char)
        private void Step_E_LinkOwner() => _weapProc.RegisterExternalProvider("Owner", _charProc);

        // Step F: Link Weapon to Character as "EquipedWeapon" (Char.EquippedWeapon = Weap)
        private void Step_F_LinkEquipped() => _charProc.RegisterExternalProvider("EquippedWeapon", _weapProc);


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
            var itemLvl = _weapProc.GetAttribute("ItemLevel");
            Assert.IsNotNull(itemLvl, "Weapon.ItemLevel missing");
            Assert.AreEqual(8f, itemLvl.ReactivePropertyAccess.Value, "Weapon ItemLevel Calculation Failed");

            // 2. Check Character Strength
            // Base 0 + ItemLevel(8) = 8
            // Note: GetAttribute might need to auto-create Strength if Step D was the only thing adding to it
            var str = _charProc.GetAttribute("Strength");
            // If Step D failed to add the modifier, this might be null or 0
            Assert.IsNotNull(str, "Character.Strength missing");
            Assert.AreEqual(8f, str.ReactivePropertyAccess.Value, "Character Strength Calculation Failed");
        }
    }
}