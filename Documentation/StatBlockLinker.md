# StatBlockLinker Class Documentation

## Overview

The `StatBlockLinker` is a Unity `MonoBehaviour` component that acts as the "Physical Interface" for your data. It connects a JSON-defined `StatBlock` (like an "Iron Sword") to an in-game entity (the GameObject representing that sword).

Its primary job is to **Apply** the stats when the object is created (or equipped) and **Remove** them when the object is destroyed (or unequipped), ensuring no "ghost stats" are left behind on the character.

## Key Features

-   **Drag-and-Drop Inspector:** Uses a custom drawer (`StatBlockID`) to allow designers to select StatBlocks from a dropdown list without typing paths manually.
    
-   **Automatic Lifecycle:** Handles the `ActiveStatBlock` receipt internally. When `OnDestroy` is called, it automatically disposes of the modifiers it created.
    
-   **Lazy Initialization:** Waits for the target `AttributeController` to be ready before applying stats.
    

## Class Definition

```
[AddComponentMenu("Attribute System/Stat Block Linker")]
public class StatBlockLinker : MonoBehaviour
{
    // ...
}

```

## Inspector Properties

-   **`Target Controller`**
    
    -   The `AttributeController` that will receive the stats.
        
    -   _Default:_ If left empty, it tries to find an `AttributeController` on the same GameObject.
        
-   **`Stat Block`**
    
    -   The ID of the JSON file to load (e.g., "Weapons/IronSword").
        
    -   _Note:_ The custom drawer for this field scans `Resources/Data/StatBlocks` to populate a dropdown.
        
-   **`Apply On Awake`**
    
    -   If `true`, the stats are applied immediately when the GameObject initializes.
        
    -   If `false`, you must call `ApplyStatBlock()` manually via script.
        

## Public API

### Methods

-   **`void ApplyStatBlock()`**
    
    -   Loads the JSON specified by `Stat Block` ID.
        
    -   Deserializes it into a `StatBlock` object.
        
    -   Applies it to the target processor.
        
    -   **Crucial:** Stores the returned `ActiveStatBlock` handle internally. If a previous block was applied, it is `Dispose()`d first.
        
-   **`void SetStatBlock(StatBlockID blockId)`**
    
    -   Changes the block ID at runtime.
        
    -   _Note:_ Does not automatically re-apply. Call `ApplyStatBlock()` afterwards.
        
-   **`void SetTarget(AttributeController controller)`**
    
    -   Sets the target controller manually (e.g., when spawning a weapon and assigning it to a specific player).
        
-   **`void Construct(IModifierFactory factory)`**
    
    -   _Advanced:_ Injects a specific `ModifierFactory`. If not called, a default factory is created.
        

## Usage Examples

### 1. Standard Usage (Equipment)

1.  Create a "Sword" GameObject.
    
2.  Add a `StatBlockLinker` component.
    
3.  Select "Weapons/IronSword" in the dropdown.
    
4.  Link it to the Player's `AttributeController`.
    
5.  **Result:** When the Sword spawns, the Player gains +10 Damage. When the Sword is destroyed, the +10 Damage is removed.
    

### 2. Manual Application (Scripting)

```
public void EquipItem(GameObject itemPrefab, AttributeController player)
{
    var item = Instantiate(itemPrefab);
    var linker = item.GetComponent<StatBlockLinker>();
    
    // Assign the player as the target for these stats
    linker.SetTarget(player);
    
    // Force application now
    linker.ApplyStatBlock();
}

```

## Internal Lifecycle Logic

The class manages a private `ActiveStatBlock _activeHandle`.

1.  **On Apply:**
    
    ```
    _activeHandle?.Dispose(); // Clean up old stats
    _activeHandle = statBlock.ApplyToProcessor(...); // Apply new & save receipt
    
    ```
    
2.  **On Destroy:**
    
    ```
    _activeHandle?.Dispose(); // Ensure stats die with this object
    
    ```
