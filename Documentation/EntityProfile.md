# EntityProfile Documentation

## Overview

An `EntityProfile` is the blueprint of an entity: its base attributes, innate tags, link groups, nested entities, pointers and innate StatBlocks. It is a plain, serializable C# class (like `StatBlock`), so you can:

-   save it as a JSON file with the **Entity Profile Editor** and pick it by ID on an `EntityController`,
    
-   author it directly in the Inspector, in an `EntityController`'s **Profile** field, or
    
-   build it in code with `ProfileBuilder` (see [Fluent Builders](Fluent%20Builders.md)).
    

`entity.ApplyProfile(profile, modifierFactory)` applies it (see [Entity](Entity.md)). Applying never modifies the profile, so one profile can create any number of entities.

Namespace: `ReactiveSolutions.AttributeSystem.Core.Data`. Keys such as `Links.RightHand` come from classes generated from KeyDomains (see [Semantic Keys](Semantic%20Keys.md)).

## Fields

| Field | Description |
| ----- | ----- |
| `ProfileName` | A name for logs and tools. |
| `BaseAttributes` | Attributes and their starting base values. |
| `InnateTags` | Tags added when the profile is applied. |
| `LinkGroups` | Link groups created empty (e.g. `Groups.Inventory`). |
| `InnateStatBlockIds` | StatBlock JSON files, by ID, applied as innate passives. |
| `InnateStatBlocks` | StatBlocks stored in the profile itself. |
| `NestedEntities` | Child entities, each registered as a provider under its `ProviderKey`. `ProfileId` names the profile JSON it is created from. |
| `Pointers` | Aliases to other attributes, local or through a provider path. |

`ApplyProfile` applies them in this order: base attributes, innate tags, link groups, nested entities, pointers, innate StatBlocks (the ones given by ID first). Entries with an unassigned key (`SemanticKey.None`) or an empty ID are skipped.

## JSON Files and IDs

Profiles are saved under `Assets/Resources/Data/EntityProfiles/`. A profile's ID is its path in that folder without the extension: `Assets/Resources/Data/EntityProfiles/Monsters/Goblin.json` is `"Monsters/Goblin"`. StatBlocks work the same way under `Assets/Resources/Data/StatBlocks/`.

-   **Entity Profile Editor** (**Window > Attribute System > Entity Profile Editor**): **New**, **Load** and **Save** profile files, with the same fields as the Inspector. Type a file name such as `Monsters/Goblin` to save into a subfolder.
    
-   **ID fields** (an `EntityController`'s **Profile Id**, a nested entity's **Profile Id**, **Innate Stat Block Ids**) are dropdowns of the files in those folders. An ID whose file doesn't exist shows as "(missing)".
    
-   **In code**, `EntityProfileJsonLoader.Load("Monsters/Goblin")` returns the profile, or `null` (and logs an error) if the file can't be loaded. `"Monsters/Goblin.json"` and `"Data/EntityProfiles/Monsters/Goblin"` work too.
    

```csharp
using ReactiveSolutions.AttributeSystem.Core;
using ReactiveSolutions.AttributeSystem.Core.Data;

var goblin = new Entity();
goblin.ApplyProfile(EntityProfileJsonLoader.Load("Monsters/Goblin"), new ModifierFactory()); // A null profile is ignored
```

The JSON stores each key with its GUID, cached name and domain GUID. Read [Identity vs. value](Semantic%20Keys.md#identity-vs-value) before editing files by hand.

## Nested Entities

A nested entity is a child `Entity` created from its own profile and registered as a provider under `ProviderKey`, so the parent reaches its attributes through that key (e.g. the `Damage` of `Links.RightHand`). It is disposed with the parent.

-   In data, `ProfileId` references another profile JSON file, so one weapon profile can be reused by many characters.
    
-   In code, `ProfileBuilder.AddNestedEntity(key, profile)` and `AddNestedEntity(key, builder => ...)` set the entry's `Profile` field instead. That field isn't serialized: saved profiles reference their nested profiles by ID.
    

A profile that nests itself, directly or through other profiles, is caught: that nested entry is skipped with the error `[Entity] Skipped nested entity '...': profile '...' is already being applied further up.` The same profile can still appear several times side by side (e.g. a dagger in each hand).

## On an EntityController

An `EntityController` has two profile fields (see [EntityController](EntityController.md)):

-   **Profile Id**: a profile JSON file, applied first.
    
-   **Profile**: a profile authored right there, applied second. Its base values win over the JSON profile's, and the rest is added on top (a nested entity or pointer with the same key replaces the JSON profile's).
    

Both are optional. Use JSON profiles for anything shared (every goblin), and the inline profile for a unique entity or per-instance tweaks.
