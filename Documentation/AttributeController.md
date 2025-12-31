# AttributeController Documentation

## Overview

The `AttributeController` is the primary `MonoBehaviour` entry point for the Reactive Attribute System. It acts as a Unity Component wrapper around the pure C# `AttributeProcessor`.

It is designed to be placed on any GameObject that has stats (e.g., Player, Enemy, Weapon, Vehicle). Other components (like `StatBlockLinker`, `AttributeUIBehaviour`, or your custom combat scripts) interact with this controller to read or modify stats.

## Key Features

-   **Lazy Initialization:** The underlying `Processor` is created automatically the first time it is accessed. You don't need to manually call an "Init" function.
    
-   **Component-Based:** Allows GameObjects to participate in the attribute system.
    
-   **Bridge to Logic:** Provides helper methods to easily access the `AttributeProcessor` API without needing to cache the processor reference manually.
    

## Class Definition

```
[AddComponentMenu("Attribute System/Attribute Controller")]
public class AttributeController : MonoBehaviour
{
    // ...
}

```

## Public API

### Core Access

-   **`AttributeProcessor Processor`**
    
    -   The raw C# engine managed by this component.
        
    -   Use this if you need advanced features like `AddModifier` directly or complex observable chains.
        

### Attribute Management

-   **`Attribute AddAttribute(SemanticKey key, float baseValue)`**
    
    -   Creates a new attribute (or updates an existing one) with a starting base value.
        
    -   _Usage:_ `controller.AddAttribute(new SemanticKey("Health"), 100f);`
        
-   **`Attribute GetAttribute(SemanticKey key)`**
    
    -   Returns the `Attribute` object if it exists locally. Returns `null` otherwise.
        
    -   _Note:_ Does not resolve remote paths.
        
-   **`IObservable<Attribute> GetAttributeObservable(SemanticKey key)`**
    
    -   Returns a reactive stream that waits for the attribute to exist.
        
    -   Essential for UI initialization where the UI might spawn before the attribute is added.
        

### Context / Links

-   **`void LinkProvider(SemanticKey key, AttributeController provider)`**
    
    -   Links another controller as a "Provider" under an alias.
        
    -   _Example:_ `swordController.LinkProvider(new SemanticKey("Owner"), playerController);`
        
    -   This enables the sword to use modifiers like `Owner.Strength`.
        
-   **`void UnlinkProvider(SemanticKey key)`**
    
    -   Removes the link. Any remote modifiers depending on this link will pause/disconnect.
        

## Usage Examples

### 1. Initialization (in a Character Script)

```
void Start()
{
    var controller = GetComponent<AttributeController>();
    
    // Initialize Stats
    controller.AddAttribute(new SemanticKey("Health"), 100f);
    controller.AddAttribute(new SemanticKey("Mana"), 50f);
    
    // Listen for changes
    controller.GetAttributeObservable(new SemanticKey("Health"))
        .SelectMany(attr => attr.ReactivePropertyAccess) // Flatten the stream
        .Subscribe(currentHealth => 
        {
            if (currentHealth <= 0) Die();
        })
        .AddTo(this);
}

```

### 2. Linking (Equipping an Item)

```
public void Equip(AttributeController weapon)
{
    // Tell the weapon who owns it
    weapon.LinkProvider(new SemanticKey("Owner"), this.GetComponent<AttributeController>());
}

```

## Deprecation Note

Previous versions of the system included extension methods (`AddFlatMod`, `AddLinearScaling`, etc.) for `AttributeController`. These are now **deprecated** in favor of using `StatBlock`s for data-driven design or interacting directly with `Processor.AddModifier()` for custom code, as the new Handle-based system requires managing the returned `IDisposable`
