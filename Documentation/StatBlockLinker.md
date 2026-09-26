# StatBlockLinker Class Documentation

## Overview

The `StatBlockLinker` is a Unity `MonoBehaviour` component that acts as the "Physical Interface" for your data. It connects one or more JSON-defined `StatBlock`s (like an "Iron Sword") to an in-game entity (by default, the `EntityController` on the GameObject representing that sword).

Its primary job is to **Apply** the stats when the object is created (or equipped) and **Remove** them when the object is destroyed (or unequipped), ensuring no "ghost stats" are left behind on the character. (The exception is a block's base values, which are permanent; see [StatBlock](StatBlock.md).)

## Key Features

-   **Drag-and-Drop Inspector:** Uses a custom drawer (`StatBlockID`) to allow designers to select StatBlocks from a dropdown list without typing paths manually.
    
-   **Automatic Lifecycle:** Handles the `ActiveStatBlock` receipts internally (one per block). When `OnDestroy` is called, it automatically disposes of the modifiers it created.
    
-   **Deferred Application:** Applies its blocks in `Start` rather than `Awake`, so profiles and context links set up in `Awake` (e.g. by an `AttributeContextLinker` on the same object) are already in place.
    

## Class Definition

```csharp
[RequireComponent(typeof(EntityController))]
public class StatBlockLinker : MonoBehaviour
{
    // ...
}

```

Namespace: `ReactiveSolutions.AttributeSystem.Unity`. Adding the component also adds an `EntityController` to the GameObject if it has none.

## Inspector Properties

-   **`Stat Block Ids`**
    
    -   The IDs of the JSON files to load (e.g., "Weapons/IronSword"), applied in list order.
        
    -   _Note:_ Each element is a dropdown of the StatBlock files in every `Resources/Data/StatBlocks` folder (including subfolders). **Edit** opens the picked one in the Stat Block Editor.
        
-   **`Controller`**
    
    -   The `EntityController` that will receive the stats.
        
    -   _Default:_ If left empty, it uses the `EntityController` on the same GameObject.
        

There is no "apply on awake" option: the listed blocks are always applied in `Start`. To choose the blocks from script, edit `StatBlockIds` before `Start`, or call `ApplyStatBlocks()` after changing it.

## Public API

### Methods

-   **`void ApplyStatBlocks()`**
    
    -   Called automatically in `Start`.
        
    -   Clears the currently applied blocks (see `ClearStatBlocks()`), then loads every ID in `Stat Block Ids` and applies it to the target controller's `Entity`.
        
    -   Logs a warning and applies nothing if there is no target controller.
        
-   **`void AddStatBlock(StatBlockID statBlockID)`**
    
    -   Loads one StatBlock from JSON, applies it to the target, and stores the returned `ActiveStatBlock` handle internally. Logs a warning and does nothing if there is no target controller.
        
    -   _Note:_ The ID is not added to `Stat Block Ids`, so the next `ApplyStatBlocks()` call removes the block, including the call in `Start`: add blocks this way after `Start` has run. Empty IDs are ignored; if the JSON file can't be found, `StatBlockJsonLoader` logs an error and nothing is applied.
        
-   **`void ClearStatBlocks()`**
    
    -   Disposes every stored handle, removing the blocks' modifiers, tags and pointers. Called automatically in `OnDestroy`.
        
-   **`void SetTarget(EntityController controller)`**
    
    -   Sets the target controller manually (e.g., when spawning a weapon and assigning it to a specific player).
        
    -   _Note:_ Does not re-apply. Call it before `Start`, or call `ApplyStatBlocks()` afterwards (which also removes the blocks from the previous target).
        

## Usage Examples

### 1. Standard Usage (Equipment)

1.  Create a "Sword" GameObject.
    
2.  Add a `StatBlockLinker` component.
    
3.  Add "Weapons/IronSword" to **Stat Block Ids** using the dropdown.
    
4.  Drag the Player's `EntityController` into the **Controller** field.
    
5.  **Result:** When the Sword starts, the Player gains the block's bonuses (e.g. +10 Damage). When the Sword is destroyed, they are removed.
    

Attribute references inside the block are resolved from the entity the block is applied to (here, the Player). For blocks that read the owner's stats through an `Owner` path, give the Sword its own stats instead: keep the default **Controller** and link the Player with an `AttributeContextLinker` (see [Getting Started](Getting%20Started.md)).

### 2. Manual Application (Scripting)

```csharp
public void EquipItem(GameObject itemPrefab, EntityController player)
{
    var item = Instantiate(itemPrefab);
    var linker = item.GetComponent<StatBlockLinker>();
    
    // Assign the player as the target for these stats
    linker.SetTarget(player);
    
    // Force application now (otherwise Start applies them)
    linker.ApplyStatBlocks();
}

```

## Internal Lifecycle Logic

The class manages a private `List<ActiveStatBlock> _activeBlocks`, one handle per applied block.

1.  **On Apply** (`ApplyStatBlocks`, which calls `AddStatBlock` for each ID):
    
    ```csharp
    ClearStatBlocks(); // Clean up old stats
    // ...then, for each loaded block:
    var activeHandle = block.ApplyToEntity(_controller.Instance); // Apply new
    _activeBlocks.Add(activeHandle); // Save receipt
    
    ```
    
2.  **On Destroy:**
    
    ```csharp
    ClearStatBlocks(); // Disposes every handle: ensure stats die with this object
    
    ```
