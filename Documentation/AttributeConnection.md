# AttributeConnection Class Documentation

## Overview

The `AttributeConnection` class is a pivotal component in the **Reactive Attribute System**, responsible for managing "Remote Modifiers." It acts as a persistent, self-sustaining link that applies a modifier to a target attribute located on a different `AttributeProcessor` (e.g., a Sword applying a buff to its Owner's Strength).

Unlike local modifiers which are applied directly, remote modifiers depend on a **Context Path** (e.g., `Owner` -> `EquippedWeapon`). The `AttributeConnection` automatically monitors this path. If the path topology changes (e.g., the Owner changes, or the weapon is unequipped), the connection reacts by moving the modifier to the new target or entering a pending state.

This class implements `IDisposable`, making it a "Handle" that controls the lifecycle of the modifier application.

## Key Features

-   **Reactive Path Resolution:** Uses `UniRx` to recursively observe the provider path. It handles dynamic changes in the object graph (e.g., `A` links to `B`, then `A` links to `C`).
    
-   **Self-Sustaining Lifecycle:** Once created, the connection keeps itself alive via its internal subscriptions. It does not require an external manager to "tick" or update it.
    
-   **Automatic Cleanup:** When the connection is disposed (or the path breaks), it automatically removes the modifier from the last known valid target.
    
-   **Handle-Based Architecture:** The connection _is_ the handle. Disposing of the connection object removes the modifier.
    

## Class Definition

```
public class AttributeConnection : IDisposable

```

### Constructor

```
public AttributeConnection(
    AttributeProcessor root,
    List<SemanticKey> path,
    SemanticKey targetAttribute,
    IAttributeModifier modifier,
    string sourceId
)

```

-   **`root`**: The `AttributeProcessor` where the path resolution begins (the "Source" of the modifier).
    
-   **`path`**: A list of `SemanticKey`s representing the steps to traverse to find the target (e.g., `["Owner", "Hireling"]`).
    
-   **`targetAttribute`**: The name of the attribute to modify on the final processor found at the end of the path.
    
-   **`modifier`**: The actual `IAttributeModifier` logic/payload to apply.
    
-   **`sourceId`**: A string identifier for the source (used for debugging or bulk removal by ID, though the Handle approach is preferred).
    

## Internal Logic

### 1. Connection Establishment (`Connect`)

Upon instantiation, the connection starts a `ResolvePathRecursively` observable chain. This chain monitors the `AttributeProcessor`'s provider registry.

### 2. Path Resolution (`ResolvePathRecursively`)

This method constructs a dynamic observable stream:

-   It observes the provider at the current index of the path.
    
-   If that provider changes, it `Switch`es to a new observable for the _rest_ of the path.
    
-   When the end of the path is reached, it emits the final `AttributeProcessor`.
    
-   If any link in the chain is broken (null), it emits `null`.
    

### 3. Application (`ApplyToTarget`)

When the resolved path emits a new target processor (or null):

1.  **Cleanup:** If a modifier was previously applied to a different target, it is removed from that old target.
    
2.  **Apply:** If a new valid target exists, the modifier is added to it.
    
    -   _Note:_ It uses `GetOrCreateAttribute` to ensure the target attribute exists before modifying it.
        

### 4. Lifecycle (`Dispose`)

When `Dispose()` is called:

1.  The reactive subscription to the path is severed (`_pathSubscription.Dispose()`). This stops the connection from reacting to future changes.
    
2.  The modifier is immediately removed from the `_currentTarget` (if one exists).
    
3.  References are cleared to allow garbage collection.
    

## Usage Example

```
// Scenario: A "Curse" component wants to apply -10 Health to the "Owner" of this object.

// 1. Define the modifier
var modifier = new StaticAttributeModifier(new ModifierArgs("Curse", ModifierType.Additive, 0, new List<ValueSource> { ValueSource.Const(-10f) }));

// 2. Define the path (Look for "Owner")
var path = new List<SemanticKey> { new SemanticKey("Owner") };

// 3. Create the connection (This immediately starts trying to find the Owner)
// The 'root' is the processor on the Cursed Item itself.
var connection = new AttributeConnection(itemProcessor, path, new SemanticKey("Health"), modifier, "CurseSource");

// ... Time passes ...
// The item is picked up by a Player. The 'Owner' link is established.
// The Connection automatically detects this and applies -10 Health to the Player.

// ... Later ...
// The curse is lifted.
connection.Dispose(); 
// The -10 Health modifier is removed from the Player. The connection is dead.
```
