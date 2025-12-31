# AttributeProcessor Class Documentation

## Overview

The `AttributeProcessor` is the core engine of the Reactive Attribute System. It acts as the central container for a collection of `Attribute`s (like Health, Strength, Damage) and serves as the registry for external dependency links (e.g., linking a "Sword" processor to a "Player" processor).

It is responsible for:

1.  **Storage:** Holding the state of all local attributes.
    
2.  **Resolution:** Resolving complex paths to finding attributes on other entities (e.g., resolving `Owner` -> `Hireling` -> `Strength`).
    
3.  **Modification:** Providing the API to add modifiers to attributes, managing the creation of `AttributeConnection`s for remote modifications.
    

## Key Features

-   **Reactive Storage:** Uses `ReactiveDictionary` to store attributes, allowing systems (like UI) to reactively detect when new attributes are added dynamically.
    
-   **Dependency Injection (Providers):** Allows registering other processors as "Providers" (e.g., `RegisterExternalProvider("Owner", playerProcessor)`), enabling cross-entity stat scaling.
    
-   **Handle-Based Modification:** Adding a modifier returns an `IDisposable` handle. Disposing this handle removes the modifier, ensuring clean lifecycle management without relying on string IDs.
    
-   **Lazy Resolution:** Can accept modifiers or observers for attributes/providers that do not exist yet. It waits for them to appear and connects automatically.
    

## Class Definition

```
public class AttributeProcessor

```

## Public API

### 1. Attribute Management

Methods for retrieving or creating the attribute objects.

-   **`GetAttribute(SemanticKey name)`**
    
    -   Retrieves a local attribute. Returns `null` if not found.
        
-   **`GetOrCreateAttribute(SemanticKey name, float defaultBaseIfMissing = 0f)`**
    
    -   Safely gets an attribute, creating it with the specified base value if it doesn't exist. Ideal for ensuring a stat exists before modifying it.
        
-   **`SetOrUpdateBaseValue(SemanticKey name, float newBase)`**
    
    -   The primary way to initialize stats. Creates the attribute if missing and sets its base (unmodified) value.
        
-   **`Attributes`** (Property)
    
    -   Access to the underlying `IReadOnlyReactiveDictionary`. Useful for debugging or listing all stats.
        

### 2. Reactive Access

Methods for observing values, even across complex chains.

-   **`GetAttributeObservable(SemanticKey name, List<SemanticKey> providerPath = null)`**
    
    -   Returns an `IObservable<Attribute>` that resolves to the target attribute.
        
    -   **Local:** If `providerPath` is null/empty, observes the local dictionary.
        
    -   **Remote:** If `providerPath` is provided, recursively observes the provider chain.
        
    -   _Usage:_ Valid for UI elements waiting for a stat that might not exist yet (e.g., waiting for "Mana" to be added to the character).
        
-   **`OnAttributeAdded`**
    
    -   Stream that fires whenever a new `Attribute` is created locally.
        

### 3. Modifier Management (The Handle System)

The processor manages the application of modifiers.

-   **`IDisposable AddModifier(string sourceId, IAttributeModifier modifier, SemanticKey attributeName)`**
    
    -   **Local Application:** Adds a modifier to a local attribute.
        
    -   **Returns:** An `IDisposable`. Call `.Dispose()` on this object to remove the modifier.
        
    -   _Note:_ If the attribute doesn't exist, it is auto-created.
        
-   **`IDisposable AddModifier(string sourceId, IAttributeModifier modifier, SemanticKey attributeName, List<SemanticKey> providerPath)`**
    
    -   **Remote Application:** Adds a modifier to a target located at the end of `providerPath`.
        
    -   **Returns:** An `AttributeConnection` (which implements `IDisposable`). This object keeps the link alive. Disposing it removes the modifier from wherever it is currently applied.
        

### 4. Provider Linking (Context)

Methods for establishing relationships between entities.

-   **`RegisterExternalProvider(SemanticKey key, AttributeProcessor processor)`**
    
    -   Registers another processor under an alias (e.g., linking the Player as "Owner").
        
    -   Triggers any pending observers or connections waiting for this key.
        
-   **`UnregisterExternalProvider(SemanticKey key)`**
    
    -   Removes a link. Any `AttributeConnection` traversing this link will momentarily lose its target (applying nothing) until the link is re-established.
        
-   **`ObserveProvider(SemanticKey key)`**
    
    -   Returns an `IObservable<AttributeProcessor>` that fires whenever the specific provider is registered, changed, or unregistered (resolving to null).
        

## Usage Example

```
var player = new AttributeProcessor();
var sword = new AttributeProcessor();

// 1. Setup Stats
player.SetOrUpdateBaseValue(new SemanticKey("Strength"), 10f);
sword.SetOrUpdateBaseValue(new SemanticKey("Damage"), 5f);

// 2. Link Context
sword.RegisterExternalProvider(new SemanticKey("Owner"), player);

// 3. Add Modifier (Sword Damage scales with Owner Strength)
// We want to add to "Damage" (Local), based on "Strength" (Remote Source).
// Note: This example uses a Modifier that reads from a remote source, 
// but is applied LOCALLY to the sword.
var scalingMod = new LinearModifier(...); 
IDisposable handle = sword.AddModifier("Scaling", scalingMod, new SemanticKey("Damage"));

// 4. Cleanup
// When the sword is destroyed or unequipped:
handle.Dispose();

```
