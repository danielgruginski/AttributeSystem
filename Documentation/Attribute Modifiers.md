# Attribute Modifiers Documentation

## Overview

An `Attribute` holds a value; **modifiers** change it: "+10", "x1.5", "set to 0", "at most MaxHealth". An attribute applies its modifiers one after another, like a stack of operations: each takes the value the previous ones produced and applies its own operation once. So two "+10%" multipliers compound (100 x 1.1 x 1.1 = 121), and a clamp placed last keeps the result in range.

Every modifier has:

-   a **Type**: what its value does to the attribute (add, multiply, override, clamp);
    
-   a **Priority**: when it applies;
    
-   a **value** that can change over time, e.g. when it reads another attribute.
    

In StatBlocks, a modifier's value comes from a **logic** class, such as `LinearLogic` (`Input * Coefficient + Addend`); see [Modifier Logic](Modifier%20Logic.md). The examples on this page assume `using ReactiveSolutions.AttributeSystem.Core;` and `using ReactiveSolutions.AttributeSystem.Core.Modifiers;`, and keys such as `Stats.Damage` from classes generated from KeyDomains (see [Semantic Keys](Semantic%20Keys.md)).

## Core Interface: `IAttributeModifier`

Every modifier implements this lightweight interface (namespace `ReactiveSolutions.AttributeSystem.Core`):

```csharp
public interface IAttributeModifier
{
    // What the value does to the attribute (see ModifierType).
    ModifierType Type { get; }

    // Determines calculation order (Lower = Earlier).
    // Ties: Additive, Multiplicative, Override, ClampMin, ClampMax, then insertion order.
    int Priority { get; }

    // Where the modifier comes from (e.g. "IronSword"). Shown by the Attribute Debugger.
    string SourceId { get; }

    // The reactive stream of the modifier's value.
    // This allows the modifier to update dynamically if its inputs change.
    // 'processor' is the Entity that owns the attribute being modified.
    IObservable<float> GetMagnitude(Entity processor);
}

```

## Modifier Types and Order

```csharp
public enum ModifierType
{
    Additive,       // Adds the value
    Multiplicative, // Multiplies by the value (each multiplier applies on its own: +10% and +10% make x1.21)
    Override,       // Replaces the value
    ClampMin,       // Keeps the value at least this high
    ClampMax        // Keeps the value at most this high (e.g. Health at most MaxHealth)
}

```

An attribute applies its modifiers ordered by **`Priority` (ascending), then `Type` (in the order above), then insertion order**. At equal priority, multipliers therefore scale the base plus the additives, an override replaces that result, and the clamps limit whatever comes out. A lower `Priority` runs first regardless of type: an Additive modifier with a higher priority than a multiplier is added after the multiplication, and a modifier with a higher priority than a clamp can take the value past its limit.

For example, with Health at a base of 80 and MaxHealth at 100:

| Modifiers (all Priority 0) | Health |
| ----- | ----- |
| `+20` (Additive), `x1.5` (Multiplicative) | (80 + 20) x 1.5 = 150 |
| the same, plus MaxHealth as a Clamp Max | min(150, 100) = 100 |
| the same, with the base dropped to 40 | min((40 + 20) x 1.5, 100) = 90 |

A clamp only limits the value: when the value drops below the limit again, the clamp has no effect. For current Health that is damaged and healed, use a [pool](Resource%20Pools.md) instead: it keeps the amount itself between 0 and MaxHealth, so healing past the maximum is lost rather than stored.

-   `Type` and `Priority` are read when the modifier is added, to place it in the pipeline; its position doesn't change afterwards.
    
-   A modifier whose magnitude hasn't emitted yet contributes nothing.
    
-   A modifier that reads the attribute it modifies sees that attribute's _final_ value, which includes the modifier's own effect. "+10% of MaxHealth" as a Linear modifier on MaxHealth that reads MaxHealth (Coefficient 0.1) settles at 111.1 for a base of 100 (x = 100 + 0.1x). To add 10% once, use a Multiplicative modifier of 1.1 (100 x 1.1 = 110). See [Circular Dependencies and Known Limitations](Attribute.md#circular-dependencies-and-known-limitations).
    

## LogicModifier

`LogicModifier` is the modifier that StatBlocks create: it takes its value from a logic object. Use it to apply logic from code:

```csharp
var entity = new Entity();
entity.SetOrUpdateBaseValue(Stats.Damage, 10f);
entity.SetOrUpdateBaseValue(Stats.Strength, 8f);

// Damage += Strength * 0.5   (10 + 4 = 14)
IDisposable scaling = entity.AddModifier("Scaling",
    new LogicModifier(new LinearLogic { Input = ValueSource.FromAttribute(Stats.Strength), Coefficient = 0.5f }),
    Stats.Damage);

// Health can't go above MaxHealth. Priority 1000 applies it after everything at lower priorities.
entity.AddModifier("HealthCap",
    new LogicModifier(new ValueLogic(ValueSource.FromAttribute(Stats.MaxHealth)), ModifierType.ClampMax, priority: 1000),
    Stats.Health);

scaling.Dispose(); // Removes the modifier: Damage is 10 again
```

The constructor is `LogicModifier(ModifierLogic logic, ModifierType type = ModifierType.Additive, int priority = 0, string sourceId = null, Entity context = null)`. `context` is the entity the logic reads its attribute inputs from; leave it `null` to read them from the entity whose attribute is modified. (StatBlocks pass the entity they are applied to.)

## Other Implementations

-   **`FunctionalAttributeModifier`** (namespace `ReactiveSolutions.AttributeSystem.Core`): passes a single `ValueSource` through a lambda. It can't be saved in a StatBlock, since a lambda isn't data. Constructor: `FunctionalAttributeModifier(string sourceId, ValueSource source, Func<float, float> operation, ModifierType type = ModifierType.Additive, int priority = 0)`.
    

## Writing a Modifier From Scratch

For anything a logic class can't express, implement `IAttributeModifier` directly. A logic class is usually simpler, and it also works in StatBlocks and the Inspector (see [Modifier Logic](Modifier%20Logic.md#writing-your-own-logic)).

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

Apply it like any other modifier; disposing the returned handle removes it:

```csharp
var entity = new Entity();
var handle = entity.AddModifier("DayNightCycle", new DayNightModifier(), Stats.Damage);
```
