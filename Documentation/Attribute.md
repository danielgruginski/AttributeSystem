# Attribute Class Documentation

## Overview

The `Attribute` class represents a single, reactive statistic (e.g., "Health", "MovementSpeed"). It is the leaf node of the attribute graph and the engine responsible for the actual mathematical calculation of the final value.

Unlike a simple `float` variable, an `Attribute` is a **Reactive Stream**. It maintains a `BaseValue` and a list of active `IAttributeModifier`s. Whenever the base value changes _or_ any of the modifiers change (e.g., a "Strength" modifier attached to this attribute changes value), the `Attribute` automatically recalculates and emits its new final value through `ObservableValue`.

## Key Features

-   **Reactive Pipeline:** Uses `UniRx` to combine multiple data streams (Base Value + Modifiers) into a single output stream.
    
-   **Priority-Based Sorting:** Modifiers are strictly ordered before calculation, ensuring consistent math: by `Priority` (ascending, lower runs first), then by `Type` (Additive, Multiplicative, Override, Clamp Min, Clamp Max), then by the order they were added. At equal priority, flat bonuses are therefore applied before multipliers, and clamps last, no matter which was added first.
    
-   **Incremental Updates:** Each modifier is subscribed once, when it is added. Adding or removing a modifier only inserts or removes that modifier, and a base-value change only recalculates; nothing is torn down or re-subscribed.
    
-   **Mathematical Operations:** Supports standard RPG math operations:
    
    -   **Additive:** `Val += Mod`
        
    -   **Multiplicative:** `Val *= Mod`
        
    -   **Override:** `Val = Mod`
        
    -   **Clamp Min:** `Val = Max(Val, Mod)` (at least Mod)
        
    -   **Clamp Max:** `Val = Min(Val, Mod)` (at most Mod, e.g. Health at most MaxHealth)
        
-   **Missing Inputs Read as 0:** A modifier argument or pointer target that names an attribute or provider that doesn't exist (yet) reads as 0 instead of blocking the pipeline, and the real value is picked up as soon as it appears.
    
