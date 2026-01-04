# Attribute Pointers (Aliasing)

Attribute Pointers allow you to create **Aliases** that redirect to a **Target Attribute**. This is a powerful feature for abstracting game mechanics, allowing you to build systems that modify "abstract" stats (like `MainStat`) which dynamically resolve to concrete stats (like `Strength` or `Intelligence`).

## Core Concept

A Pointer is an Attribute that overrides its own Base Value with the value of a Target Attribute.

-   **Reading** a Pointer returns the Target's value (plus any local modifiers on the pointer).
    
-   **Modifying** a Pointer (setting Base Value) sets the pointer's _local_ base value, which is usually ignored (shadowed) while the pointer is active.
    
-   **Modifiers** applied to a Pointer are applied to the _result_ of the pointer. (e.g. `MainStat = (Strength) + MainStatBuffs`).
    

### Use Cases

-   **Class Archetypes:** Create a `MainStat` alias. For a Warrior, point it to `Strength`. For a Mage, point it to `Intelligence`.
    
-   **Damage Conversion:** Create an alias `DamageSource`. Point it to `FireDamage` or `IceDamage` depending on the equipped weapon element.
    
-   **Shared Resources:** Make `Energy` point to `Stamina` for Rogues, but `Mana` for Mages.
    
-   **Remote Stats:** Create a `SkillBonus` pointer that links to `Owner.Intelligence`.
    

## Usage in Code

You manage pointers via the `AttributeProcessor`.

### Creating a Local Pointer

```
// Define keys
var aliasKey = new SemanticKey("MainStat");
var targetKey = new SemanticKey("Strength");

// Create the link. Returns an IDisposable to undo the pointer.
var handle = processor.SetPointer(aliasKey, targetKey);

```

### Creating a Remote Pointer

Pointers can link to attributes on other processors by specifying a provider path.

```
var skillBonusKey = new SemanticKey("SkillBonus");
var intKey = new SemanticKey("Intelligence");
var ownerPath = new List<SemanticKey> { new SemanticKey("Owner") };

// Link SkillBonus -> Owner.Intelligence
processor.SetPointer(skillBonusKey, intKey, ownerPath);

```

-   If the "Owner" provider is not registered yet, the pointer resolves to 0 (default).
    
-   As soon as you call `processor.RegisterExternalProvider("Owner", playerProcessor)`, the pointer automatically connects and streams the value.
    

### Accessing Values

```
// Setup concrete value
processor.SetOrUpdateBaseValue(targetKey, 50);

// Read via Alias
var aliasAttr = processor.GetAttribute(aliasKey);
Debug.Log(aliasAttr.Value.Value); // Outputs 50 (from Target)

```

### Chaining

Pointers can be chained (`A -> B -> C`). The system automatically resolves the chain to the final concrete attribute.

-   **Cycle Prevention:** The system prevents circular pointers (`A -> B -> A`) and logs an error if detected.
    
-   **Self-Healing:** If a link in the chain is broken, the dependent pointers gracefully fallback to 0.
    

## Architecture: The Pointer Stack

Under the hood, every `Attribute` maintains a **Stack of Pointers**. This allows for robust "Polymorph" behavior where effects can override stats temporarily without destroying data.

1.  **Base Layer:** The attribute's concrete Base Value.
    
2.  **Pointer Layer(s):** When you call `SetPointer`, you push a new reference onto the stack.
    
3.  **Resolution:** The attribute uses the Topmost Pointer in the stack as its source. If the stack is empty, it uses the Base Layer.
    

This means you can have a "Base" pointer (Class: Warrior -> Strength) and a "Temporary" pointer (Spell: Polymorph -> SheepStrength) active at the same time. When the spell ends (pointer removed), it falls back to the Class pointer.

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