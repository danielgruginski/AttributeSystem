# ModifierFactory Documentation

## Overview

The `ModifierFactory` is the central registry for modifier logic in the Reactive Attribute System. It serves as the bridge between data-driven definitions (like JSON StatBlocks or Editor specifications) and actual C# modifier instances.

Its primary responsibilities are:

1.  **Instantiation:** Creating concrete `IAttributeModifier` instances from abstract IDs (logic types: SemanticKeys such as `sk.Modifiers.Linear`, matched by their string value).
    
2.  **Metadata Management:** Storing parameter names (e.g., "Input", "Scale") so the Unity Editor can display user-friendly labels instead of generic "Arg 0", "Arg 1".
    
3.  **Extensibility:** Allowing developers to register custom modifier logic without modifying the core system source code.
    

## Key Features

-   **Registry-Based:** Maps a unique `string` ID (Logic Type) to a `ModifierBuilder` delegate. The ID is the logic type key's string value, so `sk.Modifiers.Linear` and `"Linear"` are the same registration (see [Logic Types Are Matched by Name](#logic-types-are-matched-by-name)).
    
-   **Unified Argument Passing:** All builders receive the `AttributeModifierSpec` (`delegate IAttributeModifier ModifierBuilder(AttributeModifierSpec spec)`), ensuring a consistent API for accessing input values.
    
-   **Editor Integration:** Provides the static `GetParameterNames(id)` and `TryGetParameterNames(id, out names)`. The custom `AttributeModifierSpecDrawer` and the Stat Block Editor use `TryGetParameterNames` to label arguments in the Inspector UI. The built-in names are registered statically, so they are available without creating a factory.
    

## Class Definition

```csharp
// namespace ReactiveSolutions.AttributeSystem.Core
public class ModifierFactory : IModifierFactory
{
    // ... implementation ...
}

```

## Public API

### 1. Creation

-   **`IAttributeModifier Create(string id, AttributeModifierSpec spec)`**
    
    -   Creates a modifier instance using the registered builder for `id`, passing it `spec` as-is (no copying or context baking). A SemanticKey can be passed as `id`; it converts to its string value.
        
    -   **Fallback:** If `id` is unknown, it returns a `StaticAttributeModifier` built from the same spec to prevent crashes (Null Object Pattern) and logs `[ModifierFactory] Unknown modifier logic type '<id>'. Falling back to Static.` An empty `id` falls back to Static without a warning. Note that the fallback still applies the spec's first argument, with the spec's `Type` and `Priority`.
        
-   **`IAttributeModifier Create(AttributeModifierSpec spec, Entity context = null)`**
    
    -   A higher-level helper that parses a data specification (`AttributeModifierSpec`) and creates the modifier for its `LogicType`, with the same fallback. This is what StatBlocks use. (`context` is optional on `ModifierFactory`; through `IModifierFactory` it must be passed.)
        
    -   **Context Baking:** If a `context` entity is provided, it "bakes" it into any Attribute-based `ValueSource` arguments, so they resolve relative to `context` even when the modifier is applied to another entity's attribute (see [ValueSource](ValueSource.md)).
        
    -   The spec is usually shared asset data, so it is **never modified**: each argument is copied (`ValueSource.Clone()`; a `null` entry becomes `Const(0)`), the context is baked into the copies, and the builder receives a per-application copy of the spec. Logs a warning if the spec has no arguments.
        

### 2. Registration

-   **`void Register(string id, ModifierBuilder builder, params string[] paramNames)`**
    
    -   Registers a new logic type. Registering an existing `id` replaces it and logs `[ModifierFactory] Overwriting modifier: <id>`.
        
    -   **`id`**: The unique key (e.g., "Linear", "MyCustomLogic"). A SemanticKey works too; it is registered under its string value.
        
    -   **`builder`**: A lambda or function that takes the `AttributeModifierSpec` and returns `IAttributeModifier`.
        
    -   **`paramNames`**: An array of strings defining the labels for the arguments in the Inspector.
        
    -   The builder is registered on **this factory instance** only. The parameter names are stored statically, shared by every factory and by the editor.
        
-   **`void Register(string id, ModifierBuilder builder)`**
    
    -   The `IModifierFactory` overload. The parameter names default to `"Value"`.
        

### 3. Metadata Access

-   **`IEnumerable<string> GetAvailableTypes()`**
    
    -   Returns a list of all logic IDs registered on this factory (the built-ins plus your registrations).
        
-   **`static string[] GetParameterNames(string id)`**
    
    -   Returns the argument labels for a given logic type, or `{ "Value" }` if it is unknown. Used primarily by Editor scripts.
        
-   **`static bool TryGetParameterNames(string id, out string[] names)`**
    
    -   Returns `false` for a logic type that isn't known in this session (e.g. a custom type that is only registered at runtime). The editors use it to label arguments, and to leave the arguments of unknown logic types untouched.
        
-   **`static IEnumerable<string> GetAllKeys()`**
    
    -   Every logic type that has parameter names: the built-ins plus anything registered so far in this session.
        

## Logic Types Are Matched by Name

The registry is keyed by **string**: the SemanticKey's value. `Register(sk.Modifiers.Linear, ...)`, `Register("Linear", ...)` and a spec with `LogicType = sk.Modifiers.Linear` all mean `"Linear"`. As a result:

-   Any key with a matching value selects the builder, even one from another KeyDomain (with a different GUID). For example, a key named `Linear` in your own KeyDomain picks the built-in Linear logic.
    
