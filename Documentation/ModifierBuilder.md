
# ModifierBuilder Documentation

## Overview

The `ModifierBuilder` is a fluent API designed to construct `AttributeModifierSpec` POCOs.

Defining modifiers manually requires setting up complex arrays of `ValueSource` and `AttributeReference` objects. The `ModifierBuilder` abstracts all of this boilerplate away behind clean, chainable methods. It includes fast-path helpers for the most common modifier types (Flat Additions, Multipliers, and Linear Scaling) while still allowing deep customization for complex logic.

## 1. The "90%" Helpers

For the vast majority of your game's modifiers, you can use these rapid-setup methods. They automatically configure the `Target`, `LogicType`, `ModifierType`, and all necessary arguments.

### Flat Modifiers (`MakeFlat`)

Used for flat additive or subtractive bonuses (e.g., +10 Health, -5 Speed).

```
var spec = ModifierBuilder.Create()
    .MakeFlat(new SemanticKey("Health"), 10f)
    .SetSourceId(new SemanticKey("HealthRingBonus"))
    .Build();

```

### Multipliers (`MakeMultiplier`)

Used for percentage-based scaling applied to the base value (e.g., +50% Damage).

```
var spec = ModifierBuilder.Create()
    .MakeMultiplier(new SemanticKey("Damage"), 0.5f) // Adds 50% of base damage
    .SetSourceId(new SemanticKey("FrenzyMultiplier"))
    .Build();

```

### Linear Scaling (`MakeLinearScaling`)

Used when a target attribute needs to scale based on a _different_ attribute, often from a remote entity (e.g., Weapon Damage scales by 1.5x of the Owner's Strength).

```
var spec = ModifierBuilder.Create()
    .MakeLinearScaling(
        targetAttr: new SemanticKey("Damage"),       // What we are modifying
        sourceAttr: new SemanticKey("Strength"),     // What we are reading from
        multiplier: 1.5f,                            // The scaling coefficient
        sourcePath: new SemanticKey("Owner")         // Where to find the source (optional)
    )
    .SetSourceId(new SemanticKey("StrScaling"))
    .Build();

```

## 2. Manual Configuration (Advanced)

If you have registered custom `LogicType` lambdas in your `ModifierFactory` that require unique argument setups, you can build the spec manually step-by-step.

```
var spec = ModifierBuilder.Create()
    // 1. Set the Target (and optional remote path)
    .SetTarget(new SemanticKey("Health"))
    
    // 2. Set the ID for tracking/debugging
    .SetSourceId(new SemanticKey("CustomExecuteLogic"))
    
    // 3. Define the core logic and calculation type
    .SetLogic(
        logicType: new SemanticKey("Execute"), 
        modType: ModifierType.Additive, 
        priority: 100 // Higher priority calculates later
    )
    
    // 4. Inject exact arguments expected by your custom logic
    .AddAttributeArg(new SemanticKey("MaxHealth")) // Arg 0: Reads Max Health
    .AddConstantArg(0.2f)                          // Arg 1: 20% threshold threshold
    .Build();

```

## 3. Inline Composition (StatBlock Integration)

The true power of the `ModifierBuilder` shines when used inline inside the `StatBlockBuilder`. You don't need to instantiate standalone spec variables; you can declare them directly inside the StatBlock using an `Action<ModifierBuilder>`.

### Example: Building a Complete Weapon Profile

```
// Define a StatBlock for a heavy sword
StatBlock heavySwordBuff = StatBlockBuilder.Create("HeavySwordStats")
    
    // Base Damage Modifier (+25 Flat)
    .AddModifier(mod => mod
        .MakeFlat(new SemanticKey("Damage"), 25f)
        .SetSourceId(new SemanticKey("BaseWeaponDamage"))
    )
    
    // Strength Scaling Modifier (+ 2.0 * Owner's Strength)
    .AddModifier(mod => mod
        .MakeLinearScaling(
            targetAttr: new SemanticKey("Damage"), 
            sourceAttr: new SemanticKey("Strength"), 
            multiplier: 2.0f, 
            sourcePath: new SemanticKey("Owner")
        )
        .SetSourceId(new SemanticKey("HeavyStrScaling"))
    )
    
    // Weight Penalty (-10% Speed)
    .AddModifier(mod => mod
        .MakeMultiplier(new SemanticKey("Speed"), -0.1f)
        .SetSourceId(new SemanticKey("HeavyWeaponPenalty"))
    )
    
    .Build();

```

This syntax makes creating massive databases of items, spells, and character classes incredibly fast, readable, and perfectly safe for JSON serialization.