-   **Circular Dependency Guard:** A calculation that keeps re-triggering itself is stopped and reported instead of overflowing the stack (see [Circular Dependencies and Known Limitations](#circular-dependencies-and-known-limitations)).
    

## Class Definition

```csharp
// namespace ReactiveSolutions.AttributeSystem.Core
public class Attribute : IAttribute // IAttribute extends IDisposable

```

## Public API

### Properties

-   **`SemanticKey Name`**
    
    -   The unique identifier for this attribute (e.g. `Stats.Health`).
        
-   **`float BaseValue`**
    
    -   The raw, unmodified value of the attribute. Read-only: change it with `SetBaseValue`, which triggers a recalculation. While a pointer is active, the base value is ignored (see [Attribute Pointers](Attribute%20Pointers.md)).
        
-   **`IReadOnlyReactiveProperty<float> ObservableValue`**
    
    -   The main output stream. Subscribe to this to listen for the final calculated value (it emits the current value on subscription, then whenever the value changes), or read `.Value` for the current final value.
        
    -   _Usage:_ `attribute.ObservableValue.Subscribe(val => UpdateUI(val));`
        
-   **`IEnumerable<IAttributeModifier> Modifiers`**
    
    -   Read-only access to the currently applied modifiers, in the order they are evaluated. Useful for inspection or debugging.
        
-   **`AttributeReference? ActivePointerTarget`**
    
    -   The target of the active (topmost) pointer, or `null` if the attribute isn't pointing anywhere.
        
-   **`bool IsDisposed`**
    
    -   `true` once the attribute has been disposed.
        

### Methods

-   **`SetBaseValue(float value)`**
    
    -   Updates the foundational value of the stat.
        
    -   _Example:_ Leveling up increases Base Strength from 10 to 12.
        
-   **`IDisposable AddModifier(IAttributeModifier modifier)`**
    
    -   Inserts a new modifier into the calculation list at its sorted position and subscribes to its magnitude. The modifier's `Type` and `Priority` are read here to place it; its position doesn't change afterwards.
        
    -   Disposing the returned handle removes exactly this application of the modifier.
        
-   **`RemoveModifier(IAttributeModifier modifier)`**
    
    -   Removes an existing modifier instance (the earliest application, if the same instance was added more than once).
        
-   **`IDisposable AddPointer(SemanticKey targetName, List<SemanticKey> path = null)`**
    
    -   Pushes a pointer onto this attribute's pointer stack; disposing the handle removes it. Usually called through `Entity.SetPointer`, which also rejects self and circular pointers. See [Attribute Pointers](Attribute%20Pointers.md).
        
-   **`Dispose()`**
    
    -   Stops the pipeline and releases its subscriptions, including those to other entities. The attribute keeps its last value but no longer updates, and further `SetBaseValue`/`AddModifier` calls are ignored. `Entity.Dispose()` disposes all of the entity's attributes.
        

## Internal Logic: The Calculation Pipeline

The core of this class is `Recalculate()`, which runs `CalculatePipeline()` whenever one of the attribute's inputs emits.

1.  **Source:** The pipeline starts from the base value or, if a pointer is active, from the pointer target's final value (a missing target attribute or provider reads as 0).
    
2.  **One Subscription per Modifier:** `AddModifier` inserts the modifier into a list kept in evaluation order and subscribes once to its `GetMagnitude(entity)`, where `entity` is the `Entity` that owns this attribute.
    
3.  **Reacting to Changes:** Each emission stores that modifier's latest magnitude and recalculates. This ensures that if _any_ single modifier changes its value (e.g., a "Health Potion" ticks down, or "Owner.Strength" changes), the `Attribute` recalculates immediately. Changing the base value recalculates too, without touching the modifier subscriptions.
    
4.  **Math Step (`CalculatePipeline`):**
    
    -   **Input:** The latest source value (base value or pointer target) and the latest magnitude of every modifier.
        
    -   **Sorting:** Done once, on insertion: `Priority` (ascending), then `Type` (Additive, Multiplicative, Override, Clamp Min, Clamp Max), then insertion order. For example, with a base of 10, a `+10` and a `x2` at the same priority give `(10 + 10) * 2 = 40` in either insertion order; if the `x2` has priority 0 and the `+10` has priority 10, the result is `(10 * 2) + 10 = 30`.
        
    -   **Execution:** The loop iterates through the sorted modifiers applying operations. A modifier whose magnitude hasn't emitted yet contributes nothing:
        
        ```csharp
        float result = _sourceValue; // Base value, or the active pointer target's value
        foreach (var slot in _slots) // Sorted by Priority, Type, insertion order
        {
            if (!slot.HasMagnitude) continue;
            switch (slot.Modifier.Type)
            {
                case ModifierType.Additive: result += slot.Magnitude; break;
                case ModifierType.Multiplicative: result *= slot.Magnitude; break;
                case ModifierType.Override: result = slot.Magnitude; break;
                case ModifierType.ClampMin: result = Math.Max(result, slot.Magnitude); break;
                case ModifierType.ClampMax: result = Math.Min(result, slot.Magnitude); break;
            }
        }
        
        ```
        
    -   **Output:** The result is written to `ObservableValue`, which notifies subscribers only when the value actually changes.
        

## Circular Dependencies and Known Limitations

-   **Circular dependencies:** If an attribute's final value feeds back into its own calculation (directly, or through modifiers and pointers on other attributes or entities), each update re-enters the calculation until the value settles. If it doesn't settle within `Attribute.MaxRecalculationDepth` (32) nested re-entries, the attribute stops and logs `[Attribute] Circular dependency on '<name>': ...` instead of overflowing the stack. A modifier on `MaxHealth` whose input is `MaxHealth` with a coefficient of 1 or more never settles; slowly converging loops can hit the limit too.
    
-   **Modifiers that read their own attribute see its _final_ value.** That is how a circular definition resolves: a "+10% of self" modifier (Linear on `MaxHealth` with Input = `MaxHealth`, Coefficient = 0.1) settles at the fixed point 111.1 for a base of 100 (`100 + 0.1 * 111.1`). To add 10% once, use a `Multiplicative` modifier of 1.1 (it scales the running value: 110).
    
-   **Clamp with the clamp types, not with a self-reference.** An Override whose value clamps the attribute's own value (`Health = Clamp(Health, 0, MaxHealth)`) is a self-reference and latches at the clamped value. A `ClampMax` modifier whose value is `MaxHealth` limits the running value instead, and lets it go down again.
    
-   **Intermediate values:** Updates are pushed one dependency at a time, so an attribute that depends on the same source through two paths can briefly emit an intermediate value before settling.
    

## Usage Example

While `Attribute` is rarely instantiated directly (the `Entity` handles that), understanding its direct usage is useful for debugging. `Stats.Health` comes from a static class generated from a KeyDomain; see [Semantic Keys](Semantic%20Keys.md).

```csharp
using Game.Constants; // The namespace of your generated key classes (Stats)
using ReactiveSolutions.AttributeSystem.Core;
using ReactiveSolutions.AttributeSystem.Core.Modifiers; // LogicModifier, ValueLogic
using UniRx;
using UnityEngine;

// 1. Creation (the entity creates the attribute and registers it under its key)
var entity = new Entity();
var health = entity.GetOrCreateAttribute(Stats.Health, 100f);

// 2. Subscription
health.ObservableValue.Subscribe(val => Debug.Log($"Health is: {val}"));
// Logs: "Health is: 100"

// 3. Modification
health.SetBaseValue(150f);
// Logs: "Health is: 150"

// 4. Adding a Modifier (e.g. +10 Flat Bonus)
var bonus = new LogicModifier(new ValueLogic(10f), ModifierType.Additive, sourceId: "FlatBonus");
var handle = health.AddModifier(bonus);
// Logs: "Health is: 160"

// 5. Removing it
handle.Dispose();
// Logs: "Health is: 150"

```

The constructor `new Attribute(Stats.Health, 100f, entity)` also works, but that attribute isn't registered in the entity, so nothing can find it by key. In a file that also has `using System;`, writing the type name `Attribute` is ambiguous with `System.Attribute`: declare variables with `var` (as above), or add the alias `using Attribute = ReactiveSolutions.AttributeSystem.Core.Attribute;`.
