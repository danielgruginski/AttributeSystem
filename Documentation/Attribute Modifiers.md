# Attribute Modifiers Documentation

## Overview

Modifiers are the logic units of the Attribute System. While an `Attribute` holds the value, an **Attribute Modifier** describes _how_ that value changes (e.g., "+10 Flat", "+50% Multiplier", "Clamp between 0 and 100").

The system uses a **Unified Argument Architecture**, meaning every modifier—regardless of its math—accepts a standardized list of inputs (`ValueSource`s). This allows any parameter of a formula (like the "10" in "+10 Damage") to be either a hardcoded constant OR a dynamic reference to another attribute (e.g., "Owner.Strength").

## Core Interface: `IAttributeModifier`

Every modifier implements this lightweight interface:

```
public interface IAttributeModifier
{
    // Defines how this modifier merges with the previous value 
    // (Additive, Multiplicative, or Override)
    ModifierType Type { get; }

    // Determines calculation order (Lower = Earlier)
    int Priority { get; }

    // The reactive stream of the modifier's value.
    // This allows the modifier to update dynamically if its inputs change.
    IObservable<float> GetMagnitude(AttributeProcessor processor);
}

```

## Standard Implementations

The system comes with several robust implementations to cover most RPG/Game needs without requiring custom code.

### 1. `LinearModifier`

The workhorse of the system.

-   **Formula:** `(Input * Coefficient) + Addend`
    
-   **Arguments:**
    
    1.  `Input` (The main value, usually a dynamic attribute)
        
    2.  `Coefficient` (Multiplier, default 1)
        
    3.  `Addend` (Flat bonus, default 0)
        
-   **Usage:**
    
    -   _Flat Bonus:_ Input=10, Coeff=1, Addend=0 -> Result 10.
        
    -   _Scaling:_ Input=Strength, Coeff=2.5 -> Result Strength * 2.5.
        

### 2. `PolynomialModifier`

Used for non-linear scaling (quadratic curves, etc.).

-   **Formula:** `(Input ^ Power) * Scale + Flat`
    
-   **Arguments:**
    
    1.  `Input`
        
    2.  `Power` (Exponent)
        
    3.  `Scale`
        
    4.  `Flat`
        

### 3. `FunctionalModifier` (The Swiss Army Knife)

A generic wrapper that executes a specific math function (often wrapping `Mathf`).

-   **Usage:** Used for specific logic defined in the `ModifierFactory`.
    
-   **Examples:**
    
    -   `Clamp`: Args [Input, Min, Max]
        
    -   `Min` / `Max`: Args [ValA, ValB]
        
    -   `Step`: Returns 1 if Input >= Threshold, else 0.
        

## The Modifier Factory

To support data-driven design (JSON StatBlocks), the system uses a `ModifierFactory` to map string IDs (SemanticKeys) to specific modifier classes.

### Registration

You can register new logic types in your startup code:

```
var factory = new ModifierFactory();

// Register a custom "DistanceBonus" logic
factory.Register("DistanceBonus", args => new FunctionalModifier(args, vals => {
    // Custom logic: Bonus based on distance to target
    float dist = vals[0];
    return dist > 10 ? 0 : (10 - dist) * 2; // +2 damage per meter closer than 10m
}), "DistanceToTarget");

```

### Argument Metadata

The factory also allows defining parameter names for the Unity Editor. This ensures that when a designer selects "Clamp" in the Inspector, the input fields are labeled "Input", "Min", and "Max" instead of "Arg 0", "Arg 1", "Arg 2".

## Unified `ModifierArgs`

All modifiers are constructed using the `ModifierArgs` struct. This passes the `ValueSource` list safely.

```
public struct ModifierArgs
{
    public string SourceId;
    public ModifierType Type;
    public int Priority;
    public List<ValueSource> Arguments;
    
    // Helper to safely get arguments or default to 0
    public ValueSource GetSafe(int index, float defaultVal = 0f);
}

```

## Creating a Custom Modifier

If `FunctionalModifier` isn't enough (e.g., you need complex state or external physics queries), you can implement `IAttributeModifier` directly or inherit from `ParametricAttributeModifier`.

```
public class DayNightModifier : IAttributeModifier
{
    public ModifierType Type => ModifierType.Multiplicative;
    public int Priority => 100;
    public string SourceId => "DayNightCycle";

    public IObservable<float> GetMagnitude(AttributeProcessor processor)
    {
        // Example: 1.5x damage during the day, 0.8x at night
        // Assuming there's a global reactive "IsDaytime" property
        return WorldClock.IsDaytime.Select(isDay => isDay ? 1.5f : 0.8f);
    }
}

```
