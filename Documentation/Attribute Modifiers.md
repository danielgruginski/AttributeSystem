# Attribute Modifiers Documentation

## Overview

Modifiers are the logic units of the Attribute System. While an `Attribute` holds the value, an **Attribute Modifier** describes _how_ that value changes (e.g., "+10 Flat", "+50% Multiplier", "Clamp between 0 and 100").

The system uses a **Unified Argument Architecture**, meaning every modifier—regardless of its math—accepts a standardized list of inputs (`ValueSource`s). This allows any parameter of a formula (like the "10" in "+10 Damage") to be either a hardcoded constant OR a dynamic reference to another attribute (e.g., "Owner.Strength").

## Core Interface: `IAttributeModifier`

Every modifier implements this lightweight interface (namespace `ReactiveSolutions.AttributeSystem.Core`):

```csharp
public interface IAttributeModifier
{
    // Defines how this modifier merges with the previous value 
    // (Additive, Multiplicative, or Override)
    ModifierType Type { get; }

    // Determines calculation order (Lower = Earlier).
    // Ties: Additive, then Multiplicative, then Override, then insertion order.
    int Priority { get; }

    // Where the modifier comes from (e.g. "IronSword"). Shown by the Attribute Debugger.
    string SourceId { get; }

    // The reactive stream of the modifier's value.
    // This allows the modifier to update dynamically if its inputs change.
    // 'processor' is the Entity that owns the attribute being modified.
    IObservable<float> GetMagnitude(Entity processor);
}

```

`ModifierType` has three values:

```csharp
public enum ModifierType
{
    Additive,       // Added to the base value or other additives
    Multiplicative, // Multiplies the sum of base + additives
    Override        // Replaces the value (Highest priority wins)
}

```

An attribute applies its modifiers ordered by **`Priority` (ascending), then `Type` (Additive, then Multiplicative, then Override), then insertion order**. The comments above therefore hold at equal priority: a multiplier scales base + additives, and the last override applied wins (the highest `Priority`, or the most recently added one at equal priority). A lower `Priority` runs first regardless of type, so an Additive modifier with a higher priority than a multiplier is added after the multiplication, and modifiers with a higher priority than an override still apply on top of it.

-   `Type` and `Priority` are read when the modifier is added, to place it in the pipeline; its position doesn't change afterwards.
    
-   A modifier whose magnitude hasn't emitted yet contributes nothing.
    
-   A modifier that reads the attribute it modifies sees that attribute's _final_ value (see [Circular Dependencies and Known Limitations](Attribute.md#circular-dependencies-and-known-limitations)).
    

## Standard Implementations

The system comes with several robust implementations to cover most RPG/Game needs without requiring custom code. They live in the `ReactiveSolutions.AttributeSystem.Core.Modifiers` namespace (except `FunctionalAttributeModifier`).

### 1. `LinearModifier`

The workhorse of the system.

-   **Formula:** `(Input * Coefficient) + Addend`
    
-   **Arguments:**
    
    1.  `Input` (The main value, usually a dynamic attribute)
        
    2.  `Coefficient` (Multiplier, 1 if the argument is omitted)
        
    3.  `Addend` (Flat bonus, 0 if the argument is omitted)
        
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

A generic wrapper that executes a specific math function (often wrapping `Mathf`): `new FunctionalModifier(AttributeModifierSpec spec, Func<IList<float>, float> operation)`. The operation receives the current argument values, in order.

-   **Usage:** Used for specific logic defined in the `ModifierFactory`.
    
-   **Examples:**
    
    -   `Clamp`: Args [Input, Min, Max]
        
    -   `Min` / `Max`: Args [Value A, Value B]
        
    -   `Step`: Args [Edge Threshold, Input Value]. Returns 1 if Input Value >= Edge Threshold, else 0.
        
    -   See [ModifierFactory](ModifierFactory.md) for the full list (`Floor`, `Ratio`, `Exponential`, `DiminishingReturns`, `ScaledTriangular`).
        

### 4. `StaticAttributeModifier`

Returns its first argument (`Value`) unchanged, e.g. a flat `+10` (Additive) or a fixed `x1.5` (Multiplicative). It backs the `Static` logic type and is the factory's fallback for unknown logic types.

### Other Implementations

