
# Fluent Builders

The Fluent Builder API provides a powerful, code-driven way to generate `StatBlock` POCOs (Plain Old C# Objects) and `EntityProfile`s.

While the Unity Inspector is great for visually tweaking single entities, it quickly becomes tedious when managing hundreds of items, characters, or procedurally generated content. The Fluent Builders allow you to define complex relational data, nested entities, and reactive modifiers entirely in C#, giving you compile-time safety, auto-complete, and the ability to use loops and variables to mass-generate content.

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

-   `AddFlatModifier` adds a Static, Additive modifier. `AddMultiplierModifier` adds a Static, Multiplicative modifier that multiplies the attribute by `1 + percentage`: `0.5f` is +50% (x1.5) and `-0.25f` is -25% (x0.75).
    
-   The builder leaves `Priority` at 0, and modifiers on an attribute are evaluated by `Priority`, then by type (Additive, then Multiplicative, then Override): flat bonuses are added before multipliers apply, whatever order you call the methods in.
    
-   New blocks start with an `Always` condition. `SetCondition(mode, tag, invert)` sets an `Always` or `Tag` condition (the tag is checked on the entity itself); for value comparisons, composite conditions or a `TagTarget` path, set `ActivationCondition` on the built `StatBlock`. Anything else the builder doesn't cover (base values, pointers, remote tags, or a modifier's `Priority`, `SourceId` and `TargetPath`) can also be set on the built `StatBlock`.
    

### Advanced Modifiers

You can use the generic `AddModifier` method with any logic type your `ModifierFactory` knows: the built-in ones (`sk.Modifiers.Linear`, `sk.Modifiers.Clamp`, ...) or custom `LogicType` keys you registered. Arguments are `ValueSource`s: constants, or attributes read from the entity the StatBlock is applied to (a missing attribute reads as 0).

```csharp
// Built-in logic type: Damage += Strength * 2 + 0 (Linear: Input, Coefficient, Addend)
var strength = new ValueSource { Mode = ValueSource.SourceMode.Attribute, AttributeRef = new AttributeReference(Stats.Strength) };

StatBlock bruteForce = StatBlockBuilder.Create("BruteForce")
    .AddModifier(Stats.Damage, sk.Modifiers.Linear, ModifierType.Additive, strength, ValueSource.Const(2f), ValueSource.Const(0f))
    .Build();

// Custom logic type: a key from your own KeyDomain
StatBlock executeBuff = StatBlockBuilder.Create("Execute")
    .AddModifier(Stats.Health, Modifiers.Execute, ModifierType.Additive, new ValueSource { Mode = ValueSource.SourceMode.Constant, ConstantValue = 50f })
    .Build();

```

The factory finds builders by the key's name, so `Modifiers.Execute` (a key in a `Modifiers` KeyDomain you created) needs a builder registered under `"Execute"` with `factory.Register` (see [ModifierFactory](ModifierFactory.md)). An unknown name logs a warning and falls back to Static.

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
    _entity.ApplyProfile(bossProfile, _modifierFactory);
    ActiveStatBlock poisonHandle = poisonDebuff.ApplyToEntity(_entity, _modifierFactory);
    
    ```
    
2.  **Editor Generation Scripts:** Write a custom Unity Editor script that builds the data in code and saves it as JSON files. The Stat Block Editor, the Entity Profile Editor and the ID dropdowns (such as an `EntityController`'s **Profile Id**) then pick them up. An ID is the file's path in its folder without the extension.
    
    ```csharp
    // Example inside an Editor script:
    System.IO.Directory.CreateDirectory("Assets/Resources/Data/StatBlocks/Debuffs");
    System.IO.File.WriteAllText("Assets/Resources/Data/StatBlocks/Debuffs/Poison.json", JsonUtility.ToJson(poisonDebuff, true));
    
    System.IO.Directory.CreateDirectory("Assets/Resources/Data/EntityProfiles/Bosses");
    System.IO.File.WriteAllText("Assets/Resources/Data/EntityProfiles/Bosses/GiantSkeleton.json", JsonUtility.ToJson(bossProfile, true));
    
    UnityEditor.AssetDatabase.Refresh();
    
    ```
    
    Innate StatBlocks are saved inside the profile. Nested profiles built inline (`AddNestedEntity(key, weapon => ...)`) are not: they only exist in memory, so save the weapon as its own profile file and add it by ID, e.g. `AddNestedEntity(Links.RightHand, "Weapons/BoneCleaver")`.
    
    This gives you code-driven design that outputs data files you can pick on your `EntityController`s!
