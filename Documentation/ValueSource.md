# ValueSource Class Documentation

## Overview

`ValueSource` is the fundamental "Atom" of the Attribute System's modifier logic. It represents a single numerical input of a logic class (e.g., the "5" in "+5 Damage" or the "Strength" in "+10% of Strength"; see [Modifier Logic](Modifier%20Logic.md)). Stat block conditions, pools and effects use it too.

It can be one of three things:

1.  **Constant Mode:** Acts as a simple static float (e.g., `10.5`).
    
2.  **Attribute Mode:** Acts as a dynamic reference to another attribute, potentially on a different entity (e.g., `Owner.Strength`).
    
3.  **Formula Mode:** Acts as a logic of its own, with inputs of its own (e.g., `Defense + 100`), so formulas nest. See [Formulas](#formulas).
    

This abstraction allows _any_ modifier logic (Linear, Polynomial, etc.) to automatically support static numbers, dynamic scaling and nested formulas without writing custom code for each case.

## Class Definition

```csharp
// namespace ReactiveSolutions.AttributeSystem.Core
[System.Serializable]
public class ValueSource
{
    public enum SourceMode { Constant, Attribute, Formula }
    // ...
}

```

## Public API

### Configuration Properties

-   **`SourceMode Mode`**
    
    -   Determines how the value is resolved.
        
    -   `SourceMode.Constant` (0): Uses `ConstantValue`.
        
    -   `SourceMode.Attribute` (1): Uses `AttributeRef`.
        
    -   `SourceMode.Formula` (2): Uses `Formula`.
        
-   **`float ConstantValue`**
    
    -   The raw number used when `Mode` is `Constant`.
        
-   **`AttributeReference AttributeRef`**
    
    -   The definition of the attribute to read (Name + Path; an empty path means the entity the source is resolved from) used when `Mode` is `Attribute`.
        
    -   _See [AttributeReference](AttributeReference.md) documentation for details on paths._
        
-   **`ModifierLogic Formula`**
    
    -   The logic that computes the value when `Mode` is `Formula` (a `[SerializeReference]` field). Its inputs are ValueSources too.
        

### Runtime Methods

-   **`IObservable<float> GetObservable(Entity context)`**
    
    -   The main method used by logic classes: they pass the entity their inputs are read from (see [Context](#key-concept-context) below).
        
    -   **If Constant:** Returns `Observable.Return(ConstantValue)`.
        
    -   **If Attribute:**
        
        1.  Starts from `context` (a `null` context reads as 0).
            
        2.  Follows `AttributeRef.Path` through the providers and observes the attribute `AttributeRef.Name` on the entity at the end of the path, switching automatically when a provider on the path or the attribute itself changes.
            
        3.  Emits that attribute's final value (`ObservableValue`) and every change to it.
            
        4.  If the attribute or a provider on the path is missing (local or remote), it reads as `0f` until it exists; it never blocks the modifier.
            
    -   **If Formula:** Returns the formula's value (`Formula.Observe(context)`), with its inputs read from the same `context`. A formula that isn't set reads as 0.
        
-   **`static ValueSource Const(float val)`**
    
    -   Shorthand for a `Constant` source. A `float` also converts to a constant implicitly, so `Coefficient = 0.5f` works in code.
        
-   **`static ValueSource FromAttribute(SemanticKey name, params SemanticKey[] path)`**
    
    -   Shorthand for an `Attribute` source: `FromAttribute(Stats.Strength)` (local) or `FromAttribute(Stats.Strength, Links.Owner)` (through the `Owner` provider). An `AttributeReference` also converts to an attribute source implicitly: `Input = AttributeReference.Of(Stats.Strength, Links.Owner)`.
        
-   **`static ValueSource From(ModifierLogic formula)`**
    
    -   Shorthand for a `Formula` source. A logic also converts to a formula implicitly, so `Divisor = new LinearLogic { ... }` works in code.
        
-   **`ValueSource Clone()`**
    
    -   A copy that can be changed without changing this one: its path and its formula are copied too.
        

## Usage Examples

`AttributeReference` lives in `ReactiveSolutions.AttributeSystem.Core.Data`. The keys (`Stats.Strength`, `Links.Owner`, ...) come from static classes generated from KeyDomains; see [Semantic Keys](Semantic%20Keys.md).

### 1. Defining a Constant (Code)

```csharp
var constSource = ValueSource.Const(50f);
// Result: Always returns 50.

```

### 2. Defining a Local Reference (Code)

```csharp
// Reference to "Strength" on the SAME entity
var localSource = ValueSource.FromAttribute(Stats.Strength);

// The same, spelled out
var spelledOut = new ValueSource 
{ 
    Mode = ValueSource.SourceMode.Attribute,
    AttributeRef = new AttributeReference(Stats.Strength)
};

```

### 3. Defining a Remote Reference (Code)

```csharp
// Reference to "Intelligence" on the "Owner" provider
var remoteSource = ValueSource.FromAttribute(Stats.Intelligence, Links.Owner);

```

### 4. Defining a Formula (Code)

```csharp
// Defense + 100: the divisor of an armor formula
var divisor = ValueSource.From(new LinearLogic { Input = ValueSource.FromAttribute(Stats.Defense), Addend = 100f });

// A logic converts to a formula implicitly
var mitigated = new RatioLogic
{
    Dividend = new LinearLogic { Input = ValueSource.FromAttribute(Stats.AttackPower), Coefficient = 100f },
    Divisor = divisor
};
```

### 5. In JSON Files

In StatBlock, entity profile and effect files, a constant is a number and an attribute is its name, after the names of its provider path's steps: a Linear logic whose Input is the Dexterity of the Owner's Driver, with a Coefficient of 15, is

```json
"linear": { "input": "Owner/Driver/Dexterity", "coefficient": 15 }
```

A formula is an object with one logic, named as in a modifier: `"divisor": { "linear": { "input": "Defense", "addend": 100 } }`. The names are resolved with the file's table of keys (see [JSON Format](JSON%20Format.md)).

## Formulas

A logic computes one value from its inputs, and each input can be a logic of its own. So formulas nest, without helper attributes for the steps in between. The classic armor formula, AttackPower x 100 / (Defense + 100), is a Ratio logic whose Dividend and Divisor are Linear logic:

```json
{
  "target": "Damage",
  "ratio": {
    "dividend": { "linear": { "input": "AttackPower", "coefficient": 100 } },
    "divisor": { "linear": { "input": "Defense", "addend": 100 } }
  }
}
```

-   **They work wherever a ValueSource does.** That covers the inputs of logic, both sides of a condition's comparison (`{ "compare": [{ "ratio": { "dividend": "Health", "divisor": "MaxHealth" } }, "<", 0.5] }`) and a pool's maximum. For a pool's maximum, write the formula in `"max"`: `"Health": { "max": { "linear": ... } }`.
    
-   **They stay reactive.** A change of any input, however deep, recomputes the formula.
    
-   **Effects combine the source's and the target's attributes** in one formula this way (see [Effects](Effects.md)).
    
-   **In the Inspector**, set an input's mode to **Formula** and pick its logic. The logic's fields appear below it.

## Key Concept: Context

A `ValueSource` doesn't know which entity it belongs to: the logic that uses it passes a **context** entity, and an attribute reference is resolved from there.

-   **Scenario:** A "Fire Sword" has a modifier: `Damage += 10% of (Self) HeatLevel`, applied to its owner's Damage (target path `Owner`).
    
-   **In StatBlocks**, the context is the entity the block is applied to (the Sword), even though the modifier lives on the Player's Damage. So `HeatLevel` is read from the Sword. To read one of the Player's stats instead, give the reference a path relative to the Sword (e.g. Name `Stats.Strength`, Path `[Links.Owner]`).
    
-   **Shared data stays untouched:** the context is kept by each applied modifier (`LogicModifier.Context`), not written into the `ValueSource`, so the same StatBlock can be applied to many entities (e.g. from one profile or a LinkGroup), and each application reads its own entity's attributes.
    
-   **In code**, a `LogicModifier` created without a context reads its inputs from the entity that owns the modified attribute, i.e. the remote entity for a remote target (see [Attribute Modifiers](Attribute%20Modifiers.md#logicmodifier)).
