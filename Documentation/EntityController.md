
# EntityController Documentation

## Overview

The `EntityController` is the primary `MonoBehaviour` entry point for the Reactive Attribute System. It acts as a lightweight Unity Component wrapper around the pure C# `Entity` engine.

It is designed to be placed on any GameObject that has stats (e.g., Player, Enemy, Weapon, Vehicle). Other components (like UI health bars, combat scripts, or inventory managers) interact with this controller to gain access to the underlying reactive stat engine.

## Key Features

-   **Data-Driven Initialization:** The underlying `Entity` is created and populated automatically on `Awake` using an `EntityProfileSO` blueprint.
    
-   **Component-Based:** Allows GameObjects to participate in the attribute system natively within Unity's scene hierarchy.
    
-   **Direct Engine Access:** By design, this component does not duplicate or wrap the core API. It cleanly exposes the `Instance` property, ensuring that your logic interacts directly with the pure C# engine.
    

## Class Definition

```
[AddComponentMenu("Attribute System/Entity Controller")]
public class EntityController : MonoBehaviour
{
    // ...
}

```

## Public API

### Core Access

-   **`Entity Instance`**
    
    -   The raw C# engine managed by this component.
        
    -   Use this to access attributes, add modifiers, manage tags, and establish links.
        
-   **`void InitializeEntity()`**
    
    -   Creates the `Entity` and `ModifierFactory`, and applies the assigned `EntityProfileSO`.
        
    -   _Note:_ This is called automatically in `Awake()`, but is exposed publicly in case you need to force initialization earlier in your execution order (e.g., from a master bootstrapper script).
        

## Usage Examples

Because the wrapper methods have been removed, all interaction goes directly through the `.Instance` property.

### 1. Listening to a Stat (in a Character Script)

```
void Start()
{
    var controller = GetComponent<EntityController>();
    
    // Access the core Instance to listen for changes
    controller.Instance.GetAttributeObservable(new SemanticKey("Health"))
        .SelectMany(attr => attr.ReactivePropertyAccess) // Flatten the stream
        .Subscribe(currentHealth => 
        {
            if (currentHealth <= 0) Die();
        })
        .AddTo(this);
}

```

### 2. Linking (Equipping an Item)

When one entity needs to scale based on another's stats (e.g., a sword scaling with its owner's strength), you register the owner as an external provider directly on the weapon's `Instance`.

```
public void Equip(EntityController weapon)
{
    // Tell the weapon's engine who owns it
    weapon.Instance.RegisterExternalProvider(new SemanticKey("Owner"), this.Instance);
}

```

### 3. Adding a Runtime Modifier

```
public void TakeDamage(float amount)
{
    var controller = GetComponent<EntityController>();
    
    // Assuming you have a registered logic key for "Damage"
    var damageMod = new FunctionalAttributeModifier(...); 
    
    controller.Instance.AddModifier("CombatSystem", damageMod, new SemanticKey("Health"));
}

```

## Architecture Note

Previous iterations of the system included "bridge" methods on the `EntityController` (such as `AddAttribute` or `LinkProvider`). These have been **removed**.

To maintain a strict separation of concerns, the `EntityController` is now solely responsible for Unity lifecycle integration and SO data injection. All gameplay logic and stat manipulation must route through `controller.Instance`.
