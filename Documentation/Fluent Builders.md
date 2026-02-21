
# Fluent Builders

The Fluent Builder API provides a powerful, code-driven way to generate `EntityProfile` and `StatBlock` POCOs (Plain Old C# Objects).

While the Unity Inspector is great for visually tweaking single entities, it quickly becomes tedious when managing hundreds of items, characters, or procedurally generated content. The Fluent Builders allow you to define complex relational data, nested entities, and reactive modifiers entirely in C#, giving you compile-time safety, auto-complete, and the ability to use loops and variables to mass-generate content.

## 1. StatBlockBuilder

The `StatBlockBuilder` is used to create pure `StatBlock` POCOs programmatically. It handles conditions, modifiers, and tags.

### Basic Usage

```
using ReactiveSolutions.AttributeSystem.Core.Builders;
using SemanticKeys;
using sk;

// Define your keys
SemanticKey healthKey = new SemanticKey("Health");
SemanticKey poisonedTag = new SemanticKey("Poisoned");

// Build the StatBlock
StatBlock poisonDebuff = StatBlockBuilder.Create("Poison_Debuff")
    .SetCondition(StatBlockCondition.Mode.Always, SemanticKey.None) // Active immediately
    .AddFlatModifier(healthKey, -5f)                                // -5 Health
    .AddTag(poisonedTag)                                            // Applies the "Poisoned" tag to the entity
    .Build();

```

### Advanced Modifiers

You can use the generic `AddModifier` method to link to custom `LogicType` keys handled by your `ModifierFactory`:

```
SemanticKey customExecuteLogic = new SemanticKey("ExecuteLogic");

StatBlock executeBuff = StatBlockBuilder.Create("Execute")
    .AddModifier(healthKey, customExecuteLogic, ModifierType.Additive, new ValueSource { Mode = ValueSource.SourceMode.Constant, ConstantValue = 50f })
    .Build();

```

## 2. ProfileBuilder

The `ProfileBuilder` generates `EntityProfile` POCOs. It is capable of setting up base stats, link groups, pointers, and recursively building nested entities and innate stat blocks via inline actions.

### Basic Entity Creation

```
SemanticKey speedKey = new SemanticKey("Speed");
SemanticKey undeadTag = new SemanticKey("Undead");
SemanticKey inventoryGroup = new SemanticKey("Inventory");

EntityProfile zombieProfile = ProfileBuilder.Create("Zombie")
    .AddBaseAttribute(healthKey, 100f)
    .AddBaseAttribute(speedKey, 2f)
    .AddInnateTag(undeadTag)
    .AddLinkGroup(inventoryGroup)
    .Build();

```

### Mass Attribute Initialization

If you have several attributes that start with the same default value (like D&D core stats), you can initialize them all at once:

```
SemanticKey str = new SemanticKey("Strength");
SemanticKey dex = new SemanticKey("Dexterity");
SemanticKey con = new SemanticKey("Constitution");

EntityProfile heroProfile = ProfileBuilder.Create("Hero")
    .AddBaseAttributes(10f, str, dex, con) // All start at 10
    .Build();

```

## 3. Inline Composition (The True Power of Builders)

The most powerful feature of the Fluent API is the ability to nest Builders inside each other using `Action<T>`. This allows you to create complete, highly complex entities in a single continuous block of code, without needing to declare dozens of intermediate variables.

### Example: A Boss with an Innate Buff and a Nested Weapon

```
SemanticKey damageKey = new SemanticKey("Damage");
SemanticKey rightHandKey = new SemanticKey("RightHand");
SemanticKey bossAuraTag = new SemanticKey("BossAura");

EntityProfile bossProfile = ProfileBuilder.Create("GiantSkeletonBoss")
    .AddBaseAttribute(healthKey, 1000f)
    .AddInnateTag(undeadTag)
    
    // 1. Inline Innate StatBlock (e.g., A permanent Boss Aura)
    .AddInnateStatBlock(statBlock => statBlock
        .AddFlatModifier(healthKey, 500f) // Extra 500 health
        .AddTag(bossAuraTag)
    )
    
    // 2. Inline Nested Entity (e.g., An equipped weapon processor created automatically)
    .AddNestedEntity(rightHandKey, weapon => weapon
        .AddBaseAttribute(damageKey, 75f)
        .AddInnateTag(new SemanticKey("HeavyWeapon"))
    )
    
    // 3. Setup a pointer to easily access the weapon's damage locally
    .AddPointer(
        alias: new SemanticKey("MainDamage"), 
        target: damageKey, 
        providerPath: rightHandKey
    )
    
    .Build();

```

## 4. Integration Workflow (The Data/Asset Split)

Because the builders output pure, serializable C# POCOs in memory (and not Unity `ScriptableObject`s directly), your core system is completely decoupled from the Unity Engine. You can use these built objects in two primary ways:

1.  **Runtime Generation:** Generate profiles on the fly when your game boots up or when procedurally generating a dungeon. You can immediately pass the built profile POCO directly to your processor:
    
    ```
    _processor.ApplyProfile(bossProfile, _modifierFactory);
    
    ```
    
2.  **Editor Generation Scripts:** Write a custom Unity Editor script that loops through your code, builds the POCOs, and wraps them in your `EntityProfileSO` or `StatBlockSO` wrappers so they appear as physical assets in your project folder.
    
    ```
    // Example inside an Editor script:
    var wrapperSO = ScriptableObject.CreateInstance<EntityProfileSO>();
    wrapperSO.Profile = bossProfile; // Inject the built POCO
    UnityEditor.AssetDatabase.CreateAsset(wrapperSO, "Assets/Resources/GiantSkeletonBoss.asset");
    
    ```
    
    This gives you code-driven design that safely outputs physical assets you can drag and drop onto your `AttributeController`s!
