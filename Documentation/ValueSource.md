# ValueSource Class Documentation

## Overview

`ValueSource` is the fundamental "Atom" of the Attribute System's modifier logic. It represents a single numerical input that can be supplied to a modifier (e.g., the "5" in "+5 Damage" or the "Strength" in "+10% of Strength").

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

-   **`IObservable<float> GetObservable(Entity localProcessor)`**
    
    -   The main method used by Modifiers. A modifier passes the `Entity` that owns the attribute it modifies.
        
    -   **If Constant:** Returns `Observable.Return(ConstantValue)`.
        
    -   **If Attribute:**
        
        1.  Starts from the baked context if one was set (see `BakeContext`), otherwise from `localProcessor`.
            
        2.  Follows `AttributeRef.Path` through the providers and observes the attribute `AttributeRef.Name` on the entity at the end of the path, switching automatically when a provider on the path or the attribute itself changes.
            
        3.  Emits that attribute's final value (`ObservableValue`) and every change to it.
            
        4.  If the attribute or a provider on the path is missing (local or remote), it reads as `0f` until it exists; it never blocks the modifier.
            
-   **`void BakeContext(Entity context)`**
    
    -   _Advanced:_ Pre-assigns a specific entity as the "Root" for path resolution.
        
    -   Used when a modifier is created from a specific context (like a Sword) but applied elsewhere. It ensures "Self" refers to the Sword, not the Player holding it.
        
    -   It changes this instance. `ModifierFactory.Create(spec, context)` only bakes per-application copies (see `Clone`), so shared StatBlock data is never modified; do the same if you bake a `ValueSource` you didn't create.
        
-   **`ValueSource Clone()`**
    
    -   Returns a copy that can be baked independently (it shares the `AttributeRef` path list with the original).
        
-   **`static ValueSource Const(float val)`**
    
    -   Shorthand for a `Constant` source.
        

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
var localSource = new ValueSource 
{ 
    Mode = ValueSource.SourceMode.Attribute,
    AttributeRef = new AttributeReference(Stats.Strength)
};

```

### 3. Defining a Remote Reference (Code)

```csharp
// Reference to "Intelligence" on the "Owner" provider
var remoteSource = new ValueSource 
{ 
    Mode = ValueSource.SourceMode.Attribute,
    AttributeRef = new AttributeReference(
        Stats.Intelligence, 
        new List<SemanticKey> { Links.Owner }
    )
};

```

### 4. JSON Representation

When serialized in a `StatBlock` JSON (Unity's `JsonUtility`, as the Stat Block Editor writes it; other fields omitted):

**Constant:**

```json
{
  "Mode": 0,
  "ConstantValue": 15.0
}

```

**Attribute (Remote):**

```json
{
  "Mode": 1,
  "AttributeRef": {
    "Name": { "_guid": "<GUID of Stats.Dexterity>", "_value": "Dexterity", "_domainGuid": "<GUID of the Stats domain>" },
    "Path": [
      { "_guid": "<GUID of Links.Owner>", "_value": "Owner", "_domainGuid": "<GUID of the Links domain>" },
      { "_guid": "<GUID of Links.Driver>", "_value": "Driver", "_domainGuid": "<GUID of the Links domain>" }
    ]
  }
}

```

Each key is stored with its GUID, cached value and domain GUID. Keys are matched by GUID at runtime, so hand-written JSON needs the right GUIDs; let the Stat Block Editor write them (see [Semantic Keys](Semantic%20Keys.md)).

## Key Concept: "Context Baking"

When a modifier is created, it needs to know _where_ to start looking for attributes.

-   **Scenario:** A "Fire Sword" has a modifier: `Damage += 10% of (Self) HeatLevel`.
    
-   **Problem:** When the Sword is equipped by the Player, the modifier is applied to the _Player's_ Damage attribute. If we aren't careful, `(Self)` might be interpreted as the Player.
    
-   **Solution:** When a StatBlock is applied to an entity, `ModifierFactory.Create(spec, entity)` "Bakes" that entity (the Sword) into per-application copies of the spec's `ValueSource`s before the modifier is applied. This ensures that even when the modifier lives on the Player (its target path is `Owner`), it correctly reads `HeatLevel` from the Sword. To read one of the Player's stats instead, give the reference a path relative to the Sword (e.g. Name `Stats.Strength`, Path `[Links.Owner]`).
    
-   **Shared data stays untouched:** The spec itself is never modified, so the same StatBlock can be applied to many entities (e.g. from one profile or a LinkGroup), and each application reads its own entity's attributes.
    
-   **Without baking** (e.g. a modifier you build yourself and add with `Entity.AddModifier(sourceId, modifier, attribute, providerPath)`), references resolve relative to the entity that owns the modified attribute, i.e. the remote entity for a remote target.
