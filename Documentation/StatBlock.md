# StatBlock Class Documentation

## Overview

The `StatBlock` is the primary data container for defining an entity's statistical "DNA" (such as a buff, passive ability, equipment stat modifier, or even core stat templates). It is a plain C# object (POCO) designed to be fully serializable to and from JSON/YAML, making it ideal for data-driven game design.

A `StatBlock` does not contain game logic itself. Instead, it is a blueprint that, when applied to an `Entity`, instantiates the appropriate attributes, modifiers, tags, and pointers. Applying it never modifies the `StatBlock`, so a single instance (for example a `StatBlockSO` asset, created with **Create > Attribute System > Stat Block**) can be applied to any number of entities.

## Key Features

-   **Data-Driven:** Fully serializable, allowing designers to tweak item buffs, spell effects, and passives without touching code.
    
-   **Conditional Application:** Contains an `ActivationCondition` that allows the block's dynamic content (modifiers, tags, remote tags, pointers) to reactively toggle on or off based on the entity's state.
    
-   **Lifecycle Management:** The `ApplyToEntity` method returns an [`ActiveStatBlock`](ActiveStatBlock.md), which acts as a receipt or handle. Disposing of this handle cleanly reverts the dynamic changes (e.g., removing buffs when an item is unequipped).
    
-   **Permanent & Conditional Split:** `BaseValues` are applied permanently to the session regardless of conditions, ensuring the stats exist, while `Modifiers`, `Tags`, `RemoteTags` and `Pointers` are applied only while the `ActivationCondition` holds. Disposing the handle does not revert `BaseValues`.
    
-   **Reusable:** One `StatBlock` can be applied to many entities at once. Each application works on its own copies of the modifier arguments, and attribute references in them are read from the entity the block was applied to.
    

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
    
-   **Behavior:** Each spec is converted into a live `IAttributeModifier` instance via the `ModifierFactory` and applied to the target `Entity`. Toggles based on the `ActivationCondition`.
    
    -   A spec without a `TargetAttribute` is skipped with a warning.
        
    -   A spec with a `TargetPath` modifies the attribute on the entity at the end of that path (e.g. the `Owner`) and follows the path when it changes (see [AttributeConnection](AttributeConnection.md)).
        
    -   Attribute arguments are resolved from the entity the block was applied to, even when the modifier targets a remote attribute: a sword's modifier on its Owner's Damage reads `Strength` from the sword, and needs the path `Owner` in the argument's `AttributeReference` to read the Owner's Strength. A missing attribute or provider reads as 0.
        
    -   On each attribute, modifiers are evaluated by `Priority` (lowest first), then by type (Additive, then Multiplicative, then Override), then in the order they were added.
        
    -   A modifier that reads the attribute it modifies sees that attribute's _final_ value, so self-referencing rules (such as clamping Health between 0 and MaxHealth) are not supported yet.
        

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
    

```csharp
// Active while Health < 30
statBlock.ActivationCondition = new StatBlockCondition
{
    Type = StatBlockCondition.Mode.ValueComparison,
    ValueA = new ValueSource { Mode = ValueSource.SourceMode.Attribute, AttributeRef = new AttributeReference(Stats.Health) },
    CompareOp = StatBlockCondition.Comparison.Less,
    ValueB = ValueSource.Const(30f)
};

```

`Stats.Health` is a key from a class generated from your `Stats` KeyDomain (see [Semantic Keys](Semantic%20Keys.md)).

## Public API

### `ActiveStatBlock ApplyToEntity(Entity entity, IModifierFactory factory)`

This is the main entry point for using a StatBlock at runtime. It can be called on the same StatBlock for any number of entities; each call returns an independent handle.

