# Semantic Keys in the Attribute System

Every name in the Attribute System is a `SemanticKey` from the [SemanticKeys](https://github.com/danielgruginski/SemanticKeys) package: attribute names, tags, provider aliases (the steps of a path such as `Owner`) and link group names. This page explains how to create keys and how the Attribute System uses them. (A modifier's logic is not a key but a class; see [Modifier Logic](Modifier%20Logic.md).)

## What a SemanticKey is

`SemanticKey` is a **struct** with three parts:

| Part | Used for |
| ----- | ----- |
| **GUID** | Identity. `==`, `Equals` and dictionary lookups compare GUIDs, so renaming a key doesn't break references. |
| **Value** | The cached display string (e.g. `"Health"`). The implicit `string` conversion and `ToString()` return it. |
| **Domain GUID** | The KeyDomain the key belongs to (informational). |

Consequences worth knowing:

* There is no `new SemanticKey("Health")` constructor, and strings don't convert to `SemanticKey`. Code like `entity.GetAttribute("Health")` does not compile.
* An unassigned key is `SemanticKey.None`, never `null`. Check `key != SemanticKey.None` or `key.IsValid`. (Comparing a key to `null` compiles, but is always `true`.)

## Creating keys

Keys live in **KeyDomain** assets:

1. Create a domain: **Create > SemanticKeys > Key Domain**, or pick **+ Create New Domain** from any SemanticKey field's dropdown in the Inspector.
2. Add keys to it (**+ Add Key** in the dropdown, or in the domain's inspector).
3. Select the domain asset and click **Generate Static Class**. This writes a class such as `Stats` with one `static readonly SemanticKey` per key, in the namespace set in `SemanticKeysSettings` (default `Game.Constants`).

The examples in this documentation assume four domains:

| Domain | Holds | Example |
| ----- | ----- | ----- |
| `Stats` | Attribute names | `Stats.Health`, `Stats.MaxHealth`, `Stats.Strength`, `Stats.Damage` |
| `Tags` | Tags | `Tags.Poisoned`, `Tags.Undead` |
| `Links` | Provider aliases used in paths | `Links.Owner`, `Links.RightHand` |
| `Groups` | Link group names | `Groups.Inventory`, `Groups.Party` |

```csharp
using System.Collections.Generic;
using Game.Constants; // the namespace of your generated classes
using ReactiveSolutions.AttributeSystem.Core;
using SemanticKeys;

var player = new Entity();
player.SetOrUpdateBaseValue(Stats.Strength, 10f);
player.AddTag(Tags.Poisoned);

var sword = new Entity();
sword.RegisterExternalProvider(Links.Owner, player);
var ownerStrength = sword.GetAttribute(Stats.Strength, new List<SemanticKey> { Links.Owner });
```

In the Inspector, SemanticKey fields show a searchable dropdown. Restrict a field to one domain with `[SemanticKeyFilter("Stats")]`.

## Tests and prototypes

For unit tests or quick experiments, `SemanticKey.FromRawString("Health")` creates a key without a domain. It uses the string as its GUID, so two raw keys with the same string are equal, but **a raw key is never equal to the domain key `Stats.Health`**. Don't mix raw and domain keys for the same concept.

## Identity vs. value

The Attribute System compares keys by **GUID**, so it survives renames: attributes, tags, providers and paths, pointers and link groups. A key's string value is only its display name (in the Inspector and in logs).

**JSON files** (StatBlocks and entity profiles) name keys by name, and list each name's GUID in a `keys` table at the end of the file (see [JSON Format](JSON%20Format.md#keys)). The GUID in the table is what counts: a name listed with another GUID is a *different* key that merely shares the name.

After a key is renamed, JSON files still show its old name until they are saved again. That doesn't affect the game, since keys match by GUID. To refresh a file, open it in its editor window (double-click it) and save it: opening gives renamed keys their current names.
