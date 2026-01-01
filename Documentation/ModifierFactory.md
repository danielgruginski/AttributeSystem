# ModifierFactory Documentation

## Overview

The `ModifierFactory` is the central registry for modifier logic in the Reactive Attribute System. It serves as the bridge between data-driven definitions (like JSON StatBlocks or Editor specifications) and actual C# modifier instances.

Its primary responsibilities are:

1.  **Instantiation:** Creating concrete `IAttributeModifier` instances from abstract IDs (SemanticKeys).
    
2.  **Metadata Management:** Storing parameter names (e.g., "Input", "Scale") so the Unity Editor can display user-friendly labels instead of generic "Arg 0", "Arg 1".
    
3.  **Extensibility:** Allowing developers to register custom modifier logic without modifying the core system source code.
    

## Key Features

-   **Registry-Based:** Maps a unique `string` ID (Logic Type) to a `ModifierBuilder` delegate.
    
-   **Unified Argument Passing:** All builders receive a `ModifierArgs` struct, ensuring a consistent API for accessing input values.
    
-   **Editor Integration:** Provides `GetParameterNames(id)` which the custom `AttributeModifierSpecDrawer` uses to dynamically generate Inspector UI.
    

## Class Definition

```
public class ModifierFactory : IModifierFactory
{
    // ... implementation ...
}

```

## Public API

### 1. Creation

-   **`IAttributeModifier Create(string id, ModifierArgs args)`**
    
    -   Creates a modifier instance using the registered builder for `id`.
        
    -   **Fallback:** If `id` is unknown, it returns a `StaticAttributeModifier` to prevent crashes (Null Object Pattern).
        
-   **`IAttributeModifier Create(AttributeModifierSpec spec, AttributeProcessor context = null)`**
    
    -   A higher-level helper that parses a data specification (`AttributeModifierSpec`).
        
    -   **Context Baking:** If a `context` processor is provided, it "bakes" it into any Attribute-based `ValueSource` arguments, allowing them to resolve correctly.
        

### 2. Registration

-   **`void Register(string id, ModifierBuilder builder, params string[] paramNames)`**
    
    -   Registers a new logic type.
        
    -   **`id`**: The unique key (e.g., "Linear", "MyCustomLogic").
        
    -   **`builder`**: A lambda or function that takes `ModifierArgs` and returns `IAttributeModifier`.
        
    -   **`paramNames`**: An array of strings defining the labels for the arguments in the Inspector.
        

### 3. Metadata Access

-   **`IEnumerable<string> GetAvailableTypes()`**
    
    -   Returns a list of all registered logic IDs.
        
-   **`static string[] GetParameterNames(string id)`**
    
    -   Returns the argument labels for a given logic type. Used primarily by Editor scripts.
        

## Default Registered Types

The factory comes pre-loaded with these standard modifiers:

ID

Class

Arguments

Description

`Static`

`StaticAttributeModifier`

`Value`

Returns a constant value or attribute value.

`Linear`

`LinearModifier`

`Input`, `Coefficient`, `Addend`

`(Input * Coeff) + Addend`

`Polynomial`

`PolynomialModifier`

`Input`, `Power`, `Scale`, `Flat`

`(Input ^ Power) * Scale + Flat`

`Clamp`

`FunctionalModifier`

`Input`, `Min`, `Max`

Clamps input between min and max.

`Min`

`FunctionalModifier`

`Value A`, `Value B`

Returns the smaller value.

`Max`

`FunctionalModifier`

`Value A`, `Value B`

Returns the larger value.

`Step`

`FunctionalModifier`

`Edge Threshold`, `Input Value`

Returns 1 if Input >= Edge, else 0.

## Usage Examples

### 1. Registering a Custom Modifier

You should typically do this in your game's initialization phase (e.g., a Bootstrap script).

```
var factory = new ModifierFactory();

// Logic: Returns 0 if "Input" is below "Threshold", otherwise returns "Bonus"
factory.Register("ThresholdBonus", args => new FunctionalModifier(args, vals => 
{
    float input = vals[0];
    float threshold = vals[1];
    float bonus = vals[2];
    
    return input >= threshold ? bonus : 0f;
}), "Input", "Threshold", "Bonus");

```

### 2. Using the Factory manually

```
// Define arguments
var args = new ModifierArgs("MySource", ModifierType.Additive, 0, new List<ValueSource> 
{
    ValueSource.Const(50f), // Input
    ValueSource.Const(10f), // Threshold
    ValueSource.Const(100f) // Bonus
});

// Create
var modifier = factory.Create("ThresholdBonus", args);

```