-   Lookups use the key's cached value. When a key is renamed in its KeyDomain, existing data keeps the old value until **Update All References** is run (the button on the KeyDomain asset, or Tools > SemanticKeys > Update All References); the updater scans assets and loaded scenes, not StatBlock JSON files. Whenever the value a spec holds differs from the name its builder is registered under, the factory warns and falls back to Static. So keep the names of the built-in logic types, and register custom builders under their key's current name. See [Semantic Keys](Semantic%20Keys.md).
    

## Default Registered Types

The factory comes pre-loaded with these standard modifiers:

| ID | Class | Arguments | Description |
| ----- | ----- | ----- | ----- |
| `Static` | `StaticAttributeModifier` | `Value` | Returns a constant value or attribute value. |
| `Linear` | `LinearModifier` | `Input`, `Coefficient`, `Addend` | `(Input * Coeff) + Addend` |
| `Polynomial` | `PolynomialModifier` | `Input`, `Power`, `Scale`, `Flat` | `(Input ^ Power) * Scale + Flat` |
| `Clamp` | `FunctionalModifier` | `Input`, `Min`, `Max` | Clamps input between min and max. |
| `Min` | `FunctionalModifier` | `Value A`, `Value B` | Returns the smaller value. |
| `Max` | `FunctionalModifier` | `Value A`, `Value B` | Returns the larger value. |
| `Floor` | `FunctionalModifier` | `Input` | Rounds input down to a whole number. |
| `Step` | `FunctionalModifier` | `Edge Threshold`, `Input Value` | Returns 1 if Input >= Edge, else 0. |
| `Ratio` | `FunctionalModifier` | `Dividend`, `Divisor` | `Dividend / Divisor`; returns `Dividend` if the divisor is (almost) 0. |
| `Exponential` | `FunctionalModifier` | `Exponent`, `Base` | `Base ^ Exponent` (note the argument order). |
| `DiminishingReturns` | `FunctionalModifier` | `Input`, `Max Bonus`, `Soft Cap` | `Max Bonus * (Input / (Input + Soft Cap))`; a negative input counts as 0, and the result is 0 when `Input + Soft Cap` ≤ 0 (e.g. unset arguments). |
| `ScaledTriangular` | `FunctionalModifier` | `Input`, `Scale` | `Scale * 0.5 * (sqrt(1 + 8 * Input / Scale) - 1)`, never more than `Input`; a negative input counts as 0. |

Each ID is also available as a generated key in the `sk` namespace (`sk.Modifiers.Static`, `sk.Modifiers.Linear`, ...). Missing arguments count as 0, except that `Linear`'s `Coefficient` and `Polynomial`'s `Power` and `Scale` default to 1.

## Usage Examples

The examples assume `using System.Collections.Generic;`, `using ReactiveSolutions.AttributeSystem.Core;`, `using ReactiveSolutions.AttributeSystem.Core.Data;` (`AttributeReference`) and `using ReactiveSolutions.AttributeSystem.Core.Modifiers;` (`FunctionalModifier`). `Stats.Damage` and `Stats.Strength` are keys from a generated KeyDomain class; see [Semantic Keys](Semantic%20Keys.md).

### 1. Registering a Custom Modifier

You should typically do this in your game's initialization phase (e.g., a Bootstrap script), on the factory you then pass to `statBlock.ApplyToEntity(entity, factory)`, `entity.ApplyProfile(profile, factory)` or `linkGroup.ApplyStatBlock(block, factory)`. Builders are registered per instance: `EntityController` and `StatBlockLinker` create their own `ModifierFactory` (as does `ApplyToEntity` when passed `null`), which only knows the built-ins, so a custom logic type in the StatBlocks they apply falls back to Static.

```csharp
var factory = new ModifierFactory();

// Logic: Returns 0 if "Input" is below "Threshold", otherwise returns "Bonus"
factory.Register("ThresholdBonus", spec => new FunctionalModifier(spec, vals => 
{
    float input = vals[0];
    float threshold = vals[1];
    float bonus = vals[2];
    
    return input >= threshold ? bonus : 0f;
}), "Input", "Threshold", "Bonus");

```

A spec uses this logic when its `LogicType` is a key named `ThresholdBonus`.

### 2. Using the Factory manually

```csharp
// Define the spec (arguments in the order of the parameter names)
var thresholdSpec = new AttributeModifierSpec
{
    SourceId = "MySource",
    Type = ModifierType.Additive,
    Priority = 0,
    Arguments = new List<ValueSource>
    {
        ValueSource.Const(50f), // Input
        ValueSource.Const(10f), // Threshold
        ValueSource.Const(100f) // Bonus
    }
};

// Create
var modifier = factory.Create("ThresholdBonus", thresholdSpec);

// Apply it to an attribute; dispose the handle to remove it
var entity = new Entity();
var handle = entity.AddModifier(thresholdSpec.SourceId, modifier, Stats.Damage);

```

### 3. Creating from a Spec (what StatBlocks do)

```csharp
var player = new Entity();
player.SetOrUpdateBaseValue(Stats.Strength, 10f);

var strengthScaling = new AttributeModifierSpec
{
    TargetAttribute = Stats.Damage,
    SourceId = "StrengthScaling",
    LogicType = sk.Modifiers.Linear,
    Arguments = new List<ValueSource>
    {
        new ValueSource { Mode = ValueSource.SourceMode.Attribute, AttributeRef = new AttributeReference(Stats.Strength) }, // Input
        ValueSource.Const(0.5f), // Coefficient
        ValueSource.Const(0f)    // Addend
    }
};

// Copies the arguments and bakes 'player' into the copies; strengthScaling itself is not modified.
var scaling = factory.Create(strengthScaling, player);
var scalingHandle = player.AddModifier(strengthScaling.SourceId, scaling, strengthScaling.TargetAttribute);
// player's Damage: 0 + (10 * 0.5) + 0 = 5

```
