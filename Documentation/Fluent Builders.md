
# Fluent Builders

The Fluent Builder API provides a powerful, code-driven way to generate `StatBlock` POCOs (Plain Old C# Objects) and `EntityProfile`s.

While the Unity Inspector is great for visually tweaking single entities, it quickly becomes tedious when managing hundreds of items, characters, or procedurally generated content. The Fluent Builders allow you to define complex relational data, nested entities, and reactive modifiers entirely in C#, giving you compile-time safety, auto-complete, and the ability to use loops and variables to mass-generate content.

The builders are also how JSON files are read: each property of a StatBlock or profile file is a builder call (`"tags": ["Magical"]` is `AddTag(Tags.Magical)`). See [JSON Format](JSON%20Format.md).

## 1. StatBlockBuilder

The `StatBlockBuilder` is used to create pure `StatBlock` POCOs programmatically. It handles conditions, modifiers, and tags.

### Basic Usage

The examples on this page use keys from classes generated from your KeyDomains (`Stats`, `Tags`, `Links`, `Groups`), see [Semantic Keys](Semantic%20Keys.md).

```csharp
using Game.Constants; // Namespace of your generated key classes (Stats, Tags, ...)
using ReactiveSolutions.AttributeSystem.Core;
using ReactiveSolutions.AttributeSystem.Core.Builders;
using ReactiveSolutions.AttributeSystem.Core.Data;
using SemanticKeys;

// Build the StatBlock
StatBlock poisonDebuff = StatBlockBuilder.Create("Poison_Debuff")
    .SetCondition(StatBlockCondition.Mode.Always, SemanticKey.None) // Active immediately
    .AddFlatModifier(Stats.Health, -5f)                              // -5 Health
    .AddMultiplierModifier(Stats.Speed, -0.25f)                      // -25% Speed (x0.75)
    .AddTag(Tags.Poisoned)                                           // Applies the "Poisoned" tag to the entity
    .Build();

```

-   `AddFlatModifier` adds an Additive modifier with `ValueLogic`. `AddMultiplierModifier` adds a Multiplicative one that multiplies the attribute by `1 + percentage`: `0.5f` is +50% (x1.5) and `-0.25f` is -25% (x0.75). Each multiplier applies on its own, so two +10% modifiers make x1.21.
    
-   `AddFlatModifier` and `AddMultiplierModifier` leave `Priority` at 0, and modifiers on an attribute are evaluated by `Priority`, then by type (Additive, Multiplicative, Override, Clamp Min, Clamp Max): flat bonuses are added before multipliers apply, whatever order you call the methods in.
    
-   New blocks start with an `Always` condition. `SetCondition(condition)` sets another, made with `StatBlockCondition.HasTag(tag, path)`, `LacksTag(tag, path)`, `Compare(a, op, b)`, `All(...)` or `Any(...)` (see [StatBlock](StatBlock.md#5-activation-condition)).
    

### Everything Else a StatBlock Holds

```csharp
StatBlock holySword = StatBlockBuilder.Create("Holy Sword")
    .SetCondition(StatBlockCondition.HasTag(Tags.Equipped, Links.Owner)) // While the owner has "Equipped"
    .AddBaseValue(Stats.Durability, 100f)                                // Set when the block is applied, whatever the condition
    .AddRemoteTag(Tags.Blessed, Links.Owner)                             // Tags the owner
    .AddPointer(Stats.MainStat, Stats.Strength, Links.Owner)             // MainStat is the owner's Strength
    .AddModifier(AttributeReference.Of(Stats.Damage, Links.Owner),      // Modifies the owner's Damage...
        new ValueLogic(ValueSource.FromAttribute(Stats.MainStat)),      // ...by this block's MainStat
        sourceId: "Holy Sword")                                         // Shown by the Attribute Debugger
    .Build();

```

### Advanced Modifiers

The generic `AddModifier(target, logic, type = Additive, priority = 0, sourceId = null)` takes any logic object: a built-in one (`LinearLogic`, `ClampLogic`, ...) or your own class (see [Modifier Logic](Modifier%20Logic.md); namespace `ReactiveSolutions.AttributeSystem.Core.Modifiers`). Its `ValueSource` inputs are constants, or attributes read from the entity the StatBlock is applied to (a missing attribute reads as 0).

```csharp
// Damage += Strength * 2
StatBlock bruteForce = StatBlockBuilder.Create("BruteForce")
    .AddModifier(Stats.Damage, new LinearLogic { Input = ValueSource.FromAttribute(Stats.Strength), Coefficient = 2f })
    .Build();

// Health at most MaxHealth, after everything else
StatBlock vitality = StatBlockBuilder.Create("Vitality")
    .AddModifier(Stats.Health, new ValueLogic(ValueSource.FromAttribute(Stats.MaxHealth)), ModifierType.ClampMax, priority: 1000)
    .Build();

```

## 2. ProfileBuilder

The `ProfileBuilder` generates `EntityProfile`s. It is capable of setting up base stats, innate tags, link groups, pointers, and recursively building nested entities and innate stat blocks via inline actions.

### Basic Entity Creation

```csharp
EntityProfile zombieProfile = ProfileBuilder.Create("Zombie")
    .AddBaseAttribute(Stats.Health, 100f)
    .AddBaseAttribute(Stats.Speed, 2f)
    .AddInnateTag(Tags.Undead)
    .AddLinkGroup(Groups.Inventory)
    .Build();

```

### Mass Attribute Initialization

If you have several attributes that start with the same default value (like D&D core stats), you can initialize them all at once:

```csharp
EntityProfile heroProfile = ProfileBuilder.Create("Hero")
    .AddBaseAttributes(10f, Stats.Strength, Stats.Dexterity, Stats.Constitution) // All start at 10
    .Build();

```

## 3. Inline Composition (The True Power of Builders)

The most powerful feature of the Fluent API is the ability to nest Builders inside each other using `Action<T>`. This allows you to create complete, highly complex entities in a single continuous block of code, without needing to declare dozens of intermediate variables.

### Example: A Boss with an Innate Buff and a Nested Weapon

```csharp
EntityProfile bossProfile = ProfileBuilder.Create("GiantSkeletonBoss")
    .AddBaseAttribute(Stats.Health, 1000f)
    .AddInnateTag(Tags.Undead)
    
    // 1. Inline Innate StatBlock (e.g., A permanent Boss Aura)
    .AddInnateStatBlock(statBlock => statBlock
        .AddFlatModifier(Stats.Health, 500f) // Extra 500 health
        .AddTag(Tags.BossAura)
    )
    
    // 2. Inline Nested Entity (e.g., An equipped weapon entity created automatically)
    .AddNestedEntity(Links.RightHand, weapon => weapon
        .AddBaseAttribute(Stats.Damage, 75f)
        .AddInnateTag(Tags.HeavyWeapon)
    )
    
    // 3. Setup a pointer to easily access the weapon's damage locally
    .AddPointer(
        alias: Stats.MainDamage, 
        target: Stats.Damage, 
        providerPath: Links.RightHand
    )
    
    .Build();

```

When the profile is applied, the nested weapon becomes its own `Entity`, registered as a provider under `Links.RightHand` (so the pointer can reach it) and disposed together with the boss. `AddInnateStatBlock` and `AddNestedEntity` also accept an already-built `StatBlock` or `EntityProfile`, or the ID of a JSON file: `AddInnateStatBlock("Auras/Boss")` and `AddNestedEntity(Links.RightHand, "Weapons/BoneCleaver")` (see [EntityProfile](EntityProfile.md)).

## 4. Integration Workflow (The Data/Asset Split)

The builders create their objects in memory: `StatBlockBuilder` outputs a plain, serializable `StatBlock`, and `ProfileBuilder` a plain, serializable `EntityProfile`. You can use these built objects in two primary ways:

1.  **Runtime Generation:** Generate profiles on the fly when your game boots up or when procedurally generating a dungeon. You can immediately pass the built profile directly to an `Entity`, and apply built StatBlocks the same way. Applying never modifies the profile or the StatBlock, so one instance can be used for any number of entities:
    
    ```csharp
    _entity.ApplyProfile(bossProfile);
    ActiveStatBlock poisonHandle = poisonDebuff.ApplyToEntity(_entity);
    
    ```
    
2.  **Editor Generation Scripts:** Write a custom Unity Editor script that builds the data in code and saves it as JSON files with `StatBlockJson.ToJson` and `EntityProfileJson.ToJson`. The Stat Block Editor, the Entity Profile Editor and the ID dropdowns (such as an `EntityController`'s **Profile Id**) then pick them up. An ID is the file's path in its folder without the extension.
    
    ```csharp
    // Example inside an Editor script:
    System.IO.Directory.CreateDirectory("Assets/Resources/Data/StatBlocks/Debuffs");
    System.IO.File.WriteAllText("Assets/Resources/Data/StatBlocks/Debuffs/Poison.json", StatBlockJson.ToJson(poisonDebuff));
    
    System.IO.Directory.CreateDirectory("Assets/Resources/Data/EntityProfiles/Bosses");
    System.IO.File.WriteAllText("Assets/Resources/Data/EntityProfiles/Bosses/GiantSkeleton.json", EntityProfileJson.ToJson(bossProfile));
    
    UnityEditor.AssetDatabase.Refresh();
    
    ```
    
    Innate StatBlocks, and nested profiles built inline (`AddNestedEntity(key, weapon => ...)`), are written inside the profile's file. The Entity Profile Editor only shows nested entities by ID, so it doesn't open a file with a nested profile written in full: to edit the weapon there, or to reuse it, save it as its own profile file and add it by ID, e.g. `AddNestedEntity(Links.RightHand, "Weapons/BoneCleaver")`.
    
    This gives you code-driven design that outputs data files you can pick on your `EntityController`s!
