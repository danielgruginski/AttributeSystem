
# Entity Class Documentation

## Overview

The `Entity` is the core engine of the Reactive Attribute System. It acts as the central container for a collection of `Attribute`s (like Health, Strength, Damage) and serves as the registry for external dependency links (e.g., linking a "Sword" entity to a "Player" entity).

It is responsible for:

1.  **Storage:** Holding the state of all local attributes.
    
2.  **Resolution:** Resolving complex paths to finding attributes on other entities (e.g., resolving `Owner` -> `Hireling` -> `Strength`).
    
3.  **Modification:** Providing the API to add modifiers to attributes, managing the creation of `AttributeConnection`s for remote modifications.
    
4.  **Tagging:** Managing Entity Tags for categorization and status effects.
    
5.  **Aliasing:** Manages Attribute Pointers.
    
6.  **Composition:** Applying `EntityProfile` blueprints and holding `LinkGroup`s.
    

## Key Features

-   **Reactive Storage:** Uses `ReactiveDictionary` to store attributes, allowing systems (like UI) to reactively detect when new attributes are added dynamically.
    
-   **Dependency Injection (Providers):** Allows registering other entities as "Providers" (e.g., `RegisterExternalProvider(Links.Owner, playerEntity)`), enabling cross-entity stat scaling.
    
-   **Handle-Based Modification:** Adding a modifier returns an `IDisposable` handle. Disposing this handle removes the modifier, ensuring clean lifecycle management without relying on string IDs.
    
-   **Lazy Resolution:** Can accept modifiers or observers for attributes/providers that do not exist yet. It waits for them to appear and connects automatically. Meanwhile, values that modifiers, pointers and conditions read from a missing attribute or provider count as 0.
    

Keys such as `Stats.Strength` and `Links.Owner` on this page come from classes generated from KeyDomains (see [Semantic Keys](Semantic%20Keys.md)).

## Class Definition

```csharp
namespace ReactiveSolutions.AttributeSystem.Core
{
    public class Entity : IDisposable
    {
        // ...
    }
}

```

## Public API

### 1. Attribute Management

Methods for retrieving or creating the attribute objects.

-   **`GetAttribute(SemanticKey name)`**
    
    -   Retrieves a local attribute. Returns `null` if not found.
        
