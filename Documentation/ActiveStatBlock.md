# ActiveStatBlock Class Documentation

## Overview

The `ActiveStatBlock` is a runtime handle that represents a collection of modifiers currently applied to an `AttributeProcessor`. It acts as a "receipt" for a `StatBlock` application.

Its primary purpose is **Lifecycle Management**. When you apply a StatBlock (like equipping a sword or casting a buff), the system gives you an `ActiveStatBlock`. When you want to remove those effects (unequipping the sword or the buff expiring), you simply `Dispose()` this object.

## Key Features

-   **Aggregate Disposal:** Holds multiple `IDisposable` handles (one for each modifier in the StatBlock). calling `Dispose()` on the `ActiveStatBlock` triggers disposal for all of them.
    
-   **RAII Pattern:** Follows the "Resource Acquisition Is Initialization" pattern. The existence of this object implies the stats are active. destroying it removes them.
    
-   **Safety:** Prevents "Zombie Stats" (modifiers that persist forever because you lost their reference IDs).
    

## Class Definition

```
public class ActiveStatBlock : IDisposable
{
    // ...
}

```

## Public API

### Methods

-   **`void AddHandle(IDisposable handle)`**
    
    -   Registers a new cleanup handle.
        
    -   _Internal Use:_ Typically called by `StatBlock.ApplyToProcessor` as it creates modifiers.
        
    -   _Advanced Use:_ You can manually add your own disposables here if you are building a custom complex buff system.
        
-   **`void Dispose()`**
    
    -   Iterates through all registered handles and calls `.Dispose()` on them.
        
    -   Clears the internal list.
        
    -   Can be called multiple times safely (idempotent).
        

## Usage Examples

### 1. Typical Usage (via StatBlockLinker)

Most users won't instantiate this directly, but will interact with it via `StatBlockLinker`.

```
// Inside StatBlockLinker.cs
private ActiveStatBlock _activeHandle;

public void Equip()
{
    // Apply returns the handle
    _activeHandle = myStatBlock.ApplyToProcessor(myProcessor, factory);
}

public void Unequip()
{
    // Dispose removes all modifiers applied by 'Equip'
    _activeHandle?.Dispose();
    _activeHandle = null;
}

```

### 2. Manual Usage (Custom Scripting)

If you are writing a custom spell system:

```
public class FireBuffSpell : MonoBehaviour
{
    private ActiveStatBlock _buffHandle;

    void Cast(AttributeProcessor target)
    {
        // Create a container for our spell effects
        _buffHandle = new ActiveStatBlock();

        // Add Effect 1: +10 Intellect
        var mod1 = target.AddModifier("Spell_Int", ...); 
        _buffHandle.AddHandle(mod1);

        // Add Effect 2: +5% Fire Damage
        var mod2 = target.AddModifier("Spell_Fire", ...);
        _buffHandle.AddHandle(mod2);
    }

    void OnSpellEnd()
    {
        // Clean up everything at once
        _buffHandle?.Dispose();
    }
}

```

## Best Practices

1.  **Always Store the Handle:** If you call `StatBlock.ApplyToProcessor` and ignore the return value, those stats are now permanent until the `AttributeProcessor` itself is destroyed.
    
2.  **Null Check:** Always use `?.Dispose()` as the handle might be null if the application failed or hasn't happened yet.
    
3.  **Scope:** Ideally, the `ActiveStatBlock` should be owned by the object that caused the effect (the Sword GameObject, the Buff Component, etc.).
