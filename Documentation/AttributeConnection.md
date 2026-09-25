# AttributeConnection Class Documentation

## Overview

The `AttributeConnection` class is a pivotal component in the **Reactive Attribute System**, responsible for managing "Remote Modifiers." It acts as a persistent, self-sustaining link that applies a modifier to a target attribute located on a different `Entity` (e.g., a Sword applying a buff to its Owner's Strength).

Unlike local modifiers which are applied directly, remote modifiers depend on a **Context Path** (e.g., `Owner` -> `EquippedWeapon`), where each step is a provider alias registered with `RegisterExternalProvider`. The `AttributeConnection` automatically monitors this path. If the path topology changes (e.g., the Owner changes, or the weapon is unequipped), the connection reacts by moving the modifier to the new target or entering a pending state.

This class implements `IDisposable`, making it a "Handle" that controls the lifecycle of the modifier application. You rarely create one yourself: `Entity.AddModifier(sourceId, modifier, attribute, providerPath)` creates it whenever `providerPath` is not empty (for example for a `StatBlock` modifier with a `TargetPath`) and returns it as the handle.

## Key Features

-   **Reactive Path Resolution:** Uses `UniRx` to observe the first provider on the path and hands the rest of the path to that provider, which does the same (one connection per step). It handles dynamic changes in the object graph (e.g., `A` links to `B`, then `A` links to `C`).
    
-   **Self-Sustaining Lifecycle:** Once created, the connection keeps itself alive via its internal subscriptions. It does not require an external manager to "tick" or update it.
    
-   **Automatic Cleanup:** When the connection is disposed (or the path breaks), it automatically removes the modifier from the last known valid target.
    
-   **Handle-Based Architecture:** The connection _is_ the handle. Disposing of the connection object removes the modifier.
    

## Class Definition

The class lives in the `ReactiveSolutions.AttributeSystem.Core` namespace.

```csharp
public class AttributeConnection : IDisposable

```

### Constructor

```csharp
public AttributeConnection(
    Entity localProcessor,
    List<SemanticKey> providerPath,
    SemanticKey targetAttribute,
    IAttributeModifier modifier,
    string sourceId
)

```

-   **`localProcessor`**: The `Entity` where the path resolution begins (the "Source" of the modifier).
    
-   **`providerPath`**: A list of `SemanticKey`s (provider aliases) representing the steps to traverse to find the target (e.g., `{ Links.Owner, Links.RightHand }`). It must contain at least one key; for a local modifier, call `Entity.AddModifier` without a path.
    
-   **`targetAttribute`**: The name of the attribute to modify on the final entity found at the end of the path.
    
-   **`modifier`**: The actual `IAttributeModifier` logic/payload to apply.
    
-   **`sourceId`**: A string identifier for the source. It is passed along each step of the path but not stored on the attribute: tools such as the Attribute Debugger show the modifier's own `SourceId`. There is no removal by ID; dispose the connection (the handle) instead.
    

## Internal Logic

### 1. Connection Establishment (constructor)

Upon instantiation, the connection subscribes to `localProcessor.ObserveProvider(providerPath[0])`. This stream emits the entity currently registered under the first key of the path (or `null`), and then every change to that registration.

### 2. Path Resolution (one step at a time)

The connection only watches the first step of the path. When that provider exists, it hands the rest of the path to it by calling `provider.AddModifier(sourceId, modifier, targetAttribute, remainingPath)`:

-   If steps remain, that call creates another `AttributeConnection` on the provider, which watches the next step, and so on.
    
-   At the last step, the modifier is added to the target attribute directly.
    
-   Because each step is watched by its own connection, a change anywhere along the path only rebuilds the connections after that point.
    

`PathConnection`, the base class of `TagConnection` (which `StatBlock` uses for remote tags), works differently: it resolves the whole path in a single observable chain and moves the tag when the entity at the end of the path changes.

### 3. Application (`Entity.AddModifier`)

When the first provider changes (or disappears):

1.  **Apply:** If a provider exists, the modifier is applied through it as described above. If it is `null`, nothing is applied until a provider is registered again.
    
    -   _Note:_ At the last step, `Entity.AddModifier` uses `GetOrCreateAttribute`, so the target attribute is created (with a base value of 0) if it doesn't exist yet.
        
    -   _Note:_ Exceptions thrown while applying are caught and logged as `[AttributeConnection] Failed to apply modifier to provider '<key>': ...`.
        
2.  **Cleanup:** The previous application, held in a `SerialDisposable`, is then disposed. This removes the modifier from the old target (or disposes the nested connection that holds it).
    

A `LogicModifier` reads its logic's attribute inputs from its `Context` entity, or, without one, from the entity whose attribute is modified (the one `GetMagnitude` receives). StatBlocks set the context to the entity the block was applied to: a sword's StatBlock modifier on its Owner's Damage reads `Strength` from the sword, and needs the path `Owner` in the input's reference to read the Owner's Strength. A missing attribute reads as 0.

### 4. Lifecycle (`Dispose`)

When `Dispose()` is called:

1.  The subscription to the first provider is disposed (`_topologySubscription.Dispose()`). This stops the connection from reacting to future changes.
    
2.  The current application is disposed (`_modifierHandle.Dispose()`), which removes the modifier from its target and disposes any nested connections further along the path.
    
3.  Further calls to `Dispose()` do nothing.
    

## Usage Example

`Links.Owner` and `Stats.Health` are keys from classes generated from your KeyDomains (see [Semantic Keys](Semantic%20Keys.md)). `LogicModifier` and `ValueLogic` are in the `ReactiveSolutions.AttributeSystem.Core.Modifiers` namespace.

```csharp
// Scenario: A "Curse" component wants to apply -10 Health to the "Owner" of this object.

// 1. Define the modifier
var modifier = new LogicModifier(new ValueLogic(-10f), ModifierType.Additive, sourceId: "Curse");

// 2. Define the path (Look for "Owner")
var path = new List<SemanticKey> { Links.Owner };

// 3. Create the connection (This immediately starts trying to find the Owner)
// The first argument is the Entity of the Cursed Item itself.
var connection = new AttributeConnection(itemEntity, path, Stats.Health, modifier, "CurseSource");
// Equivalent, and what StatBlocks do for a modifier with a TargetPath:
// var connection = itemEntity.AddModifier("CurseSource", modifier, Stats.Health, path);

// ... Time passes ...
// The item is picked up by a Player. The 'Owner' link is established:
// itemEntity.RegisterExternalProvider(Links.Owner, playerEntity);
// The Connection automatically detects this and applies -10 Health to the Player.

// ... Later ...
// The curse is lifted.
connection.Dispose(); 
// The -10 Health modifier is removed from the Player. The connection is dead.
```
