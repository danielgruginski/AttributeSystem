# LinkGroup

The `LinkGroup` is a dynamic and reactive collection of entities (`Entity` objects). It acts as a "group manager" (such as an Inventory, a Party of characters, or a list of Minions) and allows the automatic distribution of `StatBlock`s to all its members based on reactive conditions.

## Overview

While an `Entity` represents a single object in your game (a character, a sword), the `LinkGroup` represents a "one-to-many" relationship. Instead of writing manual `foreach` loops in your code to apply buffs or check statuses, you register your entities in a `LinkGroup` and let it manage the rules.

The true power of the `LinkGroup` lies in its **reactive** nature:

1.  **New Members:** If a `StatBlock` is active on the group and you add a new member, that member receives the `StatBlock` instantly.
    
2.  **Removed Members:** If a member leaves the group, the `StatBlock` is automatically cleared from them (except its permanent `BaseValues`).
    
3.  **Dynamic Conditions:** If the `StatBlock` has an `ActivationCondition` (e.g., must have the `Equipped` tag), or you pass a condition to `ApplyStatBlock`, it is evaluated for each member separately. If the tag is added to or removed from a member, the `StatBlock` is activated or deactivated for that specific member in real-time.
    

## How to Use

### 1. Creating and Managing Members

A `LinkGroup` is typically stored inside a "Parent" `Entity` (e.g., The Player contains a LinkGroup called "Inventory"). Groups listed in an `EntityProfile`'s `LinkGroups` are created when the profile is applied.

```csharp
// Getting or creating a LinkGroup in the Player's entity
LinkGroup inventory = playerEntity.GetOrCreateLinkGroup(Groups.Inventory);

// Creating item entities
Entity sword = new Entity();
Entity shield = new Entity();

// Adding to the group
inventory.AddMember(sword);
inventory.AddMember(shield);

// Removing from the group
inventory.RemoveMember(shield);

```

`Groups.Inventory` (and `Tags.Stolen` below) are keys from classes generated from your KeyDomains (see [Semantic Keys](Semantic%20Keys.md)). `GetLinkGroup(key)` returns an existing group, or `null`. Adding a member twice has no effect; `Contains(entity)` checks membership, and `Members` is an `IReadOnlyReactiveCollection<Entity>` you can observe.

### 2. Applying a StatBlock to the Group

To apply a `StatBlock` to all members of a group, use the `ApplyStatBlock` method.

```csharp
// Applies the "Sharpen" buff to all weapons in the inventory
IDisposable buffHandle = inventory.ApplyStatBlock(sharpenStatBlock);

// When the buff ends (e.g., the spell expires), simply Dispose it:
buffHandle.Dispose(); // Clears the buff from ALL items in the group

```

Each member gets its own application of the `StatBlock`: attribute references in its modifiers are read from that member (an aura of "+1 Damage per point of Strength" uses each member's own Strength), and the `StatBlock` itself is never modified. As with any StatBlock, its `BaseValues` are permanent: they stay on a member after it leaves the group or the handle is disposed.

### 3. Conditional Application (The "Law Blessing" Pattern)

If you pass a `StatBlockCondition`, the `LinkGroup` will apply the `StatBlock` **only** to the members that satisfy the condition.

**Example:** The "Law Blessing" spell increases the `SellPrice` of all items in the inventory, **unless** the item has the "Stolen" tag.

```csharp
// Creating the condition: The member MUST NOT have the "Stolen" tag
StatBlockCondition isLegalItemCondition = new StatBlockCondition
{
    Type = StatBlockCondition.Mode.Tag,
    Tag = Tags.Stolen,
    InvertTag = true // We want this to be TRUE when the tag is MISSING
};

// Apply the blessing to the group with the condition
IDisposable blessingHandle = inventory.ApplyStatBlock(lawBlessingStatBlock, isLegalItemCondition);

```

**How the system reacts in this scenario:**

-   If the `sword` does not have the "Stolen" tag, it receives the price increase.
    
-   If later the player uses a spell to "launder" a stolen item (calling `item.RemoveTag(Tags.Stolen)`), the `LinkGroup` detects the change and **automatically applies** the `lawBlessingStatBlock` to this item. (Tags are reference counted: the item counts as untagged once every `AddTag` has been matched by a `RemoveTag`.)
    
-   If the main blessing spell is canceled (`blessingHandle.Dispose()`), all items lose the price increase.
    

The StatBlock's own `ActivationCondition` still applies on top of the group condition. Keep the group condition independent of what the StatBlock itself does (for example, the blessing must not add or remove the "Stolen" tag): unlike a StatBlock's `ActivationCondition`, the group condition is not protected against such feedback loops.

## Architecture Notes

The `LinkGroup` embraces the Hybrid design philosophy of the Reactive Attribute System:

-   **`Entity`**: The **subject** (Character, Item). It has state (attributes, tags) and values that change over time.
    
-   **`StatBlock`**: The **message/rule** (Buff, Status Modifier). It has no state of its own; it's just a set of instructions (e.g., "+10 Strength"), so the same StatBlock can be applied to every member.
    
-   **`LinkGroup`**: The **glue** and **distributor**. It connects the rules (`StatBlocks`) to the subjects (`Entities`) in a scalable way, completely free of synchronization bugs.
