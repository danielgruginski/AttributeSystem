
# EntityController Documentation

## Overview

The `EntityController` is the primary `MonoBehaviour` entry point for the Reactive Attribute System. It acts as a lightweight Unity Component wrapper around the pure C# `Entity` engine.

It is designed to be placed on any GameObject that has stats (e.g., Player, Enemy, Weapon, Vehicle). Other components (like UI health bars, combat scripts, or inventory managers) interact with this controller to gain access to the underlying reactive stat engine.

## Key Features

-   **Data-Driven Initialization:** The underlying `Entity` is created automatically, on the first access to `Instance` or in `Awake` (whichever comes first), and populated from its profiles: a JSON profile picked in **Profile Id** and a profile authored in the **Profile** field (see [Inspector Fields](#inspector-fields)). Without either, the entity starts empty.
    
-   **Component-Based:** Allows GameObjects to participate in the attribute system natively within Unity's scene hierarchy.
    
-   **Direct Engine Access:** By design, this component does not duplicate or wrap the core API. It cleanly exposes the `Instance` property, ensuring that your logic interacts directly with the pure C# engine.
    
-   **Automatic Cleanup:** In `OnDestroy`, the controller disposes its `Entity`: the innate StatBlocks and nested entities from its profile are disposed, and its attributes stop updating and release their subscriptions to other entities.
    

## Class Definition

```csharp
public class EntityController : MonoBehaviour
{
    // ...
}

```

Namespace: `ReactiveSolutions.AttributeSystem.Unity`.

## Inspector Fields

-   **Profile Id** (`string ProfileId`)
    
    -   A profile JSON file under `Resources/Data/EntityProfiles`, picked from a dropdown (create them with **Tools > Attribute System > Entity Profile Editor**, and click **Edit** next to the dropdown to open the picked one). Applied first. If the file can't be loaded, an error is logged and only the inline profile is applied.
        
-   **Profile** (`EntityProfile Profile`)
    
    -   A profile authored right in the Inspector: base attributes, tags, link groups, innate StatBlocks (by ID or inline), nested entities and pointers. Applied after the **Profile Id**'s, so its base values win.
        

Both are read once, when the entity is created; changing them later has no effect on it. See [EntityProfile](EntityProfile.md).

-   **Tick Status Effects** (`bool TickStatusEffects`, on by default)
    
    -   Advances the entity's [status effects](Status%20Effects.md) every frame, by `Time.deltaTime`, so their durations and ticks are in seconds. Turn it off to advance them yourself, e.g. once per turn with `Instance.TickStatusEffects(1)`.

## Public API

### Core Access

-   **`Entity Instance`**
    
    -   The raw C# engine managed by this component.
        
    -   Use this to access attributes, add modifiers, manage tags, and establish links.
        
    -   Created on first access, so it is safe to use from other components' `Awake`, regardless of script execution order. Disposed in `OnDestroy`.
        
-   **`void InitializeEntity()`**
    
    -   Creates the `Entity`, then applies the **Profile Id**'s JSON profile (if set) and the inline **Profile**.
        
    -   _Note:_ This is called automatically in `Awake()` or by the first access to `Instance`, whichever comes first, and does nothing once the entity exists. You rarely need to call it yourself.
        

## Usage Examples

Because the wrapper methods have been removed, all interaction goes directly through the `.Instance` property.

The examples are methods of a `MonoBehaviour` and assume `using ReactiveSolutions.AttributeSystem.Core;`, `using ReactiveSolutions.AttributeSystem.Core.Modifiers;`, `using ReactiveSolutions.AttributeSystem.Unity;`, `using UniRx;` and `using Game.Constants;`. `Stats.Health` and `Links.Owner` are keys from classes generated from KeyDomains (see [Semantic Keys](Semantic%20Keys.md)).

### 1. Listening to a Stat (in a Character Script)

```csharp
void Start()
{
    var controller = GetComponent<EntityController>();
    
    // Access the core Instance to listen for changes
    controller.Instance.ObserveValue(Stats.Health) // Final value; emits once the attribute exists
        .Subscribe(currentHealth => 
        {
            if (currentHealth <= 0) Die();
        })
        .AddTo(this);
}

```

### 2. Linking (Equipping an Item)

When one entity needs to scale based on another's stats (e.g., a sword scaling with its owner's strength), you register the owner as an external provider directly on the weapon's `Instance`. (The `AttributeContextLinker` component does the same from the Inspector.)

```csharp
public void Equip(EntityController weapon)
{
    var owner = GetComponent<EntityController>();

    // Tell the weapon's engine who owns it
    weapon.Instance.RegisterExternalProvider(Links.Owner, owner.Instance);
}

```

### 3. Adding a Runtime Modifier

```csharp
public void TakeDamage(float amount)
{
    var controller = GetComponent<EntityController>();
    
    // A modifier that adds -amount to Health
    var damageMod = new LogicModifier(new ValueLogic(-amount), ModifierType.Additive, sourceId: "CombatSystem");
    
    // AddModifier returns an IDisposable handle: disposing it removes the modifier again
    controller.Instance.AddModifier("CombatSystem", damageMod, Stats.Health);
}

```

## Architecture Note

Previous iterations of the system included "bridge" methods on the `EntityController` (such as `AddAttribute`, `GetAttributeObservable` or `LinkProvider`, and a `Processor` property). These have been **removed**. For example, `controller.AddAttribute(key, value)` is now `controller.Instance.SetOrUpdateBaseValue(key, value)`.

To maintain a strict separation of concerns, the `EntityController` is now solely responsible for Unity lifecycle integration and applying its profiles. All gameplay logic and stat manipulation must route through `controller.Instance`.
