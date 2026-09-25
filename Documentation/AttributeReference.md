# AttributeReference Class Documentation

## Overview

The `AttributeReference` struct is a data structure designed to uniquely identify an attribute within the attribute system, potentially across different entities. It consists of a `Name` (SemanticKey) and an optional `Path` (List of SemanticKeys).

It serves as the "Address" for finding a value. When a `ValueSource` is in `Attribute` mode, it holds an `AttributeReference` to tell the system _where_ to look for that value. StatBlock pointers (`PointerSpec.Target`) and `Attribute.ActivePointerTarget` use it too.

## Class Definition

```csharp
// namespace ReactiveSolutions.AttributeSystem.Core.Data
[System.Serializable]
public struct AttributeReference
{
    public SemanticKey Name;
    public List<SemanticKey> Path;

    public AttributeReference(SemanticKey name, List<SemanticKey> path = null);
}

```

## Public API

### Properties

-   **`SemanticKey Name`**
    
    -   The name of the target attribute (e.g., `Stats.Strength`, `Stats.Damage`).
        
-   **`List<SemanticKey> Path`**
    
    -   An ordered list of provider keys representing the traversal path to find the target entity.
        
    -   **Empty / Null:** Refers to the _same_ entity (Local).
        
    -   **Example `[Links.Owner]`:** Refers to the entity registered as provider `Links.Owner` on the current entity (`RegisterExternalProvider`).
        
    -   **Example `[Links.Owner, Links.Hireling]`:** Refers to the "Hireling" of the "Owner".
        

### Constructor

-   **`AttributeReference(SemanticKey name, List<SemanticKey> path = null)`**
    
    -   A `null` path becomes an empty list (Local). A `default` reference has a `null` path, which also means Local.
        

### Resolving a Reference

`AttributeReference` is plain data with no methods of its own (there is no `Resolve`). The `Entity` you start from resolves it:

-   **`IObservable<Attribute> entity.GetAttributeObservable(reference.Name, reference.Path)`**
    
    -   The primary way to find the attribute at runtime.
        
    -   **Logic:**
        
        1.  Start at `entity`.
            
        2.  Follow the `Path` one provider at a time (as `Entity.ObserveProvider()` does), re-resolving whenever a provider on the path is registered, replaced or unregistered.
            
        3.  On the entity at the end of the path, observe the attribute `Name`.
            
    -   **Return:** An observable that emits the `Attribute` object once it exists (and again if it is replaced). While a provider on the path is missing (e.g., no "Owner" is registered) it emits `null`, and it switches to the new stream when the path is repaired.
        
-   **`IObservable<float> entity.ObserveValue(reference.Name, reference.Path)`**
    
    -   Emits the attribute's final value; nothing while the attribute or a provider on the path is missing.
        
-   **`Attribute entity.GetAttribute(reference.Name, reference.Path)`**
    
    -   A one-off lookup; returns `null` if the attribute or a provider on the path is missing.
        
-   `ValueSource` (Attribute mode) and pointers resolve references the same way, but read a missing attribute or provider as 0.
    

## JSON Representation

When serialized within a `StatBlock` or `ValueSource` (Unity's `JsonUtility`):

```json
{
  "Name": { "_guid": "<GUID of Stats.Intelligence>", "_value": "Intelligence", "_domainGuid": "<GUID of the Stats domain>" },
  "Path": [
    { "_guid": "<GUID of Links.Owner>", "_value": "Owner", "_domainGuid": "<GUID of the Links domain>" }
  ]
}

```

Each key is stored with its GUID, cached value and domain GUID. Keys are matched by GUID at runtime, so hand-written JSON needs the right GUIDs; let the Stat Block Editor write them (see [Semantic Keys](Semantic%20Keys.md)).

## Usage Examples

The keys (`Stats.Health`, `Links.Owner`, ...) come from static classes generated from KeyDomains; see [Semantic Keys](Semantic%20Keys.md).

### 1. Local Reference (Code)

```csharp
// Refers to "Health" on the current entity
var localRef = new AttributeReference(Stats.Health);

```

### 2. Remote Reference (Code)

```csharp
// Refers to "Strength" on the entity's "Owner"
var remoteRef = new AttributeReference(
    Stats.Strength,
    new List<SemanticKey> { Links.Owner }
);

```

### 3. Resolving Manually

```csharp
var player = new Entity();
player.SetOrUpdateBaseValue(Stats.Strength, 10f);
var sword = new Entity();

sword.GetAttributeObservable(remoteRef.Name, remoteRef.Path).Subscribe(attr => 
{
    if (attr != null)
        Debug.Log($"Found attribute: {attr.Name} with value {attr.ObservableValue.Value}");
});
// Nothing logged yet: the sword has no "Owner", so attr is null.

sword.RegisterExternalProvider(Links.Owner, player);
// Logs: "Found attribute: Strength with value 10"

// To follow the value itself:
sword.ObserveValue(remoteRef.Name, remoteRef.Path).Subscribe(v => Debug.Log($"Owner's Strength: {v}"));

```

## Key Concept: "Path Resolution"

The power of `AttributeReference` lies in its ability to traverse dynamic relationships.

-   **Scenario:** A "Squad Leader" aura that boosts "Soldier" morale.
    
-   **Path:** `[Links.SquadLeader]`
    
-   **Dynamic:** If a Soldier switches squads, the `SquadLeader` provider changes (`RegisterExternalProvider(Links.SquadLeader, newLeader)`). Whatever resolves the `AttributeReference` (a `ValueSource`, a pointer, or `GetAttributeObservable`) automatically unsubscribes from the old leader's stats and subscribes to the new leader's stats without any manual code.
