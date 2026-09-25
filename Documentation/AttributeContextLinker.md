# AttributeContextLinker Documentation

## Overview

The `AttributeContextLinker` is a Unity `MonoBehaviour` designed to manage the context connections (Dependency Injection) for an `EntityController`.

In the Reactive Attribute System, entities often need to read stats from other entities (e.g., a Sword reading its Owner's Strength). This component is the bridge that tells the system: _"This Sword's 'Owner' is that Player."_

## Key Features

-   **Inspector Configuration:** Allows you to drag-and-drop another `EntityController` to link it as a provider. Each linker creates one link (one alias); add one linker per alias.
    
-   **Dynamic Linking:** Supports linking at runtime via script, which is essential for equipping items or spawning minions.
    
-   **Automatic Registration:** In `Awake` (when **Link On Awake** is checked), it calls `RegisterExternalProvider` on the Receiver's `Entity`. In `OnDestroy`, it removes the link again.
    

## Class Definition

```csharp
[AddComponentMenu("Attribute System/Attribute Context Linker")]
public class AttributeContextLinker : MonoBehaviour
{
    // ...
}

```

Namespace: `ReactiveSolutions.AttributeSystem.Unity`.

## Inspector Usage

1.  **Receiver:** The `EntityController` that needs the external data (e.g., the Sword). _Default:_ If empty, the `EntityController` on this GameObject.
    
2.  **Provider:** The `EntityController` of the other entity (e.g., the Player).
    
3.  **Alias:** The key the Receiver uses in its paths to refer to the Provider (e.g., `Owner`, `Target`, `Vehicle`), picked from a dropdown.
    
4.  **Link On Awake:** If checked (the default), the link is established in `Awake`. Uncheck it when the Provider is assigned from script; `LinkContext()` reports a missing Receiver, Provider or Alias as an assertion failure.
    

## Public API

### Methods

-   **`void LinkContext()`**
    
    -   Registers the Provider's `Entity` on the Receiver's `Entity` under the Alias, replacing any provider already registered under that alias. If Receiver is empty, it uses the `EntityController` on this GameObject. Does nothing (after a failed assertion) if the Receiver, Provider or Alias is missing.
        
    -   Calling it again replaces this component's previous link. When only the provider changes, the new provider replaces the old one directly, so the alias is never briefly missing.
        
-   **`void SetProvider(EntityController provider)`**
    
    -   Sets the Provider and re-links (using this GameObject's `EntityController` if Receiver is empty). Passing `null` removes the link, e.g. when the weapon is dropped.
        
    -   _Example:_ When a player picks up a weapon:
        
        ```csharp
        weapon.GetComponent<AttributeContextLinker>().SetProvider(playerController);
        
        ```
        
-   **`void SetReceiver(EntityController receiver)`**
    
    -   Moves the link: removes it from the previous Receiver, then links to the new one if a Provider is set.
        
-   **`void SetAlias(SemanticKey alias)`**
    
    -   Changes the Alias. An existing link moves to the new alias.
        
-   **`void RemoveLink()`**
    
    -   Removes the link this component registered (`UnregisterExternalProvider`), unless another component has registered a different provider under the same alias since. Does nothing if this component isn't linked. Called automatically in `OnDestroy`.
        
    -   Any `AttributeConnection`s traversing this link remove their modifiers until a new link is established, and attribute references through the alias read as 0.
        

## Use Cases

`Links.Summoner` and `Links.Rider` below are keys from a class generated from a `Links` KeyDomain (see [Semantic Keys](Semantic%20Keys.md)).

### 1. Equipment (The "Owner" Pattern)

-   **Scenario:** A Sword has a modifier `Damage += 50% Owner.Strength`.
    
-   **Setup:**
    
    1.  The Sword prefab has an `AttributeContextLinker` with **Alias** `Owner`, the Sword's own `EntityController` as **Receiver**, and **Link On Awake** unchecked.
        
    2.  When the Player equips the Sword, the Inventory System calls: `swordLinker.SetProvider(playerController);`
        
    3.  **Result:** The connection is live. If the Player's Strength changes, the Sword's Damage updates immediately.
        

### 2. Minions / Summons

-   **Scenario:** A Necromancer summons a Skeleton. The Skeleton's Health scales with the Necromancer's Intelligence.
    
-   **Setup:**
    
    1.  Spawn the Skeleton prefab (its linker set up like the Sword's above).
        
    2.  `skeletonLinker.SetAlias(Links.Summoner);` (or set the Alias in the Inspector), then `skeletonLinker.SetProvider(necromancerController);`
        
    3.  Skeleton stats use the path `new List<SemanticKey> { Links.Summoner }` (a **Context Path** of `Summoner` in the Inspector) to read Intelligence.
        

### 3. Vehicles / Mounts

-   **Scenario:** A Player mounts a Horse. The Horse's speed is boosted by the Player's RidingSkill.
    
-   **Setup:**
    
    1.  `horseLinker.SetProvider(playerController);` (with the Alias set to `Rider`)
        
    2.  Horse stats use path `new List<SemanticKey> { Links.Rider }`.
