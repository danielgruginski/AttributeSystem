# Attribute Class Documentation

## Overview

The `Attribute` class represents a single, reactive statistic (e.g., "Health", "MovementSpeed"). It is the leaf node of the attribute graph and the engine responsible for the actual mathematical calculation of the final value.

Unlike a simple `float` variable, an `Attribute` is a **Reactive Stream**. It maintains a `BaseValue` and a list of active `IAttributeModifier`s. Whenever the base value changes _or_ any of the modifiers change (e.g., a "Strength" modifier attached to this attribute changes value), the `Attribute` automatically recalculates and emits its new `CurrentValue`.

## Key Features

-   **Reactive Pipeline:** Uses `UniRx` to combine multiple data streams (Base Value + Modifiers) into a single output stream.
    
-   **Priority-Based Sorting:** Modifiers are strictly ordered by their `Priority` property before calculation, ensuring consistent math (e.g., flat bonuses applied before multipliers).
    
-   **Pipeline Rebuilding:** The calculation chain is dynamic. When a modifier is added or removed, the pipeline is torn down and rebuilt to accommodate the new topology.
    
-   **Mathematical Operations:** Supports standard RPG math operations:
    
    -   **Additive:** `Val += Mod`
        
    -   **Multiplicative:** `Val *= Mod`
        
    -   **Override:** `Val = Mod`
        

## Class Definition

```
public class Attribute : IDisposable

```

## Public API

### Properties

-   **`SemanticKey Name`**
    
    -   The unique identifier for this attribute.
        
-   **`float BaseValue`**
    
    -   The raw, unmodified value of the attribute. Setting this triggers a recalculation.
        
-   **`IReadOnlyReactiveProperty<float> ReactivePropertyAccess`**
    
    -   The main output stream. Subscribe to this to listen for the final calculated value.
        
    -   _Usage:_ `attribute.ReactivePropertyAccess.Subscribe(val => UpdateUI(val));`
        
-   **`IEnumerable<IAttributeModifier> Modifiers`**
    
    -   Read-only access to the currently active modifiers. Useful for inspection or debugging.
        

### Methods

-   **`SetBaseValue(float value)`**
    
    -   Updates the foundational value of the stat.
        
    -   _Example:_ Leveling up increases Base Strength from 10 to 12.
        
-   **`AddModifier(IAttributeModifier modifier)`**
    
    -   Injects a new modifier into the calculation list. Triggers a `RebuildCalculationChain`.
        
-   **`RemoveModifier(IAttributeModifier modifier)`**
    
    -   Removes an existing modifier instance. Triggers a `RebuildCalculationChain`.
        

## Internal Logic: The Calculation Pipeline

The core complexity of this class lies in `RebuildCalculationChain()`.

1.  **Trigger:** Called whenever the `_modifiers` collection changes (add/remove).
    
2.  **Stream Gathering:** It collects the `IObservable<float>` (Magnitude) from every active modifier.
    
3.  **CombineLatest:** It creates a single observable using `Observable.CombineLatest`. This ensures that if _any_ single modifier changes its value (e.g., a "Health Potion" ticks down, or "Owner.Strength" changes), the entire `Attribute` recalculates immediately.
    
4.  **Math Step (`CalculatePipeline`):**
    
    -   **Input:** The latest Base Value and the latest values from all Modifiers.
        
    -   **Sorting:** Modifiers are sorted by `Priority`.
        
    -   **Execution:** The loop iterates through sorted modifiers applying operations:
        
        ```
        foreach (var step in pipeline)
        {
            switch (step.Modifier.Type)
            {
                case ModifierType.Additive: result += step.Val; break;
                case ModifierType.Multiplicative: result *= step.Val; break;
                case ModifierType.Override: result = step.Val; break;
            }
        }
        
        ```
        

## Usage Example

While `Attribute` is rarely instantiated directly (the `AttributeProcessor` handles that), understanding its direct usage is useful for debugging.

```
// 1. Creation
var health = new Attribute(new SemanticKey("Health"), 100f, processor);

// 2. Subscription
health.ReactivePropertyAccess.Subscribe(val => Debug.Log($"Health is: {val}")); 
// Logs: "Health is: 100"

// 3. Modification
health.SetBaseValue(150f);
// Logs: "Health is: 150"

// 4. Adding a Modifier (e.g. +10 Flat Bonus)
var bonus = new StaticAttributeModifier(... value=10 ...);
health.AddModifier(bonus);
// Logs: "Health is: 160"

```
