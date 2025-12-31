# AttributeReference Class Documentation

## Overview

The `AttributeReference` class is a data structure designed to uniquely identify an attribute within the attribute system, potentially across different entities. It consists of a `Name` (SemanticKey) and an optional `Path` (List of SemanticKeys).

It serves as the "Address" for finding a value. When a `ValueSource` is in `Attribute` mode, it holds an `AttributeReference` to tell the system _where_ to look for that value.

## Class Definition

```
[System.Serializable]
public class AttributeReference
{
    // ...
}

```

## Public API

### Properties

-   **`SemanticKey Name`**
    
    -   The name of the target attribute (e.g., "Strength", "Damage").
        
-   **`List<SemanticKey> Path`**
    
    -   An ordered list of keys representing the traversal path to find the target processor.
        
    -   **Empty / Null:** Refers to the _same_ processor (Local).
        
    -   **Example `["Owner"]`:** Refers to the processor registered as "Owner" on the current processor.
        
    -   **Example `["Owner", "Hireling"]`:** Refers to the "Hireling" of the "Owner".
        

### Methods

-   **`IObservable<Attribute> Resolve(AttributeProcessor startContext)`**
    
    -   The primary logic for finding the attribute at runtime.
        
    -   **Logic:**
        
        1.  Start at `startContext`.
            
        2.  Recursively resolve the `Path` using `AttributeProcessor.ObserveProvider()`.
            
        3.  Once the final processor is found, call `GetAttributeObservable(Name)`.
            
    -   **Return:** An observable that emits the `Attribute` object. If the path is broken (e.g., "Owner" is null), it may not emit or will switch to a new stream when the path is repaired.
        

## JSON Representation

When serialized within a `StatBlock` or `ValueSource`:

```
{
  "Name": "Intelligence",
  "Path": [ "Owner" ]
}

```

## Usage Examples

### 1. Local Reference (Code)

```
// Refers to "Health" on the current entity
var localRef = new AttributeReference(new SemanticKey("Health"));

```

### 2. Remote Reference (Code)

```
// Refers to "Strength" on the entity's "Owner"
var remoteRef = new AttributeReference(
    new SemanticKey("Strength"),
    new List<SemanticKey> { new SemanticKey("Owner") }
);

```

### 3. Resolving Manually

```
AttributeReference ref = ...;
AttributeProcessor myProcessor = ...;

ref.Resolve(myProcessor).Subscribe(attr => 
{
    if (attr != null)
        Debug.Log($"Found attribute: {attr.Name} with value {attr.CurrentValue}");
});

```

## Key Concept: "Path Resolution"

The power of `AttributeReference` lies in its ability to traverse dynamic relationships.

-   **Scenario:** A "Squad Leader" aura that boosts "Soldier" morale.
    
-   **Path:** `["SquadLeader"]`
    
-   **Dynamic:** If a Soldier switches squads, the `SquadLeader` provider changes. The `AttributeReference` (via `Resolve`) automatically unsubscribes from the old leader's stats and subscribes to the new leader's stats without any manual code.
