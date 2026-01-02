# StatBlock Class Documentation

## Overview

The `StatBlock` is the primary data container for defining an entity's statistical "DNA". It is a plain C# object (POCO) designed to be serialized to and from JSON, making it ideal for data-driven game design.

A StatBlock does not contain game logic itself. Instead, it is a blueprint that, when applied to an `AttributeProcessor`, instantiates attributes and modifiers.

## Key Features

-   **Data-Driven:** Fully serializable to JSON, allowing designers to tweak character stats, item buffs, and spell effects without touching code.
    
-   **Composition:** Contains both Base Values (starting stats) and Modifiers (logic to change stats).
    
-   **Lifecycle Management:** The `ApplyToProcessor` method returns an `ActiveStatBlock`, which acts as a receipt or handle. Disposing of this handle cleanly reverts the changes (e.g., removing buffs when an item is unequipped).
    

## Class Definition

```
[System.Serializable]
public class StatBlock
{
    // ...
}

```

## Data Structure

### 1. Base Values

A list of starting values for attributes.

```
[System.Serializable]
public struct BaseValueEntry
{
    public SemanticKey Name;
    public float Value;
}
public List<BaseValueEntry> BaseValues;

```

-   **Usage:** Defining "Health = 100", "Speed = 5".
    
-   **Behavior:** When applied, these values update the `BaseValue` of the target attribute. If the attribute doesn't exist, it is created.
    

### 2. Modifiers

A list of `AttributeModifierSpec` objects defining complex logic.

```
public List<AttributeModifierSpec> Modifiers;

```

-   **Usage:** Defining "+10% Strength", "Clamp Health between 0 and MaxHealth".
    
-   **Behavior:** Each spec is converted into a live `IAttributeModifier` instance via the `ModifierFactory` and applied to the processor.

### 3. Tags

The StatBlock can now apply tags to entities. This is useful for categorization (e.g., "Undead", "Boss") or status effects (e.g., "Stunned", "Blessed").

#### Local Tags (`Tags`)

A list of strings (or SemanticKeys) applied to the **Self** (the processor the block is applied to).

```
public List<SemanticKey> Tags;

```

-   **Usage:** "This Armor is Cursed." -> Adds "Cursed" tag to the Armor.
    
-   **Behavior:** Adds the tag to the processor. Removed when the StatBlock is disposed.
    

#### Remote Tags (`RemoteTags`)

A list of specs to apply tags to **Remote Entities** via a provider path.

```
public List<TagModifierSpec> RemoteTags;

```

-   **Usage:** "This Holy Sword applies 'Blessed' to its Owner."
    
-   **Structure:**
    
    -   `Tag`: The tag to apply (e.g., "Blessed").
        
    -   `TargetPath`: The path to the target (e.g., `["Owner"]`).
        
-   **Behavior:** Creates a `TagConnection` that monitors the path. If the Owner changes, the tag moves automatically.

## Public API

### `ActiveStatBlock ApplyToProcessor(AttributeProcessor processor, IModifierFactory factory)`

This is the main entry point for using a StatBlock at runtime.

-   **Parameters:**
    
    -   `processor`: The target `AttributeProcessor` (e.g., the Player's stats).
        
    -   `factory`: The `IModifierFactory` instance used to resolve modifier logic (e.g., converting the string "Linear" into a `LinearModifier` class).
        
-   **Returns:**
    
    -   `ActiveStatBlock`: A disposable handle that tracks all modifiers created by this operation.
        
-   **Logic Flow:**
    
    1.  **Apply Base Values:** Iterates through `BaseValues` and calls `processor.SetOrUpdateBaseValue`.
        
    2.  **Create Modifiers:** Iterates through `Modifiers` (specs), uses the Factory to create instances.
        
    3.  **Apply Modifiers:** Calls `processor.AddModifier` for each instance.
        
    4.  **Track Lifecycle:** Captures the `IDisposable` returned by `AddModifier` and stores it in the `ActiveStatBlock`.
        

## JSON Structure

A StatBlock is typically stored as a `.json` file in `Resources/Data/StatBlocks`.

```
{
  "BlockName": "Iron Sword",
  "Tags": [ "Magical" ], // Local Tag on the Sword
  "RemoteTags": [
    {
      "Tag": "Blessed", // Remote Tag applied to Owner
      "TargetPath": [ "Owner" ]
    }
  ],
  "BaseValues": [
    { "Name": "Durability", "Value": 100.0 }
  ],
  "Modifiers": [
    {
      "TargetAttribute": "Damage",
      "SourceId": "SwordBaseDmg",
      "Type": 0, // Additive
      "Priority": 10,
      "LogicType": "Linear",
      "Arguments": [
        { "Mode": 0, "ConstantValue": 5.0 } // +5 Flat
      ]
    },
    {
      "TargetAttribute": "Damage",
      "SourceId": "StrengthScaling",
      "Type": 0, // Additive
      "Priority": 10,
      "LogicType": "Linear",
      "Arguments": [
        // Attribute Reference: Owner.Strength
        { 
            "Mode": 1, 
            "AttributeRef": { 
                "Name": "Strength", 
                "Path": [ "Owner" ] 
            } 
        },
        { "Mode": 0, "ConstantValue": 0.5 } // 0.5 Coefficient
      ]
    }
  ]
}

```

## Usage Example

### Loading and Applying

```
// 1. Load from JSON (using helper)
var json = Resources.Load<TextAsset>("Data/StatBlocks/MySword").text;
var statBlock = new StatBlock();
StatBlockJsonLoader.LoadIntoStatBlock(json, statBlock);

// 2. Apply to Player
// Returns a handle we MUST keep if we want to remove it later
ActiveStatBlock handle = statBlock.ApplyToProcessor(player.Processor, modifierFactory);

// ... The player now has the sword's stats ...

// 3. Remove (Unequip)
handle.Dispose();

```
