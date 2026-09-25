# Attribute Pointers (Aliasing)

Attribute Pointers allow you to create **Aliases** that redirect to a **Target Attribute**. This is a powerful feature for abstracting game mechanics, allowing you to build systems that modify "abstract" stats (like `MainStat`) which dynamically resolve to concrete stats (like `Strength` or `Intelligence`).

## Core Concept

A Pointer is an Attribute that overrides its own Base Value with the (final) value of a Target Attribute.

-   **Reading** a Pointer returns the Target's value (plus any local modifiers on the pointer).
    
-   **Modifying** a Pointer (setting Base Value) sets the pointer's _local_ base value, which is ignored (shadowed) while a pointer is active and used again once every pointer is removed.
    
-   **Modifiers** applied to a Pointer are applied to the _result_ of the pointer. (e.g. `MainStat = (Strength) + MainStatBuffs`).
    

### Use Cases

-   **Class Archetypes:** Create a `MainStat` alias. For a Warrior, point it to `Strength`. For a Mage, point it to `Intelligence`.
    
-   **Damage Conversion:** Create an alias `DamageSource`. Point it to `FireDamage` or `IceDamage` depending on the equipped weapon element.
    
-   **Shared Resources:** Make `Energy` point to `Stamina` for Rogues, but `Mana` for Mages.
    
-   **Remote Stats:** Create a `SkillBonus` pointer that links to `Owner.Intelligence`.
    

## Usage in Code

You manage pointers via the `Entity` (`SetPointer`). Pointers can also be declared in data: a StatBlock's `Pointers` (active while the block is active) and an `EntityProfile`'s `Pointers`. The keys below (`Stats.MainStat`, `Links.Owner`, ...) come from static classes generated from KeyDomains; see [Semantic Keys](Semantic%20Keys.md).

### Creating a Local Pointer

```csharp
var entity = new Entity();

// Create the link MainStat -> Strength. Returns an IDisposable to undo the pointer.
var handle = entity.SetPointer(Stats.MainStat, Stats.Strength);

```

`SetPointer` creates the alias attribute if it doesn't exist yet.

### Creating a Remote Pointer

Pointers can link to attributes on other entities by specifying a provider path.

```csharp
var ownerPath = new List<SemanticKey> { Links.Owner };

// Link SkillBonus -> Owner.Intelligence
entity.SetPointer(Stats.SkillBonus, Stats.Intelligence, ownerPath);

```

-   If the "Owner" provider is not registered yet (or has no `Intelligence` attribute), the pointer resolves to 0 (default).
    
-   As soon as you call `entity.RegisterExternalProvider(Links.Owner, player)`, the pointer automatically connects and streams the value. If the provider is unregistered, the pointer goes back to 0; if another entity is registered as `Links.Owner`, it follows that one.
    

### Accessing Values

```csharp
// Setup concrete value
entity.SetOrUpdateBaseValue(Stats.Strength, 50f);

// Read via Alias
var mainStat = entity.GetAttribute(Stats.MainStat);
Debug.Log(mainStat.ObservableValue.Value); // Outputs 50 (from Target)

```

### Chaining

Pointers can be chained (`A -> B -> C`). Each pointer reads its target's final value, so the chain resolves to the final concrete attribute.

-   **Cycle Prevention:** `SetPointer` refuses circular pointers (`A -> B -> A`): it logs the error `[Entity] Circular pointer detected: B -> A` and returns an empty handle. Pointing an alias at itself logs the warning `[Entity] Cannot point alias 'A' to itself.` The check follows the currently active local pointers. A loop through a provider path isn't detected here, but if values keep changing around it, the attribute's circular-dependency guard stops it and logs an error (see [Attribute](Attribute.md#circular-dependencies-and-known-limitations)).
    
-   **Self-Healing:** If a link in the chain is broken (a target attribute or provider is missing), the dependent pointers gracefully fall back to 0. If a pointer in the middle of the chain is removed, that attribute falls back to its own base value (or to the previous pointer on its stack).
    

## Architecture: The Pointer Stack

Under the hood, every `Attribute` maintains a **Stack of Pointers**. This allows for robust "Polymorph" behavior where effects can override stats temporarily without destroying data.

1.  **Base Layer:** The attribute's concrete Base Value.
    
2.  **Pointer Layer(s):** When you call `SetPointer`, you push a new reference onto the stack.
    
3.  **Resolution:** The attribute uses the Topmost Pointer in the stack as its source. If the stack is empty, it uses the Base Layer.
    

This means you can have a "Base" pointer (Class: Warrior -> Strength) and a "Temporary" pointer (Spell: Polymorph -> SheepStrength) active at the same time. When the spell ends (pointer removed), it falls back to the Class pointer.

Disposing a pointer's handle removes that pointer wherever it is in the stack; only removing the topmost one changes the source. `Attribute.ActivePointerTarget` returns the active target (the Attribute Debugger shows it too).

### Modifiers on Pointers

Modifiers added to an Alias apply **Locally**.

```
Alias (A) -> Target (B)
B = 10
Modifier on A = +5

Result A = (B.Value) + 5 = 15.
Result B = 10.

```

This is distinct from "Proxying" where the modifier would move to B. This architecture ensures that buffs applied to a temporary form (or alias) stay on that alias and don't pollute the underlying stats.