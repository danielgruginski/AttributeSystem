# ValueSource Class Documentation

## Overview

`ValueSource` is the fundamental "Atom" of the Attribute System's modifier logic. It represents a single numerical input of a logic class (e.g., the "5" in "+5 Damage" or the "Strength" in "+10% of Strength"; see [Modifier Logic](Modifier%20Logic.md)). Stat block conditions use it too.

Its primary power lies in its **Dual Nature**:

1.  **Constant Mode:** Acts as a simple static float (e.g., `10.5`).
    
2.  **Attribute Mode:** Acts as a dynamic reference to another attribute, potentially on a different entity (e.g., `Owner.Strength`).
    

This abstraction allows _any_ modifier logic (Linear, Polynomial, etc.) to automatically support both static numbers and dynamic scaling without writing custom code for each case.

## Class Definition

```csharp
// namespace ReactiveSolutions.AttributeSystem.Core
[System.Serializable]
public class ValueSource
{
    public enum SourceMode { Constant, Attribute }
    // ...
}

```

## Public API

### Configuration Properties

-   **`SourceMode Mode`**
    
    -   Determines how the value is resolved.
        
    -   `SourceMode.Constant` (0): Uses `ConstantValue`.
        
    -   `SourceMode.Attribute` (1): Uses `AttributeRef`.
        
-   **`float ConstantValue`**
    
    -   The raw number used when `Mode` is `Constant`.
        
-   **`AttributeReference AttributeRef`**
    
    -   The definition of the attribute to read (Name + Path; an empty path means the entity the source is resolved from) used when `Mode` is `Attribute`.
        
    -   _See [AttributeReference](AttributeReference.md) documentation for details on paths._
        

### Runtime Methods

-   **`IObservable<float> GetObservable(Entity context)`**
    
    -   The main method used by logic classes: they pass the entity their inputs are read from (see [Context](#key-concept-context) below).
        
    -   **If Constant:** Returns `Observable.Return(ConstantValue)`.
        
    -   **If Attribute:**
        
        1.  Starts from `context` (a `null` context reads as 0).
            
        2.  Follows `AttributeRef.Path` through the providers and observes the attribute `AttributeRef.Name` on the entity at the end of the path, switching automatically when a provider on the path or the attribute itself changes.
            
        3.  Emits that attribute's final value (`ObservableValue`) and every change to it.
            
        4.  If the attribute or a provider on the path is missing (local or remote), it reads as `0f` until it exists; it never blocks the modifier.
            
-   **`static ValueSource Const(float val)`**
    
    -   Shorthand for a `Constant` source. A `float` also converts to a constant implicitly, so `Coefficient = 0.5f` works in code.
        
-   **`static ValueSource FromAttribute(SemanticKey name, params SemanticKey[] path)`**
    
    -   Shorthand for an `Attribute` source: `FromAttribute(Stats.Strength)` (local) or `FromAttribute(Stats.Strength, Links.Owner)` (through the `Owner` provider).
        

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

### 4. In JSON Files

In StatBlock and entity profile files, a constant is a number and an attribute is its name, after the names of its provider path's steps: a Linear logic whose Input is the Dexterity of the Owner's Driver, with a Coefficient of 15, is

```json
"linear": { "input": "Owner/Driver/Dexterity", "coefficient": 15 }
```

The names are resolved with the file's table of keys (see [JSON Format](JSON%20Format.md)).

## Key Concept: Context

A `ValueSource` doesn't know which entity it belongs to: the logic that uses it passes a **context** entity, and an attribute reference is resolved from there.

-   **Scenario:** A "Fire Sword" has a modifier: `Damage += 10% of (Self) HeatLevel`, applied to its owner's Damage (target path `Owner`).
    
-   **In StatBlocks**, the context is the entity the block is applied to (the Sword), even though the modifier lives on the Player's Damage. So `HeatLevel` is read from the Sword. To read one of the Player's stats instead, give the reference a path relative to the Sword (e.g. Name `Stats.Strength`, Path `[Links.Owner]`).
    
-   **Shared data stays untouched:** the context is kept by each applied modifier (`LogicModifier.Context`), not written into the `ValueSource`, so the same StatBlock can be applied to many entities (e.g. from one profile or a LinkGroup), and each application reads its own entity's attributes.
    
-   **In code**, a `LogicModifier` created without a context reads its inputs from the entity that owns the modified attribute, i.e. the remote entity for a remote target (see [Attribute Modifiers](Attribute%20Modifiers.md#logicmodifier)).
