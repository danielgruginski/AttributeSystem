# ValueSource Class Documentation

## Overview

`ValueSource` is the fundamental "Atom" of the Attribute System's modifier logic. It represents a single numerical input that can be supplied to a modifier (e.g., the "5" in "+5 Damage" or the "Strength" in "+10% of Strength").

Its primary power lies in its **Dual Nature**:

1.  **Constant Mode:** Acts as a simple static float (e.g., `10.5`).
    
2.  **Attribute Mode:** Acts as a dynamic reference to another attribute, potentially on a different entity (e.g., `Owner.Strength`).
    

This abstraction allows _any_ modifier logic (Linear, Polynomial, etc.) to automatically support both static numbers and dynamic scaling without writing custom code for each case.

## Class Definition

```
[System.Serializable]
public class ValueSource
{
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
    
    -   The definition of the remote attribute (Name + Path) used when `Mode` is `Attribute`.
        
    -   _See `AttributeReference` documentation for details on paths._
        

### Runtime Methods

-   **`IObservable<float> GetObservable(AttributeProcessor context)`**
    
    -   The main method used by Modifiers.
        
    -   **If Constant:** Returns `Observable.Return(ConstantValue)`.
        
    -   **If Attribute:**
        
        1.  Uses `AttributeReference.Resolve(context)` to find the target attribute.
            
        2.  Subscribes to its `ReactivePropertyAccess`.
            
        3.  If the attribute is missing or the path is broken, defaults to `0f` (or the last known value).
            
-   **`void BakeContext(AttributeProcessor context)`**
    
    -   _Advanced:_ Pre-assigns a specific processor as the "Root" for path resolution.
        
    -   Used when a modifier is created from a specific context (like a Sword) but applied elsewhere. It ensures "Self" refers to the Sword, not the Player holding it.
        

## Usage Examples

### 1. Defining a Constant (Code)

```
var constSource = ValueSource.Const(50f);
// Result: Always returns 50.

```

### 2. Defining a Local Reference (Code)

```
// Reference to "Strength" on the SAME processor
var localSource = new ValueSource 
{ 
    Mode = ValueSource.SourceMode.Attribute,
    AttributeRef = new AttributeReference(new SemanticKey("Strength"))
};

```

### 3. Defining a Remote Reference (Code)

```
// Reference to "Intelligence" on the "Owner" provider
var remoteSource = new ValueSource 
{ 
    Mode = ValueSource.SourceMode.Attribute,
    AttributeRef = new AttributeReference(
        new SemanticKey("Intelligence"), 
        new List<SemanticKey> { new SemanticKey("Owner") }
    )
};

```

### 4. JSON Representation

When serialized in a `StatBlock` JSON:

**Constant:**

```
{
  "Mode": 0,
  "ConstantValue": 15.0
}

```

**Attribute (Remote):**

```
{
  "Mode": 1,
  "AttributeRef": {
    "Name": "Dexterity",
    "Path": [ "Owner", "Driver" ]
  }
}

```

## Key Concept: "Context Baking"

When a modifier is created, it needs to know _where_ to start looking for attributes.

-   **Scenario:** A "Fire Sword" has a modifier: `Damage += 10% of (Self) HeatLevel`.
    
-   **Problem:** When the Sword is equipped by the Player, the modifier is applied to the _Player's_ Damage attribute. If we aren't careful, `(Self)` might be interpreted as the Player.
    
-   **Solution:** The system "Bakes" the Sword's processor into the `ValueSource` before applying it. This ensures that even when the modifier lives on the Player, it correctly reads `HeatLevel` from the Sword.