-   **`GetAttribute(SemanticKey name, List<SemanticKey> providerPath)`**
    
    -   Retrieves an attribute through a provider path (e.g., `Owner`'s `Strength`). Returns `null` if a provider or the attribute is missing.
        
-   **`GetOrCreateAttribute(SemanticKey name, float defaultBaseIfMissing = 0f)`**
    
    -   Safely gets an attribute, creating it with the specified base value if it doesn't exist. Ideal for ensuring a stat exists before modifying it.
        
-   **`SetOrUpdateBaseValue(SemanticKey key, float value)`**
    
    -   The primary way to initialize stats. Creates the attribute if missing and sets its base (unmodified) value.
        
-   **`Attributes`** (Property)
    
    -   Access to the underlying `IReadOnlyReactiveDictionary<SemanticKey, Attribute>`. Useful for debugging or listing all stats.
        

### 2. Pointer Management

-   `IDisposable SetPointer(SemanticKey alias, SemanticKey target, List<SemanticKey> path = null)`: Makes `alias` read the final value of `target` (a local attribute, or one at the end of `path`). Returns a handle.
    
    -   If `alias` already exists as a concrete attribute, it is kept: while the pointer is active, its pipeline starts from the target's value instead of its own base value (which is shadowed, not lost), and its own modifiers still apply on top.
        
    -   Pointers stack: the newest pointer on an alias is the active one. A missing target reads as 0.
        
    -   Prevents local circular dependencies: pointing an alias to itself (`A -> A`) logs a warning, and a cycle such as `A -> B -> A` or `A -> B -> C -> A` logs an error. In both cases the pointer is not created. Only local pointers are checked; a pointer with a provider path targets another entity's attribute and is never treated as a local cycle.
        
-   To remove a pointer, dispose the handle returned by `SetPointer`. This does **not** affect the Target attribute; the alias falls back to the previous pointer on its stack, or to its own base value.
    

### 3. Tag Management

The entity now includes a Tag Manager to handle entity states.

-   **`IReadOnlyReactiveDictionary<SemanticKey, int> Tags`**
    
    -   The collection of active tags and their reference counts.
        
-   **`void AddTag(SemanticKey tag)`**
    
    -   Adds a tag. Increments the reference count if it already exists.
        
-   **`void RemoveTag(SemanticKey tag)`**
    
    -   Decrements the reference count. Removes the tag if count drops to zero.
        
-   **`bool HasTag(SemanticKey tag)`**
    
    -   Checks if the tag is currently active (count > 0).
        

### 4. Reactive Access

Methods for observing values, even across complex chains.

-   **`GetAttributeObservable(SemanticKey name, List<SemanticKey> providerPath = null)`**
    
    -   Returns an `IObservable<Attribute>` that resolves to the target attribute. It emits the attribute once it exists (and again if it is replaced).
        
    -   **Local:** If `providerPath` is null/empty, observes the local dictionary.
        
    -   **Remote:** If `providerPath` is provided, recursively observes the provider chain. Emits `null` while a provider on the path is missing.
        
    -   _Usage:_ Valid for UI elements waiting for a stat that might not exist yet (e.g., waiting for "Mana" to be added to the character).
        
-   **`ObserveValue(SemanticKey name, List<SemanticKey> providerPath = null)`**
    
    -   Returns an `IObservable<float>` stream of the attribute's final value: the current value on subscribe, then every change. Emits nothing while the attribute (or a provider on the path) is missing, and follows the attribute across provider changes. This is usually what UI and gameplay code want.
        
-   **`ObserveAttributeValue(this IObservable<Entity> entities, SemanticKey name)`** (extension method, `EntityObservableExtensions`)
    
    -   Follows whichever entity the source currently holds and emits that entity's attribute values. Switching entities drops the previous subscription; a `null` entity emits nothing.
        
    -   _Usage:_ `sword.ObserveProvider(Links.Owner).ObserveAttributeValue(Stats.Health)` streams the Health of whoever currently owns the sword. `AttributeUIBehaviour` uses it to follow its controller.
        
-   **`Attributes.ObserveAdd()`**
    
    -   Stream (from UniRx's `IReadOnlyReactiveDictionary`) that fires whenever a new `Attribute` is created locally.
        

### 5. Modifier Management (The Handle System)

The entity manages the application of modifiers.

-   **`IDisposable AddModifier(string sourceId, IAttributeModifier modifier, SemanticKey attributeName)`**
    
    -   **Local Application:** Adds a modifier to a local attribute.
        
    -   **Returns:** An `IDisposable`. Call `.Dispose()` on this object to remove the modifier.
        
    -   _Note:_ If the attribute doesn't exist, it is auto-created.
        
-   **`IDisposable AddModifier(string sourceId, IAttributeModifier modifier, SemanticKey attributeName, List<SemanticKey> providerPath)`**
    
    -   **Remote Application:** Adds a modifier to a target located at the end of `providerPath`. The modifier is applied once every provider on the path exists, and follows provider changes (removed while a provider is missing, re-applied when one is registered).
        
    -   **Returns:** An `AttributeConnection` (which implements `IDisposable`). This object keeps the link alive. Disposing it removes the modifier from wherever it is currently applied.
        

### 6. Provider Linking (Context)

Methods for establishing relationships between entities.

-   **`RegisterExternalProvider(SemanticKey key, Entity entity)`**
    
    -   Registers another entity under an alias (e.g., linking the Player as `Links.Owner`), replacing any provider already registered under that key.
        
    -   Triggers any pending observers or connections waiting for this key.
        
-   **`UnregisterExternalProvider(SemanticKey key)`**
    
    -   Removes a link. Any `AttributeConnection` traversing this link will momentarily lose its target (applying nothing) until the link is re-established, and values read through the link count as 0.
        
-   **`ObserveProvider(SemanticKey key)`**
    
    -   Returns an `IObservable<Entity>` that emits the current provider (or null) on subscribe, then fires whenever the specific provider is registered, changed, or unregistered (resolving to null).
        

### 7. Profiles & Link Groups

-   **`ApplyProfile(EntityProfile profile)`**
    
    -   Initializes the entity from a blueprint, in this order: templates, base attributes, pools, innate tags, link groups, nested entities (registered as providers), pointers, innate StatBlocks. Entries with an unassigned key (`SemanticKey.None`) are skipped, and a `null` profile is ignored. Templates, nested profiles and StatBlocks given by ID are loaded from their JSON files; a profile that nests or builds on itself is skipped with an error. See [EntityProfile](EntityProfile.md); profiles can also be built in code with `ProfileBuilder` (see [Fluent Builders](Fluent%20Builders.md)).
        
    -   Each profile is applied once per entity, directly or as a template: a template that several of the entity's profiles build on is applied the first time only, and applying a profile the entity already has logs a warning and does nothing.
        
-   **`AddPool(SemanticKey resource, ValueSource max, PoolMaxChange onMaxChange = KeepPercent)`** / **`GetPool(SemanticKey resource)`**
    
    -   Make an attribute (e.g. Health) a pool: an amount that is spent and restored, between 0 and `max` (e.g. MaxHealth). `GetPool` returns `null` for an attribute that isn't one. See [Resource Pools](Resource%20Pools.md).
        
-   **`Implements(string profileId)`** / **`Implements(EntityProfile profile)`**
    
    -   Whether the profile (e.g. `"Templates/Character"`) was applied to this entity, directly or as a template.
        
-   **`ParentKey`**
    
    -   The key under which this entity reaches the entity it is nested in (e.g. `Links.Owner`), from its profile's `ParentKey`. When a profile's nested entity names one, `ApplyProfile` registers the parent as the child's provider under that key.
        
-   **`GetOrCreateLinkGroup(SemanticKey key)`** / **`GetLinkGroup(SemanticKey key)`**
    
    -   Return the entity's `LinkGroup` for that key (e.g., `Groups.Inventory`). `GetLinkGroup` returns `null` if the group doesn't exist. See [LinkGroup](LinkGroup.md).
        

### 8. Lifecycle

-   **`void Dispose()`** / **`bool IsDisposed`**
    
    -   Disposes the innate StatBlocks and nested entities created by `ApplyProfile`, and all attributes: they keep their last value but stop updating and release their subscriptions to other entities.
        
    -   A disposed entity ignores new modifiers and provider (un)registrations. `EntityController` disposes its entity automatically in `OnDestroy`.
        

## Usage Example

```csharp
using System;
using Game.Constants;                                   // Namespace of your generated key classes
using ReactiveSolutions.AttributeSystem.Core;
using ReactiveSolutions.AttributeSystem.Core.Modifiers; // LogicModifier, LinearLogic

var player = new Entity();
var sword = new Entity();

// 1. Setup Stats
player.SetOrUpdateBaseValue(Stats.Strength, 10f);
sword.SetOrUpdateBaseValue(Stats.Damage, 5f);

// 2. Link Context
sword.RegisterExternalProvider(Links.Owner, player);

// 3. Add Modifier (Sword Damage scales with Owner Strength)
// We want to add to "Damage" (Local), based on "Strength" (Remote Source).
// Note: This example uses a Modifier that reads from a remote source, 
// but is applied LOCALLY to the sword.
var scalingMod = new LogicModifier(new LinearLogic
{
    Input = ValueSource.FromAttribute(Stats.Strength, Links.Owner), // Owner.Strength, resolved from the sword
    Coefficient = 0.5f,
    Addend = 0f
}, ModifierType.Additive, sourceId: "Scaling");
IDisposable handle = sword.AddModifier("Scaling", scalingMod, Stats.Damage); // Damage: 5 + 10 * 0.5 = 10

// 4. Cleanup
// When the sword is destroyed or unequipped:
handle.Dispose(); // Damage: 5

// When the sword entity itself is discarded, dispose it: its attributes stop
// updating and release their subscriptions to the player.
sword.Dispose();

```
