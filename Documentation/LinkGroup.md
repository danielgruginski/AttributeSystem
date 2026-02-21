# LinkGroup

The `LinkGroup` is a dynamic and reactive collection of `AttributeProcessor`s. It acts as a "group manager" (such as an Inventory, a Party of characters, or a list of Minions) and allows the automatic distribution of `StatBlock`s to all its members based on reactive conditions.

## Overview

While an `AttributeProcessor` represents an individual entity (a character, a sword), the `LinkGroup` represents a "one-to-many" relationship. Instead of writing manual `foreach` loops in your code to apply buffs or check statuses, you register your processors in a `LinkGroup` and let it manage the rules.

The true power of the `LinkGroup` lies in its **reactive** nature:

1.  **New Members:** If a `StatBlock` is active on the group and you add a new member, that member receives the `StatBlock` instantly.
    
2.  **Removed Members:** If a member leaves the group, the `StatBlock` is automatically cleared from them.
    
3.  **Dynamic Conditions:** If the `StatBlock` has a condition (e.g., must have the `Equipped` tag), the `LinkGroup` continuously observes the members. If the tag is added or removed from a member, the `StatBlock` is activated or deactivated for that specific member in real-time.
    

## How to Use

### 1. Creating and Managing Members

A `LinkGroup` is typically stored inside a "Parent" `AttributeProcessor` (e.g., The Player contains a LinkGroup called "Inventory").

```
// Getting or creating a LinkGroup in the Player's processor
LinkGroup inventory = playerProcessor.GetOrCreateLinkGroup(new SemanticKey("Inventory"));

// Creating item processors
AttributeProcessor sword = new AttributeProcessor();
AttributeProcessor shield = new AttributeProcessor();

// Adding to the group
inventory.AddMember(sword);
inventory.AddMember(shield);

// Removing from the group
inventory.RemoveMember(shield);

```

### 2. Applying a StatBlock to the Group

To apply a `StatBlock` to all members of a group, use the `ApplyStatBlock` method. This method requires the `StatBlock` itself and an `IModifierFactory` (usually provided by your `AttributeController`).

```
// Applies the "Sharpen" buff to all weapons in the inventory
IDisposable buffHandle = inventory.ApplyStatBlock(sharpenStatBlock, modifierFactory);

// When the buff ends (e.g., the spell expires), simply Dispose it:
buffHandle.Dispose(); // Clears the buff from ALL items in the group

```

### 3. Conditional Application (The "Law Blessing" Pattern)

If you pass a `StatBlockCondition`, the `LinkGroup` will apply the `StatBlock` **only** to the members that satisfy the condition.

**Example:** The "Law Blessing" spell increases the `SellPrice` of all items in the inventory, **unless** the item has the "Stolen" tag.

```
SemanticKey stolenTag = new SemanticKey("Stolen");

// Creating the condition: The member MUST NOT have the "Stolen" tag
StatBlockCondition isLegalItemCondition = new StatBlockCondition
{
    Type = StatBlockCondition.Mode.Tag,
    Tag = stolenTag,
    InvertTag = true // We want this to be TRUE when the tag is MISSING
};

// Apply the blessing to the group with the condition
IDisposable blessingHandle = inventory.ApplyStatBlock(lawBlessingStatBlock, modifierFactory, isLegalItemCondition);

```

**How the system reacts in this scenario:**

-   If the `sword` does not have the "Stolen" tag, it receives the price increase.
    
-   If later the player uses a spell to "launder" a stolen item (calling `itemProcessor.RemoveTag(stolenTag)`), the `LinkGroup` detects the change and **automatically applies** the `lawBlessingStatBlock` to this item.
    
-   If the main blessing spell is canceled (`blessingHandle.Dispose()`), all items lose the price increase.
    

## Architecture Notes

The `LinkGroup` embraces the Hybrid design philosophy of the Reactive Attribute System:

-   **`AttributeProcessor`**: The **subject** (Character, Item). It has state, durations, and values that change over time.
    
-   **`StatBlock`**: The **message/rule** (Buff, Status Modifier). It has no state of its own; it's just a set of instructions (e.g., "+10 Strength").
    
-   **`LinkGroup`**: The **glue** and **distributor**. It connects the rules (`StatBlocks`) to the subjects (`Processors`) in a scalable way, completely free of synchronization bugs.