-   **`FunctionalAttributeModifier`** (namespace `ReactiveSolutions.AttributeSystem.Core`): passes a single `ValueSource` through a lambda. Constructor: `FunctionalAttributeModifier(string sourceId, ValueSource source, Func<float, float> operation, ModifierType type = ModifierType.Additive, int priority = 0)`.
    
-   **`SegmentedMultiplierAttributeModifier`**: returns the multiplier of the highest threshold its source value reaches (tiered "breakpoints"), or a default multiplier. It is configured through its serialized fields only and is not registered in the factory.
    

## The Modifier Factory

To support data-driven design (JSON StatBlocks), the system uses a `ModifierFactory` to map logic types to specific modifier classes. A spec names its logic type with a SemanticKey, such as the package's generated `sk.Modifiers.Linear`, and the factory looks it up by the key's string value (`"Linear"`). An unknown logic type logs a warning and falls back to `Static`. See [ModifierFactory](ModifierFactory.md).

### Registration

You can register new logic types in your startup code:

```csharp
using ReactiveSolutions.AttributeSystem.Core;           // ModifierFactory
using ReactiveSolutions.AttributeSystem.Core.Modifiers; // FunctionalModifier

var factory = new ModifierFactory();

// Register a custom "DistanceBonus" logic
factory.Register("DistanceBonus", spec => new FunctionalModifier(spec, vals => {
    // Custom logic: Bonus based on distance to target
    float dist = vals[0];
    return dist > 10 ? 0 : (10 - dist) * 2; // +2 damage per meter closer than 10m
}), "DistanceToTarget");

```

The builder receives the `AttributeModifierSpec`. A spec selects this logic when its `LogicType` is a key named `DistanceBonus`, e.g. one you add to your own KeyDomain (see [Semantic Keys](Semantic%20Keys.md)). Builders belong to the factory instance they are registered on, so pass that factory wherever StatBlocks are applied (see [ModifierFactory](ModifierFactory.md)).

### Argument Metadata

The factory also allows defining parameter names for the Unity Editor. This ensures that when a designer selects "Clamp" in the Inspector, the input fields are labeled "Input", "Min", and "Max" instead of generic labels. The built-in names are available statically (`ModifierFactory.TryGetParameterNames`), so the editor knows them without creating a factory.

## Unified Arguments: `AttributeModifierSpec`

All built-in modifiers are constructed from an `AttributeModifierSpec`, the same serializable class that StatBlocks store (it replaces the old `ModifierArgs` struct). Factory builders receive it too. This passes the `ValueSource` list safely.

```csharp
[Serializable]
public class AttributeModifierSpec
{
    public SemanticKey TargetAttribute;  // The attribute a StatBlock applies it to
    public List<SemanticKey> TargetPath; // Provider path to a remote target (empty = local)
    public string SourceId;
    public ModifierType Type = ModifierType.Additive;
    public int Priority = 0;
    public SemanticKey LogicType;        // e.g. sk.Modifiers.Linear
    public List<ValueSource> Arguments;
    
    // Helper to safely get arguments or default to a Constant (0 unless specified)
    public ValueSource GetSafe(int index, float defaultConstant = 0f);

    // Logs a warning and returns false if fewer than requiredCount arguments are defined
    public bool ValidateArgCount(int requiredCount, string logicType);
}

```

## Creating a Custom Modifier

If `FunctionalModifier` isn't enough (e.g., you need complex state or external physics queries), you can implement `IAttributeModifier` directly or inherit from `ParametricAttributeModifier` (its constructor takes `sourceId, type, priority, arguments`; override `protected float Calculate(IList<float> args)` and it resolves the `ValueSource` arguments for you).

```csharp
using System;
using ReactiveSolutions.AttributeSystem.Core;
using UniRx;

public class DayNightModifier : IAttributeModifier
{
    public ModifierType Type => ModifierType.Multiplicative;
    public int Priority => 100;
    public string SourceId => "DayNightCycle";

    public IObservable<float> GetMagnitude(Entity processor)
    {
        // Example: 1.5x damage during the day, 0.8x at night
        // Assuming there's a global reactive "IsDaytime" property
        return WorldClock.IsDaytime.Select(isDay => isDay ? 1.5f : 0.8f);
    }
}

```

Apply it like any other modifier; disposing the returned handle removes it (`Stats.Damage` is a key from a generated KeyDomain class, see [Semantic Keys](Semantic%20Keys.md)):

```csharp
var entity = new Entity();
var handle = entity.AddModifier("DayNightCycle", new DayNightModifier(), Stats.Damage);
```
