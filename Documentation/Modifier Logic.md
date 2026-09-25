# Modifier Logic Documentation

## Overview

Every modifier in a StatBlock (an `AttributeModifierSpec`) has a **Logic**: a small serializable class that computes the modifier's value. The modifier's **Type** then decides what the value does to the attribute (add to it, multiply it, replace it, clamp it) and its **Priority** decides when; see [Attribute Modifiers](Attribute%20Modifiers.md).

-   **Nothing to register.** Every `[Serializable]` class that derives from `ModifierLogic` and has a parameterless constructor appears in the **Logic** dropdown of the Inspector and the Stat Block Editor, and is saved with the StatBlock.
    
-   **Named inputs.** Each input is its own field (`Input`, `Coefficient`, ...), so there is no argument order to remember.
    
-   **Any settings.** Inputs are `ValueSource`s (a constant, an attribute's value, or a formula: another logic), and a logic class can also have numbers, text, enums, lists, curves, other logic and `[Serializable]` classes of your own (see [JSON Format](JSON%20Format.md#field-values) for how each is saved).
    

Namespace: `ReactiveSolutions.AttributeSystem.Core.Modifiers`. Keys such as `Stats.Damage` come from classes generated from KeyDomains (see [Semantic Keys](Semantic%20Keys.md)).

## Built-in Logic

| Logic | Fields | Value |
| ----- | ----- | ----- |
| `ValueLogic` | `Value` | The value itself: a constant (`+5`) or an attribute (`+ Strength`). The default for new modifiers. |
| `LinearLogic` | `Input`, `Coefficient` (1), `Addend` | `Input * Coefficient + Addend` |
| `PolynomialLogic` | `Input`, `Power` (1), `Scale` (1), `Flat` | `Input ^ Power * Scale + Flat` |
| `ClampLogic` | `Input`, `Min`, `Max` | `Input` limited to [Min, Max]. To limit the attribute itself (e.g. Health to MaxHealth), use a modifier of Type **Clamp Max** instead. |
| `MinLogic` / `MaxLogic` | `A`, `B` | The smaller / larger of A and B. |
| `FloorLogic` | `Input` | `Input` rounded down to a whole number. |
| `StepLogic` | `Input`, `Threshold` | 1 if `Input >= Threshold`, otherwise 0. |
| `RatioLogic` | `Dividend`, `Divisor` (1) | `Dividend / Divisor`, or `Dividend` if the divisor is (almost) 0. |
| `ExponentialLogic` | `Base` (1), `Exponent` (1) | `Base ^ Exponent` |
| `DiminishingReturnsLogic` | `Input`, `MaxBonus`, `SoftCap` | `MaxBonus * Input / (Input + SoftCap)`: approaches MaxBonus, half of it at `Input = SoftCap`. A negative input counts as 0, and the result is 0 when `Input + SoftCap <= 0`. |
| `ScaledTriangularLogic` | `Input`, `Scale` (1) | `Scale * 0.5 * (sqrt(1 + 8 * Input / Scale) - 1)`, never more than `Input`. A negative input counts as 0. |
| `SegmentedLogic` | `Input`, `Default` (1), `Segments` | Breakpoints: the `Value` of the highest `Threshold` that Input reaches, or `Default` below all of them. |
| `GroupTotalLogic` | `Group`, `Attribute`, `Operation` (Sum) | An attribute totaled over a link group's members: `Sum`, `Average`, `Min`, `Max`, or `Count` of the members. See [LinkGroup](LinkGroup.md#totals-over-a-group). |

All fields are `ValueSource`s except `SegmentedLogic`'s `Default` and `Segments`, and `GroupTotalLogic`'s. Defaults are in parentheses; the others default to 0. The dropdown shows the names without the "Logic" suffix (e.g. "Diminishing Returns").

An input can be a logic of its own, so the built-ins combine into bigger formulas. AttackPower x 100 / (Defense + 100) is a `RatioLogic` whose `Dividend` and `Divisor` are `LinearLogic`s. In the Inspector, set the input's mode to **Formula**; in code, assign the logic (`Divisor = new LinearLogic { ... }`). See [ValueSource](ValueSource.md#formulas).

## Writing Your Own Logic

For a formula over inputs, derive from `FormulaLogic`: list the `ValueSource` fields in `Inputs`, and compute the value from their current values. It is recomputed whenever one of them changes.

```csharp
using System;
using System.Collections.Generic;
using ReactiveSolutions.AttributeSystem.Core;
using ReactiveSolutions.AttributeSystem.Core.Modifiers;

// +2 per meter closer than MaxRange
[Serializable]
public class DistanceBonusLogic : FormulaLogic
{
    public ValueSource Distance = ValueSource.Const(0f);
    public float MaxRange = 10f;
    public float BonusPerMeter = 2f;

    protected override IEnumerable<ValueSource> Inputs => new[] { Distance };

    protected override float Compute(IList<float> inputs) =>
        inputs[0] >= MaxRange ? 0f : (MaxRange - inputs[0]) * BonusPerMeter;
}
```

-   Keep the `[Serializable]` attribute and a parameterless constructor, or the class can't be picked in the Inspector or saved.
    
-   For a value that doesn't come from inputs (e.g. from a game system), derive from `ModifierLogic` and implement `IObservable<float> Observe(Entity context)`: the value on subscribe, then every change. Attribute inputs should be read from `context`.
    
-   A logic object is shared data: a StatBlock applies the same object to every entity, so `Observe` must not change its fields.
    
-   `Clone()` copies the logic with its inputs, lists and nested formulas (the fields Unity saves). Override it, calling `base.Clone()`, if your logic holds other objects that each copy needs its own of.
    

Use your logic like the built-ins, in the Inspector or in code:

```csharp
var scope = StatBlockBuilder.Create("Sniper Scope")
    .AddModifier(Stats.Damage, new DistanceBonusLogic { Distance = ValueSource.FromAttribute(Stats.TargetDistance) })
    .Build();
```

To apply logic without a StatBlock, wrap it in a `LogicModifier` (see [Attribute Modifiers](Attribute%20Modifiers.md#logicmodifier)).

## Where Inputs Are Read

A StatBlock's modifiers read their attribute inputs from the entity the block is applied to, even when `TargetPath` sends the modifier to another entity's attribute. For example, a sword's StatBlock can have a modifier with target `Owner` / `Strength` whose input is the sword's own `Sharpness`. Give an input a provider path to read from elsewhere (e.g. `Owner` / `Strength`, read from the sword).

## Saving, and Renaming a Logic Class

In JSON files, a logic is named after its class, in camelCase and without "Logic", and so are its fields: `"distanceBonus": { "distance": "TargetDistance", "maxRange": 5 }`. Fields with their default value are left out. See [JSON Format](JSON%20Format.md#logic).

-   **Renaming a class or a field** changes its name in JSON files, and a file that names a logic or field that doesn't exist fails to load, with an error naming it. Rename it in the files too (the names are plain text, so search and replace works).
    
-   **Scenes and prefabs** (a StatBlock authored in the Inspector) store the logic with `[SerializeReference]`, by class name, namespace and assembly. After renaming or moving a class, add Unity's `[MovedFrom]` attribute (namespace `UnityEngine.Scripting.APIUpdating`) so they still find it:
    
    ```csharp
    [Serializable, MovedFrom(false, sourceClassName: "ProximityBonusLogic")]
    public class DistanceBonusLogic : FormulaLogic
    {
        ...
    }
    ```
    
    In the Inspector, a modifier whose logic class can't be found shows a warning. When its StatBlock is applied, it is skipped with a warning (`skipped a modifier on '...' with no Logic`).
    
-   **Code stripping:** IL2CPP builds can remove a class that only JSON files name. Mark your logic classes `[Preserve]` (namespace `UnityEngine.Scripting`), as the built-in ones are.
    
