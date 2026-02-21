# StatBlock Class Documentation

## Overview

The `StatBlock` is the primary data container for defining an entity's statistical "DNA" (such as a buff, passive ability, equipment stat modifier, or even core stat templates). It is a plain C# object (POCO) designed to be fully serializable to and from JSON/YAML, making it ideal for data-driven game design.

A `StatBlock` does not contain game logic itself. Instead, it is a blueprint that, when applied to an `Entity`, instantiates the appropriate attributes, modifiers, tags, and pointers.

## Key Features

-   **Data-Driven:** Fully serializable, allowing designers to tweak item buffs, spell effects, and passives without touching code.
    
-   **Conditional Application:** Contains an `ActivationCondition` that allows the block's dynamic content (modifiers, tags, pointers) to reactively toggle on or off based on the entity's state.
    
-   **Lifecycle Management:** The `ApplyToEntity` method returns an `ActiveStatBlock`, which acts as a receipt or handle. Disposing of this handle cleanly reverts the dynamic changes (e.g., removing buffs when an item is unequipped).
    
-   **Permanent & Conditional Split:** `BaseValues` are applied permanently to the session regardless of conditions, ensuring the stats exist, while `Modifiers`, `Tags`, and `Pointers` toggle based on the `ActivationCondition`.
    

## Class Definition

```
[System.Serializable]
public class StatBlock
{
    public string BlockName;
    public StatBlockCondition ActivationCondition;
    
    public List<SemanticKey> Tags;
    public List<TagModifierSpec> RemoteTags;
    public List<PointerSpec> Pointers;
    public List<BaseValueEntry> BaseValues;
    public List<AttributeModifierSpec> Modifiers;
    // ...
}

```

## Data Structure

### 1. Base Values

A list of starting values for attributes.

```
public List<BaseValueEntry> BaseValues;

```

-   **Usage:** Defining "Health = 100", "Speed = 5".
    
-   **Behavior:** When applied, these values update the `BaseValue` of the target attribute. **Note:** These are applied _immediately and permanently_ for the session, bypassing the `ActivationCondition`. If dynamic, reversible base stats are needed, they should be created as `Modifiers`.
    

### 2. Modifiers

A list of `AttributeModifierSpec` objects defining complex mathematical rules.

```
public List<AttributeModifierSpec> Modifiers;

```

-   **Usage:** Defining "+10% Strength", "Clamp Health between 0 and MaxHealth".
    
-   **Behavior:** Each spec is converted into a live `IAttributeModifier` instance via the `ModifierFactory` and applied to the target `Entity`. Toggles based on the `ActivationCondition`.
    

### 3. Tags

Tags applied for categorization or status effects.

-   **Local Tags (`Tags`):** A list of `SemanticKey`s applied to the **Self** (the entity the block is applied to).
    
    -   _Usage:_ Adds the "Cursed" tag to the equipped character.
        
-   **Remote Tags (`RemoteTags`):** A list of `TagModifierSpec`s applied to **Remote Entities** via a provider path.
    
    -   _Usage:_ "This Holy Sword applies 'Blessed' to its Owner." Creates a `TagConnection` that monitors the path.
        

### 4. Pointers (Aliases)

A list of `PointerSpec`s that map local attribute aliases to target attributes.

```
public List<PointerSpec> Pointers;

```

-   **Usage:** Mapping a local request for `MainStat` to `Owner -> Strength`.
    
-   **Behavior:** Applies structurally to the entity, allowing subsequent modifiers in this block or others to target the alias. Toggles based on the `ActivationCondition`.
    

### 5. Activation Condition

Defines the reactive rules for when this block's dynamic content should actually be active.

```
public StatBlockCondition ActivationCondition;

```

-   **Usage:** "Only apply this damage boost and 'Enraged' tag if the entity has the 'LowHealth' tag."
    

## Public API

### `ActiveStatBlock ApplyToEntity(Entity entity, IModifierFactory factory)`

This is the main entry point for using a StatBlock at runtime.

-   **Parameters:**
    
    -   `entity`: The target `Entity` engine (e.g., the Player's core engine).
        
    -   `factory`: The `IModifierFactory` instance used to resolve modifier logic (e.g., converting the string "Linear" into a `LinearModifier` class).
        
-   **Returns:**
    
    -   `ActiveStatBlock`: A disposable handle that tracks all dynamic modifiers, tags, and pointers created by this operation.
        
-   **Logic Flow:**
    
    1.  **Apply Base Values:** Iterates through `BaseValues` and permanently sets them on the `entity`.
        
    2.  **Observe Condition:** Sets up a reactive subscription based on the `ActivationCondition`.
        
    3.  **Activate Content:** When the condition is met, it applies Pointers, Local Tags, Remote Tags, and Modifiers, storing their disposal handles.
        
    4.  **Deactivate Content:** When the condition fails (or the parent handle is disposed), it strips away the dynamic content automatically.
        

## JSON Structure Example

Because it is a pure POCO, a StatBlock can be stored cleanly as a `.json` file.

```
{
  "BlockName": "Iron Sword Buff",
  "ActivationCondition": {
    "Type": 0,
    "Tag": null,
    "InvertTag": false
  },
  "Tags": [ "Magical" ],
  "RemoteTags": [
    {
      "Tag": "Blessed",
      "TargetPath": [ "Owner" ]
    }
  ],
  "Pointers": [
    {
      "Alias": "MainStat",
      "Target": { "Name": "Strength", "Path": [ "Owner" ] }
    }
  ],
  "BaseValues": [
    { "Name": "Durability", "Value": 100.0 }
  ],
  "Modifiers": [
    {
      "TargetAttribute": "Damage",
      "SourceId": "SwordBaseDmg",
      "Type": 0,
      "LogicType": "Static",
      "Arguments": [
        { "Mode": 0, "ConstantValue": 5.0 }
      ]
    }
  ]
}

```

## Usage Example

### Loading and Applying

```
// 1. Load from JSON (using a custom helper)
var json = Resources.Load<TextAsset>("Data/StatBlocks/MySwordBuff").text;
var statBlock = new StatBlock();
StatBlockJsonLoader.LoadIntoStatBlock(json, statBlock);

// 2. Apply to Player's Engine
// Returns a handle we MUST keep if we want to remove the dynamic buffs later
ActiveStatBlock handle = statBlock.ApplyToEntity(playerController.Instance, modifierFactory);

// ... The player now has the sword's modifiers active ...

// 3. Remove (Unequip)
// This strips the tags, pointers, and modifiers, but leaves the BaseValues intact.
handle.Dispose();

```
