# Attribute Pointers (Aliasing)

Attribute Pointers allow you to create **Aliases** that redirect to a **Target Attribute**. This is a powerful feature for abstracting game mechanics, allowing you to build systems that modify "abstract" stats (like `MainStat`) which dynamically resolve to concrete stats (like `Strength` or `Intelligence`).

## Core Concept

A Pointer is a special type of Attribute that does not store its own value. Instead, it mirrors the value of another attribute (the Target).

-   **Reading** a Pointer returns the Target's value.
    
-   **Modifying** a Pointer (setting Base Value) applies the change to the Target.
    
-   **Modifiers** applied to a Pointer are effectively applied to the Target (they "flow through").
    

### Use Cases

-   **Class Archetypes:** Create a `MainStat` alias. For a Warrior, point it to `Strength`. For a Mage, point it to `Intelligence`. All your UI and Damage calculations can just use `MainStat` without knowing the specific underlying stat.
    
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

// Create the link
processor.SetPointer(aliasKey, targetKey);

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

-   If the "Owner" provider is not registered yet, the pointer returns 0.
    
-   As soon as you call `processor.RegisterExternalProvider("Owner", playerProcessor)`, the pointer automatically connects and streams the value.

### Accessing Values

```
// Setup concrete value
processor.SetOrUpdateBaseValue(targetKey, 50);

// Read via Alias
var aliasAttr = processor.GetAttribute(aliasKey);
Debug.Log(aliasAttr.Value.Value); // Outputs 50

// Write via Alias
processor.SetOrUpdateBaseValue(aliasKey, 100);
Debug.Log(processor.GetAttribute(targetKey).Value.Value); // Outputs 100

```

### Chaining

Pointers can be chained (`A -> B -> C`). The system automatically resolves the chain to the final concrete attribute.

-   **Self-Healing:** If you break a chain (e.g., delete `B`), `A` will gracefully handle it (usually returning 0) until the link is restored.
    
-   **Cycle Prevention:** The system prevents circular pointers (`A -> B -> A`) and logs an error if detected.
    

## Advanced Behavior

### Polymorphism

Under the hood, pointers are implemented as `PointerAttribute` instances.

-   `GetAttribute("Alias")` returns the `PointerAttribute` object.
    
-   `GetAttribute("Alias").Name` returns the Alias Name (e.g., "MainStat").
    
-   `GetAttribute("Alias").Value` returns the stream of the Target (e.g., Strength).
    

### Modifiers

Adding a modifier to a pointer applies it to the target context.

```
// Add +10 to MainStat
processor.AddModifier("Buff", new AdditiveModifier(10), aliasKey);

// Strength (the target) now increases by 10.

```

### Dynamic Re-Targeting

You can change a pointer's target at runtime.

```
// Switch class to Mage
processor.SetPointer(aliasKey, new SemanticKey("Intelligence"));

```

-   The pointer immediately updates to reflect the new target's value.
    
-   **Note:** Modifiers explicitly added to the _Alias_ will move to the new Target. Modifiers added to the _Old Target_ stay on the Old Target.
