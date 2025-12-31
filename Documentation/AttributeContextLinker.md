# AttributeContextLinker Documentation

## Overview

The `AttributeContextLinker` is a Unity `MonoBehaviour` designed to manage the context connections (Dependency Injection) for an `AttributeController`.

In the Reactive Attribute System, entities often need to read stats from other entities (e.g., a Sword reading its Owner's Strength). This component is the bridge that tells the system: _"This Sword's 'Owner' is that Player."_

## Key Features

-   **Inspector Configuration:** Allows you to drag-and-drop other `AttributeController`s to link them as providers.
    
-   **Dynamic Linking:** Supports linking at runtime via script, which is essential for equipping items or spawning minions.
    
-   **Automatic Registration:** On `Start`, it automatically calls `RegisterExternalProvider` on the main controller for every link defined in the inspector.
    

## Class Definition

```
[AddComponentMenu("Attribute System/Context Linker")]
public class AttributeContextLinker : MonoBehaviour
{
    // ...
}

```

## Inspector Usage

1.  **Context Root:** The `AttributeController` _on this object_ that needs the external data.
    
2.  **Links (List):**
    
    -   **Key:** The alias for the provider (e.g., "Owner", "Target", "Vehicle").
        
    -   **Target:** The `AttributeController` of the other entity.
        

## Public API

### Methods

-   **`void Link(SemanticKey key, AttributeController target)`**
    
    -   Registers `target` as the provider for `key`.
        
    -   _Example:_ When a player picks up a weapon:
        
        ```
        weapon.GetComponent<AttributeContextLinker>().Link(new SemanticKey("Owner"), playerController);
        
        ```
        
-   **`void Unlink(SemanticKey key)`**
    
    -   Removes the provider for `key`.
        
    -   Any `AttributeConnection`s traversing this link will momentarily pause (resolve to null) until a new link is established.
        

## Use Cases

### 1. Equipment (The "Owner" Pattern)

-   **Scenario:** A Sword has a modifier `Damage += 50% Owner.Strength`.
    
-   **Setup:**
    
    1.  The Sword prefab has an `AttributeContextLinker`.
        
    2.  When the Player equips the Sword, the Inventory System calls: `swordLinker.Link(new SemanticKey("Owner"), playerAttributeController);`
        
    3.  **Result:** The connection is live. If the Player's Strength changes, the Sword's Damage updates immediately.
        

### 2. Minions / Summons

-   **Scenario:** A Necromancer summons a Skeleton. The Skeleton's Health scales with the Necromancer's Intelligence.
    
-   **Setup:**
    
    1.  Spawn the Skeleton prefab.
        
    2.  `skeletonLinker.Link(new SemanticKey("Summoner"), necromancerController);`
        
    3.  Skeleton stats use the path `["Summoner"]` to read Intelligence.
        

### 3. Vehicles / Mounts

-   **Scenario:** A Player mounts a Horse. The Horse's speed is boosted by the Player's RidingSkill.
    
-   **Setup:**
    
    1.  `horseLinker.Link(new SemanticKey("Rider"), playerController);`
        
    2.  Horse stats use path `["Rider"]`.
