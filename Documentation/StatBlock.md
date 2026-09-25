# StatBlock Class Documentation

## Overview

The `StatBlock` is the primary data container for defining an entity's statistical "DNA" (such as a buff, passive ability, equipment stat modifier, or even core stat templates). It is a plain C# object (POCO) designed to be fully serializable to and from JSON/YAML, making it ideal for data-driven game design.

A `StatBlock` does not contain game logic itself. Instead, it is a blueprint that, when applied to an `Entity`, instantiates the appropriate attributes, modifiers, tags, and pointers. Applying it never modifies the `StatBlock`, so a single instance (for example one loaded from a JSON file, or authored in an `EntityController`'s profile) can be applied to any number of entities.

## Key Features

-   **Data-Driven:** Fully serializable, allowing designers to tweak item buffs, spell effects, and passives without touching code.
    
-   **Conditional Application:** Contains an `ActivationCondition` that allows the block's dynamic content (modifiers, tags, remote tags, pointers) to reactively toggle on or off based on the entity's state.
    
-   **Lifecycle Management:** The `ApplyToEntity` method returns an [`ActiveStatBlock`](ActiveStatBlock.md), which acts as a receipt or handle. Disposing of this handle cleanly reverts the dynamic changes (e.g., removing buffs when an item is unequipped).
    
-   **Permanent & Conditional Split:** `BaseValues` are applied permanently to the session regardless of conditions, ensuring the stats exist, while `Modifiers`, `Tags`, `RemoteTags` and `Pointers` are applied only while the `ActivationCondition` holds. Disposing the handle does not revert `BaseValues`.
    
-   **Reusable:** One `StatBlock` can be applied to many entities at once. Applying it never modifies it, and the attributes its modifiers read are read from the entity it was applied to.
    

## Class Definition

The class lives in the `ReactiveSolutions.AttributeSystem.Core.Data` namespace.

```csharp
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

```csharp
public List<BaseValueEntry> BaseValues;

public struct BaseValueEntry { public SemanticKey Name; public float Value; }

```

-   **Usage:** Defining "Health = 100", "Speed = 5".
    
-   **Behavior:** When applied, these values update the `BaseValue` of the target attribute (creating the attribute if it doesn't exist). **Note:** These are applied _immediately and permanently_ for the session, bypassing the `ActivationCondition`, and they are **not** reverted when the `ActiveStatBlock` is disposed. If dynamic, reversible base stats are needed, they should be created as `Modifiers`. Entries whose `Name` is unassigned (`SemanticKey.None`) are skipped.
    

### 2. Modifiers

A list of `AttributeModifierSpec` objects defining complex mathematical rules.

```csharp
public List<AttributeModifierSpec> Modifiers;

```

-   **Usage:** Defining "+10% Strength", "+2 Damage per point of Strength".
    
-   **Behavior:** Each spec becomes a live modifier (a `LogicModifier`) on the target `Entity`: its **Logic** computes the value (see [Modifier Logic](Modifier%20Logic.md)), and its **Type** and **Priority** place it in the attribute's stack of modifiers (see [Attribute Modifiers](Attribute%20Modifiers.md)). Toggles based on the `ActivationCondition`.
    
    -   A spec without a `TargetAttribute` or without a `Logic` is skipped with a warning.
        
    -   A spec with a `TargetPath` modifies the attribute on the entity at the end of that path (e.g. the `Owner`) and follows the path when it changes (see [AttributeConnection](AttributeConnection.md)).
        
    -   The logic's attribute inputs are read from the entity the block was applied to, even when the modifier targets a remote attribute: a sword's modifier on its Owner's Damage reads `Strength` from the sword, and needs the path `Owner` in the input's `AttributeReference` to read the Owner's Strength. A missing attribute or provider reads as 0.
        
    -   On each attribute, modifiers are evaluated by `Priority` (lowest first), then by type (Additive, Multiplicative, Override, Clamp Min, Clamp Max), then in the order they were added.
        
    -   A modifier that reads the attribute it modifies sees that attribute's _final_ value, including its own effect (see [Attribute Modifiers](Attribute%20Modifiers.md#modifier-types-and-order)). To keep Health between 0 and MaxHealth, use modifiers of Type **Clamp Min** and **Clamp Max**.
        

### 3. Tags

Tags applied for categorization or status effects.

-   **Local Tags (`Tags`):** A list of `SemanticKey`s applied to the **Self** (the entity the block is applied to).
    
    -   _Usage:_ Adds the "Cursed" tag to the equipped character.
        
-   **Remote Tags (`RemoteTags`):** A list of `TagModifierSpec`s (a `Tag` and a `TargetPath`) applied to **Remote Entities** via a provider path.
    
    -   _Usage:_ "This Holy Sword applies 'Blessed' to its Owner." Creates a `TagConnection` that monitors the path and moves the tag if the target changes.
        

Tags are reference counted: removing the block removes only the count it added, so a tag that another source also applied stays.

### 4. Pointers (Aliases)

A list of `PointerSpec`s that map local attribute aliases to target attributes.

```csharp
public List<PointerSpec> Pointers;

public struct PointerSpec { public SemanticKey Alias; public AttributeReference Target; }

```

-   **Usage:** Mapping a local request for `MainStat` to `Owner -> Strength`.
    
-   **Behavior:** Applies structurally to the entity, allowing subsequent modifiers in this block or others to target the alias. Toggles based on the `ActivationCondition`.
    

### 5. Activation Condition

Defines the reactive rules for when this block's dynamic content should actually be active.

```csharp
public StatBlockCondition ActivationCondition;

```

-   **Usage:** "Only apply this damage boost and 'Enraged' tag if the entity has the 'LowHealth' tag."
    
-   **Modes (`Type`):** Conditions are evaluated against the entity the block is applied to.
    
    -   `Always`: Always active. A `null` `ActivationCondition` behaves the same.
        
    -   `Tag`: Active while the entity has `Tag` (or, with `InvertTag`, while it doesn't). Set `TagTarget` to a provider path to check another entity instead (empty = self).
        
    -   `ValueComparison`: Compares `ValueA` with `ValueB` (both `ValueSource`s) using `CompareOp` (`Equal`, `NotEqual`, `Greater`, `Less`, `GreaterOrEqual`, `LessOrEqual`). `Tolerance` is the margin used by `Equal` and `NotEqual`. A missing attribute reads as 0.
        
    -   `Composite`: Combines `SubConditions` with `GroupOp` (`And` or `Or`).
        
-   **Self-defeating conditions:** The condition must not depend on the block's own effects. If applying or removing the content flips the condition (e.g. "while Health < 50: +100 Health"), there is no stable state, so the block removes its content, stays disabled for that application and logs `[StatBlock] '<BlockName>' was disabled: its activation condition depends on its own effects ...`.
    

In code, `StatBlockCondition.Always()`, `HasTag(tag, path)`, `LacksTag(tag, path)`, `Compare(a, op, b)`, `All(...)` and `Any(...)` create conditions:

```csharp
// Active while Health < 30 and the entity isn't Stunned
statBlock.ActivationCondition = StatBlockCondition.All(
    StatBlockCondition.Compare(ValueSource.FromAttribute(Stats.Health), StatBlockCondition.Comparison.Less, 30f),
    StatBlockCondition.LacksTag(Tags.Stunned));

```

In a JSON file the same condition is `{ "all": [{ "compare": ["Health", "<", 30] }, { "lacksTag": "Stunned" }] }` (see [JSON Format](JSON%20Format.md#conditions)). `Stats.Health` and `Tags.Stunned` are keys from classes generated from your KeyDomains (see [Semantic Keys](Semantic%20Keys.md)).

## Public API

### `ActiveStatBlock ApplyToEntity(Entity entity)`

This is the main entry point for using a StatBlock at runtime. It can be called on the same StatBlock for any number of entities; each call returns an independent handle.

-   **Parameters:**
    
    -   `entity`: The target `Entity` (e.g., the Player's `EntityController.Instance`).
        
-   **Returns:**
    
    -   `ActiveStatBlock`: A disposable handle that tracks all dynamic modifiers, tags, and pointers created by this operation.
        
-   **Logic Flow:**
    
    1.  **Apply Base Values:** Iterates through `BaseValues` and permanently sets them on the `entity`.
        
    2.  **Observe Condition:** Sets up a reactive subscription based on the `ActivationCondition` (`null` means always active).
        
    3.  **Activate Content:** When the condition is met, it applies Pointers, Local Tags, Remote Tags, and Modifiers (in that order), storing their disposal handles.
        
    4.  **Deactivate Content:** When the condition fails (or the parent handle is disposed), it strips away the dynamic content automatically.
        
    If activating or deactivating the content flips the condition itself, the block is disabled with an error instead of toggling forever (see _Activation Condition_ above).
    

## JSON Files

A StatBlock can be saved as a `.json` file. The **Stat Block Editor** (**Window > Attribute System > Stat Block Editor (Unified)**) saves these files to `Assets/Resources/Data/StatBlocks/`, and `StatBlockJsonLoader` reads them from there. A file describes the block the way `StatBlockBuilder` builds it, one property per builder call, and names keys by name, with a table of their GUIDs at the end (see [JSON Format](JSON%20Format.md)):

```json
{
  "statBlock": "Iron Sword",
  "condition": { "hasTag": "Owner/Armed" },
  "baseValues": { "Durability": 100 },
  "tags": ["Magical"],
  "remoteTags": ["Owner/Blessed"],
  "pointers": { "MainStat": "Owner/Strength" },
  "modifiers": [
    { "target": "Damage", "source": "SwordBaseDmg", "value": 5 },
    { "target": "Damage", "linear": { "input": "MainStat", "coefficient": 0.5 } }
  ],
  "keys": {
    "Owner": "e2d4f6a8-0c1b-4e3d-a5f7-9b1d3c5e7f90",
    "Armed": "c3e5a7b9-1d0f-4c2e-b4a6-8f0b2d4c6e8a",
    "Durability": "a7c9e1f3-5b6d-4f8a-9c0e-2b4d6f8a1c3e",
    "Magical": "0b9e4c1d-8f2a-4e6b-b1c3-5d7e9f0a2b4c",
    "Blessed": "f1a3c5e7-9b0d-4f2a-8c4e-6a8c0e2b4d6f",
    "MainStat": "6f8b0d2e-4a5c-4e7f-89ab-3d5f7a9c1e2b",
    "Strength": "5c7e9a1b-3d4f-4b6a-8c0e-1f3a5c7e9b2d",
    "Damage": "3a5c7e9b-1d2f-4a6c-8e0b-2c4d6f8a0b1e"
  }
}
```

This block is active while its owner has the `Armed` tag. It sets the sword's Durability to 100, tags the sword `Magical` and its owner `Blessed`, aliases `MainStat` to the owner's Strength, and adds 5 + MainStat x 0.5 to the sword's Damage.

`StatBlockJson.ToJson(block)` and `StatBlockJson.FromJson(json)` convert StatBlocks to and from this format in code, e.g. to save StatBlocks generated by an editor script.

## Usage Example

### Loading and Applying

```csharp
// 1. Load from JSON: StatBlockJsonLoader reads Resources/Data/StatBlocks/<id>.json
StatBlock statBlock = StatBlockJsonLoader.Load("MySwordBuff");

// 2. Apply to the Player's Entity (playerController is the Player's EntityController)
// Returns a handle we MUST keep if we want to remove the dynamic buffs later
ActiveStatBlock handle = statBlock.ApplyToEntity(playerController.Instance);

// ... The player now has the sword's modifiers active ...

// 3. Remove (Unequip)
// This strips the tags, pointers, and modifiers, but leaves the BaseValues intact.
handle.Dispose();

```

### Applying One StatBlock to Many Entities

```csharp
// goblinPassive is e.g. "Damage += Strength" (loaded with StatBlockJsonLoader.Load("Passives/Goblin")).
// Each call returns its own handle, and each goblin's bonus uses that goblin's own Strength.
ActiveStatBlock handleA = goblinPassive.ApplyToEntity(goblinA);
ActiveStatBlock handleB = goblinPassive.ApplyToEntity(goblinB);

```

To apply a StatBlock to every member of a group, including members added later, use a [LinkGroup](LinkGroup.md).
