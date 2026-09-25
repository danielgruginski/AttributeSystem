# ActiveStatBlock Class Documentation

## Overview

The `ActiveStatBlock` is a runtime handle that represents everything a `StatBlock` application added to an `Entity` (its modifiers, tags, remote tags and pointers, plus the subscription to its activation condition). It acts as a "receipt" for a `StatBlock` application.

Its primary purpose is **Lifecycle Management**. When you apply a StatBlock (like equipping a sword or casting a buff), the system gives you an `ActiveStatBlock`. When you want to remove those effects (unequipping the sword or the buff expiring), you simply `Dispose()` this object. The StatBlock's `BaseValues` are the exception: they are permanent and stay after disposal.

## Key Features

-   **Aggregate Disposal:** Holds multiple `IDisposable` handles (one for each modifier, tag and pointer the StatBlock applied). Calling `Dispose()` on the `ActiveStatBlock` triggers disposal for all of them.
    
-   **RAII Pattern:** Follows the "Resource Acquisition Is Initialization" pattern. The StatBlock stays applied for as long as this object is not disposed; disposing it removes the effects. Nothing happens automatically: losing the reference does not remove anything.
    
-   **Safety:** Prevents "Zombie Stats" (modifiers that persist forever because you lost their reference IDs).
    

## Class Definition

```csharp
public class ActiveStatBlock : IDisposable
{
    // ...
}

```

## Public API

### Methods

-   **`void AddHandle(IDisposable handle)`**
    
    -   Registers a new cleanup handle (`null` is ignored).
        
    -   _Internal Use:_ Typically called by `StatBlock.ApplyToEntity` as it applies the block's content.
        
    -   _Advanced Use:_ You can manually add your own disposables here if you are building a custom complex buff system.
        
-   **`void Dispose()`**
    
    -   Iterates through all registered handles and calls `.Dispose()` on them.
        
    -   Clears the internal list.
        
    -   Can be called multiple times safely (idempotent).
        

## Usage Examples

### 1. Typical Usage (via StatBlockLinker)

Most users won't instantiate this directly, but will interact with it via `StatBlockLinker`, which keeps the handle of every StatBlock it applies and disposes them in `ClearStatBlocks()` and when it is destroyed.

```csharp
// The pattern StatBlockLinker follows (simplified: it keeps one handle per StatBlock ID)
private ActiveStatBlock _activeHandle;

public void Equip()
{
    // Apply returns the handle
    _activeHandle = myStatBlock.ApplyToEntity(myEntity, factory);
}

public void Unequip()
{
    // Dispose removes everything applied by 'Equip' (except the StatBlock's permanent BaseValues)
    _activeHandle?.Dispose();
    _activeHandle = null;
}

```

### 2. Manual Usage (Custom Scripting)

If you are writing a custom spell system (`Stats.Intelligence` and `Stats.FireDamage` are keys from a class generated from your `Stats` KeyDomain, see [Semantic Keys](Semantic%20Keys.md)):

```csharp
using Game.Constants; // Namespace of your generated key classes (Stats)
using ReactiveSolutions.AttributeSystem.Core;
using ReactiveSolutions.AttributeSystem.Core.Modifiers; // StaticAttributeModifier
using UnityEngine;

public class FireBuffSpell : MonoBehaviour
{
    private ActiveStatBlock _buffHandle;

    void Cast(Entity target)
    {
        // Create a container for our spell effects
        _buffHandle = new ActiveStatBlock();

        // Add Effect 1: +10 Intelligence
        var mod1 = target.AddModifier("Spell_Int", new StaticAttributeModifier(new AttributeModifierSpec
        {
            SourceId = "Spell_Int",
            Type = ModifierType.Additive,
            Arguments = { ValueSource.Const(10f) }
        }), Stats.Intelligence);
        _buffHandle.AddHandle(mod1);

        // Add Effect 2: +5% Fire Damage
        var mod2 = target.AddModifier("Spell_Fire", new StaticAttributeModifier(new AttributeModifierSpec
        {
            SourceId = "Spell_Fire",
            Type = ModifierType.Multiplicative,
            Arguments = { ValueSource.Const(1.05f) }
        }), Stats.FireDamage);
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

1.  **Always Store the Handle:** If you call `StatBlock.ApplyToEntity` and ignore the return value, you can never remove those effects: they last as long as the `Entity`, and tags or modifiers the block applied to _other_ entities (through `RemoteTags` or a modifier's `TargetPath`) stay on them even after this `Entity` is disposed. (Innate StatBlocks from an `EntityProfile` are the exception: the `Entity` disposes them itself.)
    
2.  **Null Check:** Always use `?.Dispose()` on a stored handle: the field is null until the StatBlock has been applied (`ApplyToEntity` itself never returns null).
    
3.  **Scope:** Ideally, the `ActiveStatBlock` should be owned by the object that caused the effect (the Sword GameObject, the Buff Component, etc.).