-   **Parameters:**
    
    -   `entity`: The target `Entity` (e.g., the Player's `EntityController.Instance`).
        
    -   `factory`: The `IModifierFactory` instance used to resolve modifier logic (e.g., turning the logic type `sk.Modifiers.Linear` into a `LinearModifier`). The factory looks logic types up by the key's name; an unknown name logs a warning and falls back to Static. If `null`, a new `ModifierFactory` with the built-in types is used.
        
-   **Returns:**
    
    -   `ActiveStatBlock`: A disposable handle that tracks all dynamic modifiers, tags, and pointers created by this operation.
        
-   **Logic Flow:**
    
    1.  **Apply Base Values:** Iterates through `BaseValues` and permanently sets them on the `entity`.
        
    2.  **Observe Condition:** Sets up a reactive subscription based on the `ActivationCondition` (`null` means always active).
        
    3.  **Activate Content:** When the condition is met, it applies Pointers, Local Tags, Remote Tags, and Modifiers (in that order), storing their disposal handles.
        
    4.  **Deactivate Content:** When the condition fails (or the parent handle is disposed), it strips away the dynamic content automatically.
        
    If activating or deactivating the content flips the condition itself, the block is disabled with an error instead of toggling forever (see _Activation Condition_ above).
    

## JSON Structure Example

Because it is a pure POCO, a StatBlock can be stored cleanly as a `.json` file. The **Stat Block Editor** (**Window > Attribute System > Stat Block Editor (Unified)**) saves these files to `Assets/Resources/Data/StatBlocks/`, and `StatBlockJsonLoader` reads them from there with `JsonUtility`.

Every `SemanticKey` is stored with its GUID (`_guid`), name (`_value`) and domain GUID (`_domainGuid`). Keys are matched by GUID at runtime, so create these files with the editor rather than typing names by hand (see [Semantic Keys](Semantic%20Keys.md)). Enums are stored as numbers (`"Type": 0` is `Always` for the condition and `Additive` for a modifier). In the example below, GUIDs are placeholders and some fields are left out.

```json
{
  "BlockName": "Iron Sword Buff",
  "ActivationCondition": {
    "Type": 0,
    "InvertTag": false
  },
  "Tags": [
    { "_guid": "<guid>", "_value": "Magical", "_domainGuid": "<Tags domain guid>" }
  ],
  "RemoteTags": [
    {
      "Tag": { "_guid": "<guid>", "_value": "Blessed", "_domainGuid": "<Tags domain guid>" },
      "TargetPath": [
        { "_guid": "<guid>", "_value": "Owner", "_domainGuid": "<Links domain guid>" }
      ]
    }
  ],
  "Pointers": [
    {
      "Alias": { "_guid": "<guid>", "_value": "MainStat", "_domainGuid": "<Stats domain guid>" },
      "Target": {
        "Name": { "_guid": "<guid>", "_value": "Strength", "_domainGuid": "<Stats domain guid>" },
        "Path": [
          { "_guid": "<guid>", "_value": "Owner", "_domainGuid": "<Links domain guid>" }
        ]
      }
    }
  ],
  "BaseValues": [
    {
      "Name": { "_guid": "<guid>", "_value": "Durability", "_domainGuid": "<Stats domain guid>" },
      "Value": 100.0
    }
  ],
  "Modifiers": [
    {
      "TargetAttribute": { "_guid": "<guid>", "_value": "Damage", "_domainGuid": "<Stats domain guid>" },
      "TargetPath": [],
      "SourceId": "SwordBaseDmg",
      "Type": 0,
      "Priority": 0,
      "LogicType": { "_guid": "<guid>", "_value": "Static", "_domainGuid": "<Modifiers domain guid>" },
      "Arguments": [
        { "Mode": 0, "ConstantValue": 5.0 }
      ]
    }
  ]
}

```

## Usage Example

### Loading and Applying

```csharp
// 1. Load from JSON: StatBlockJsonLoader reads Resources/Data/StatBlocks/<id>.json
StatBlock statBlock = StatBlockJsonLoader.Load("MySwordBuff");

// 2. Apply to the Player's Entity (playerController is the Player's EntityController)
// Returns a handle we MUST keep if we want to remove the dynamic buffs later
var modifierFactory = new ModifierFactory();
ActiveStatBlock handle = statBlock.ApplyToEntity(playerController.Instance, modifierFactory);

// ... The player now has the sword's modifiers active ...

// 3. Remove (Unequip)
// This strips the tags, pointers, and modifiers, but leaves the BaseValues intact.
handle.Dispose();

```

### Applying One StatBlock to Many Entities

```csharp
// goblinPassive is e.g. "Damage += Strength" (loaded with StatBlockJsonLoader.Load("Passives/Goblin")).
// Each call returns its own handle, and each goblin's bonus uses that goblin's own Strength.
ActiveStatBlock handleA = goblinPassive.ApplyToEntity(goblinA, modifierFactory);
ActiveStatBlock handleB = goblinPassive.ApplyToEntity(goblinB, modifierFactory);

```

To apply a StatBlock to every member of a group, including members added later, use a [LinkGroup](LinkGroup.md).
