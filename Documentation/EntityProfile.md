# EntityProfile Documentation

## Overview

An `EntityProfile` is the blueprint of an entity: its base attributes, innate tags, link groups, nested entities, pointers and innate StatBlocks. It is a plain, serializable C# class (like `StatBlock`), so you can:

-   save it as a JSON file with the **Entity Profile Editor** and pick it by ID on an `EntityController`,
    
-   author it directly in the Inspector, in an `EntityController`'s **Profile** field, or
    
-   build it in code with `ProfileBuilder` (see [Fluent Builders](Fluent%20Builders.md)).
    

A profile can build on **templates**: profiles shared by many entities, such as a `Character` template that every character uses (see [Templates](#templates)).

`entity.ApplyProfile(profile)` applies it (see [Entity](Entity.md)). Applying never modifies the profile, so one profile can create any number of entities.

Namespace: `ReactiveSolutions.AttributeSystem.Core.Data`. Keys such as `Links.RightHand` come from classes generated from KeyDomains (see [Semantic Keys](Semantic%20Keys.md)).

## Fields

| Field | Description |
| ----- | ----- |
| `ProfileName` | A name for logs and tools. |
| `Templates` | Profiles this one builds on, applied first and once per entity (see [Templates](#templates)). |
| `ParentKey` | When an entity created from this profile is nested in another, the key under which it reaches that entity (see [Nested Entities](#nested-entities)). |
| `BaseAttributes` | Attributes and their starting base values. |
| `InnateTags` | Tags added when the profile is applied. |
| `LinkGroups` | Link groups created empty (e.g. `Groups.Inventory`). |
| `InnateStatBlockIds` | StatBlock JSON files, by ID, applied as innate passives. |
| `InnateStatBlocks` | StatBlocks stored in the profile itself. |
| `NestedEntities` | Child entities, each registered as a provider under its `ProviderKey`. `ProfileId` names the profile JSON it is created from. |
| `Pointers` | Aliases to other attributes, local or through a provider path. |

`ApplyProfile` applies them in this order: templates, base attributes, innate tags, link groups, nested entities, pointers, innate StatBlocks (the ones given by ID first). Entries with an unassigned key (`SemanticKey.None`) or an empty ID are skipped. Each profile is applied once per entity: applying a profile the entity already has logs a warning and does nothing.

## JSON Files and IDs

Profiles are saved under `Assets/Resources/Data/EntityProfiles/`. A profile's ID is its path in that folder without the extension: `Assets/Resources/Data/EntityProfiles/Monsters/Goblin.json` is `"Monsters/Goblin"`. StatBlocks work the same way under `Assets/Resources/Data/StatBlocks/`.

-   **Entity Profile Editor** (**Window > Attribute System > Entity Profile Editor**): **New**, **Load** and **Save** profile files, with the same fields as the Inspector. Type a file name such as `Monsters/Goblin` to save into a subfolder.
    
-   **ID fields** (an `EntityController`'s **Profile Id**, **Templates**, a nested entity's **Profile Id**, **Innate Stat Block Ids**) are dropdowns of the files in those folders. An ID whose file doesn't exist shows as "(missing)".
    
-   **In code**, `EntityProfileJsonLoader.Load("Monsters/Goblin")` returns the profile, or `null` (and logs an error) if the file can't be loaded. `"Monsters/Goblin.json"` and `"Data/EntityProfiles/Monsters/Goblin"` work too.
    

```csharp
using ReactiveSolutions.AttributeSystem.Core;
using ReactiveSolutions.AttributeSystem.Core.Data;

var goblin = new Entity();
goblin.ApplyProfile(EntityProfileJsonLoader.Load("Monsters/Goblin")); // A null profile is ignored
```

A file describes the profile the way `ProfileBuilder` builds it, e.g. `"baseAttributes": { "Health": 40 }`, and names keys by name, with a table of their GUIDs at the end. See [JSON Format](JSON%20Format.md#entity-profile-files) for the format, and `EntityProfileJson.ToJson` / `FromJson` to convert profiles in code.

## Templates

A template is a profile that other profiles build on. Every character can build on a `Character` template for its stats and formulas, casters on a `Caster` template that itself builds on `Character`, and so on. A template is an ordinary profile file (these ones are in `Resources/Data/EntityProfiles/Templates/`), edited in the same window. A profile lists its templates under **Templates**, and `ProfileBuilder.AddTemplate(...)` adds them in code. (Key tables are left out of these examples.)

```json
{
  "profile": "Character",
  "baseAttributes": { "Level": 1, "Strength": 10, "Vitality": 10 },
  "innateTags": ["Character"],
  "innateStatBlocks": [
    {
      "statBlock": "Character Rules",
      "modifiers": [
        { "target": "MaxHealth", "linear": { "input": "Vitality", "coefficient": 10 } },
        { "target": "AttackPower", "linear": { "input": "Strength", "coefficient": 2 } }
      ]
    }
  ]
}
```

```json
{
  "profile": "Caster",
  "templates": ["Templates/Character"],
  "baseAttributes": { "Intelligence": 12 },
  "innateTags": ["Caster"],
  "innateStatBlocks": [
    {
      "statBlock": "Caster Rules",
      "modifiers": [
        { "target": "MaxMana", "linear": { "input": "Intelligence", "coefficient": 5 } }
      ]
    }
  ]
}
```

```json
{
  "profile": "Goblin Shaman",
  "templates": ["Templates/Caster"],
  "baseAttributes": { "Strength": 6, "Vitality": 7 },
  "innateTags": ["Goblin"]
}
```

A Goblin Shaman gets the Character and Caster tags, stats and formulas, with its own Strength and Vitality: MaxHealth 70, AttackPower 12, MaxMana 60.

-   **Templates first:** a profile's templates are applied before the profile itself, in the order listed. So a template's values are defaults: the shaman's Strength of 6 replaces Character's 10.
    
-   **Once per entity:** a template is applied once per entity, however many of the entity's profiles and templates build on it. A Spellblade built on both `Caster` and `Warrior`, which both build on `Character`, gets one set of Character formulas and one Character tag.
    
-   **Templates of templates:** templates can build on templates. One that builds on itself, directly or through others, is skipped with the error `[Entity] Skipped template '...' of profile '...': it is already being applied further up.`
    
-   **In code**, `entity.Implements("Templates/Caster")` (or `Implements(profile)`) tells whether a profile was applied to the entity, directly or as a template.
    

### Reacting to Templates

Give each template a tag of its own (`Character`, `Caster`, `Weapon`), and other blocks can react to it with conditions. This `Weapon` template adds a weapon's Damage to its owner's AttackPower, but only while the owner is a Character:

```json
{
  "profile": "Weapon",
  "parentKey": "Owner",
  "innateTags": ["Weapon"],
  "innateStatBlocks": [
    {
      "statBlock": "Wielded",
      "condition": { "hasTag": "Owner/Character" },
      "modifiers": [
        { "target": "Owner/AttackPower", "value": "Damage" }
      ]
    }
  ]
}
```

A sword built on it is just `{ "profile": "Iron Sword", "templates": ["Templates/Weapon"], "baseAttributes": { "Damage": 12 } }`. Nested in a character's `MainHand`, it adds 12 to the character's AttackPower; nested in a weapon rack, it adds nothing. Its `parentKey` is what lets it reach the entity it is nested in (see below).

The same goes for rules that every entity of a kind follows, like "Undead take extra damage from holy weapons": put the StatBlock in the `Undead` template, with a condition if it should only apply at times.

## Nested Entities

A nested entity is a child `Entity` created from its own profile and registered as a provider under `ProviderKey`, so the parent reaches its attributes through that key (e.g. the `Damage` of `Links.RightHand`). It is disposed with the parent.

-   In data, `ProfileId` references another profile JSON file, so one weapon profile can be reused by many characters.
    
-   In code, `ProfileBuilder.AddNestedEntity(key, profile)` and `AddNestedEntity(key, builder => ...)` set the entry's `Profile` field instead. Unity doesn't serialize that field, so the Inspector only shows nested entities by ID. `EntityProfileJson.ToJson` writes such a nested profile in full, inside the file; the Entity Profile Editor doesn't open files like that (edit them as text).
    

-   **Reaching the parent:** a nested entity reaches the entity it is nested in if its profile names a **Parent Key** (`"parentKey"` in JSON, `SetParentKey` in code): the parent is registered as its provider under that key, e.g. a sword's `Owner`. A template can set it for all its profiles (every weapon's parent is its Owner), and a profile's own Parent Key replaces its templates'.
    

For entities you link at runtime, such as a sword equipped from an inventory, register both links yourself. The entity's `ParentKey` property holds the key from its profile:

```csharp
hero.RegisterExternalProvider(Links.MainHand, sword);
sword.RegisterExternalProvider(sword.ParentKey, hero); // e.g. Links.Owner
```

A profile that nests itself, directly or through other profiles, is caught: that nested entry is skipped with the error `[Entity] Skipped nested entity '...': profile '...' is already being applied further up.` The same profile can still appear several times side by side (e.g. a dagger in each hand).

## On an EntityController

An `EntityController` has two profile fields (see [EntityController](EntityController.md)):

-   **Profile Id**: a profile JSON file, applied first.
    
-   **Profile**: a profile authored right there, applied second. Its base values win over the JSON profile's, and the rest is added on top (a nested entity or pointer with the same key replaces the JSON profile's).
    

Both are optional. Use JSON profiles for anything shared (every goblin), and the inline profile for a unique entity or per-instance tweaks.
