# Semantic Keys in the Attribute System

Every name in the Attribute System is a `SemanticKey` from the [SemanticKeys](https://github.com/danielgruginski/SemanticKeys) package: attribute names, tags, provider aliases (the steps of a path such as `Owner`), link group names and modifier logic types. This page explains how to create keys and how the Attribute System uses them.

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

Most of the Attribute System compares keys by **GUID**, so it survives renames: attributes, tags, providers and paths, pointers and link groups.

Two places use the key's **string value** instead:

* **Modifier logic types.** `ModifierFactory` looks builders up by `spec.LogicType`'s value (`sk.Modifiers.Linear` becomes `"Linear"`). Renaming a key in the KeyDomain inspector runs **Update All References**, which updates its cached value in assets and open scenes. Closed scenes and JSON StatBlocks (below) keep the old value, and wherever it remains, the factory can't find the builder: it logs `Unknown modifier logic type '...'` and falls back to Static.
* **JSON StatBlocks.** `JsonUtility` stores each key as `{"_guid": ..., "_value": ..., "_domainGuid": ...}`. Keys match by GUID at runtime, so hand-written JSON must contain the right GUIDs: a key with another GUID is a *different* key that merely shares the display name, and a key without a GUID is `SemanticKey.None` (unassigned). The SemanticKeys reference updater doesn't scan JSON files, and saving a StatBlock writes back the values it was loaded with. To refresh one after renaming keys, load it in the StatBlock Editor, run **Tools > SemanticKeys > Update All References** (it also updates the open block), then save. Or edit its `_value` fields by hand.

## Modifier logic types

The package ships generated keys for the built-in logic types in the `sk` namespace: `sk.Modifiers.Static`, `Linear`, `Polynomial`, `Clamp`, `Min`, `Max`, `Floor`, `Step`, `Ratio`, `Exponential`, `DiminishingReturns` and `ScaledTriangular`. Use them in code:

```csharp
var spec = new AttributeModifierSpec
{
    TargetAttribute = Stats.Damage,
    LogicType = sk.Modifiers.Linear,
    Arguments = new List<ValueSource> { ValueSource.Const(5f), ValueSource.Const(1f), ValueSource.Const(0f) }
};
```

The package does **not** ship the KeyDomain asset behind `sk.Modifiers`. For the **Logic Type** dropdown in the Inspector to list the built-ins, create a KeyDomain named `Modifiers` with keys named exactly like the built-ins (`Static`, `Linear`, ...). The factory matches on the name, so these keys work even though their GUIDs differ from `sk.Modifiers`. To add a custom logic type, add a key to that domain and register a builder under the same name:

```csharp
using ReactiveSolutions.AttributeSystem.Core.Modifiers; // FunctionalModifier

var factory = new ModifierFactory();
// +2 per meter closer than 10m. The last argument is the parameter label shown in the Inspector.
factory.Register("DistanceBonus", spec => new FunctionalModifier(spec, args => args[0] > 10f ? 0f : (10f - args[0]) * 2f), "Distance");
```
