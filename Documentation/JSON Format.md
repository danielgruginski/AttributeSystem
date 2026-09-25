# JSON Format

## Overview

StatBlocks and entity profiles are saved as JSON files in a format of their own, which describes them the way the [Fluent Builders](Fluent%20Builders.md) build them: each property of a file is a builder call. Loading a file makes those calls; saving writes what they need.

```json
{
  "statBlock": "Iron Sword",
  "condition": { "hasTag": "Equipped" },
  "tags": ["Magical"],
  "modifiers": [
    { "target": "Damage", "value": 5 },
    { "target": "Damage", "linear": { "input": "Owner/Strength", "coefficient": 0.5 } },
    { "target": "Owner/Health", "type": "ClampMax", "priority": 1000, "value": "Owner/MaxHealth" }
  ],
  "keys": {
    "Equipped": "7f1c0a52-3c1e-4d0b-9a57-2f6d8e4b1c90",
    "Magical": "0b9e4c1d-8f2a-4e6b-b1c3-5d7e9f0a2b4c",
    "Damage": "3a5c7e9b-1d2f-4a6c-8e0b-2c4d6f8a0b1e",
    "Owner": "e2d4f6a8-0c1b-4e3d-a5f7-9b1d3c5e7f90",
    "Strength": "5c7e9a1b-3d4f-4b6a-8c0e-1f3a5c7e9b2d",
    "Health": "9a1b3c5d-7e8f-4a0b-b2c4-6d8e0f1a3b5c",
    "MaxHealth": "1d3f5a7c-9e0b-4c2d-a4e6-8f0a2c4e6b8d"
  }
}
```

is built as:

```csharp
StatBlockBuilder.Create("Iron Sword")
    .SetCondition(StatBlockCondition.HasTag(Tags.Equipped))
    .AddTag(Tags.Magical)
    .AddModifier(Stats.Damage, new ValueLogic(5f))
    .AddModifier(Stats.Damage, new LinearLogic { Input = ValueSource.FromAttribute(Stats.Strength, Links.Owner), Coefficient = 0.5f })
    .AddModifier(AttributeReference.Of(Stats.Health, Links.Owner), new ValueLogic(ValueSource.FromAttribute(Stats.MaxHealth, Links.Owner)),
        ModifierType.ClampMax, priority: 1000)
    .Build();
```

The **Stat Block Editor** and the **Entity Profile Editor** write these files (see [StatBlock](StatBlock.md) and [EntityProfile](EntityProfile.md)), and the loaders read them. They are plain text, so you can also write and edit them by hand, and review them in version control.

## Keys

The body names keys by name: `"Damage"`, `"Owner/Strength"` (the steps of a path are separated by `/`). The `keys` table at the end of the file maps each name to the key's GUID, and the GUID is what counts: keys match by GUID (see [Semantic Keys](Semantic%20Keys.md#identity-vs-value)), so a file keeps working when a key is renamed.

-   The editor windows write the table when they save a file. When they load one, keys renamed since it was saved get their current names, which the next save writes.
    
-   Names are the keys' names. If a file uses two keys with the same name (e.g. a `Poison` attribute and a `Poison` tag), the second is called `Poison#2`. A `/` in a key's name is written as `_`.
    
-   **Writing by hand:** a name that isn't in the table is an error when the file is loaded in the game. Load and save the file in its editor window: the window looks the name up in your KeyDomains and adds it to the table. If keys in several domains have that name, write it with the domain's name, e.g. `"Tags.Poison"`.
    
-   **In code**, `StatBlockJson.FromJson(json, findKey)` and `EntityProfileJson.FromJson(json, findKey)` take a function that finds the keys missing from the table (return `SemanticKey.None` for a name you don't know). For example, a game that loads files written by modders can resolve names with its own table of keys.
    

## StatBlock Files

| Property | Builder call | Value |
| ----- | ----- | ----- |
| `statBlock` | `StatBlockBuilder.Create(name)` | The block's name. |
| `condition` | `SetCondition(condition)` | When the block's content applies (see [Conditions](#conditions)). Leave it out for always. |
| `baseValues` | `AddBaseValue(attribute, value)` | Base values set when the block is applied: `{ "Durability": 100 }` |
| `tags` | `AddTag(tag)` | `["Magical"]` |
| `remoteTags` | `AddRemoteTag(tag, path)` | Tags added to other entities: `["Owner/Blessed"]` is the tag `Blessed` on the `Owner`. |
| `pointers` | `AddPointer(alias, target, path)` | `{ "MainStat": "Owner/Strength" }` |
| `modifiers` | `AddModifier(...)` | The modifiers (see below). |
| `keys` | | The key table (see [Keys](#keys)). |

Every property is optional. See [StatBlock](StatBlock.md) for what each part does.

### Modifiers

```json
{ "target": "Owner/Health", "type": "ClampMax", "priority": 1000, "source": "Iron Sword", "value": "Owner/MaxHealth" }
```

| Property | Default | Value |
| ----- | ----- | ----- |
| `target` | | The attribute modified: `"Damage"`, or `"Owner/Damage"` for the Damage of the entity registered as `Owner`. |
| `type` | `Additive` | `Additive`, `Multiplicative`, `Override`, `ClampMin` or `ClampMax` (see [Attribute Modifiers](Attribute%20Modifiers.md#modifier-types-and-order)). |
| `priority` | `0` | Lower priorities apply first. |
| `source` | | The name shown by the Attribute Debugger. |
| the logic | | One property named after the logic that computes the value, e.g. `"linear": { ... }` (see below). |

### Logic

A modifier's logic is named after its class, in camelCase and without the "Logic" suffix: `value`, `linear`, `polynomial`, `clamp`, `min`, `max`, `floor`, `step`, `ratio`, `exponential`, `diminishingReturns`, `scaledTriangular`, `segmented`, `groupTotal` (see [Modifier Logic](Modifier%20Logic.md)). Your own `DistanceBonusLogic` is `distanceBonus`. The class name (`"LinearLogic"`) and the full name with the namespace work too; if two classes have the same name, files use the full name.

Its value is an object with the logic's fields, named in camelCase: `Coefficient` is `coefficient`, and a private `[SerializeField] float _maxRange` is `maxRange`.

```json
{ "target": "Damage", "diminishingReturns": { "input": "Strength", "maxBonus": 50, "softCap": 100 } }
```

-   A field that isn't in the file keeps the class's default value, and files leave out the fields that have it: `"linear": { "input": "Strength" }` has a Coefficient of 1 and an Addend of 0.
    
-   A logic with one field can be written as the field's value: `"value": 5` is `"value": { "value": 5 }`, and `"floor": "Strength"` rounds Strength down.
    
-   An input (a `ValueSource`) is a number, for a constant, or an attribute: `"Strength"`, or `"Owner/Strength"` through a provider path. Attributes are read from the entity the StatBlock is applied to.
    

### Field Values

How each type of field is written, for your own logic classes:

| Field type | JSON |
| ----- | ----- |
| `float`, `double`, `int`, `long` and other numbers | A number: `2.5`. The float and double values NaN and infinity are written `"NaN"`, `"Infinity"` and `"-Infinity"`. |
| `bool` | `true` or `false` |
| `string`, `char` | `"text"` |
| An enum | The value's name: `"High"`. For a `[Flags]` enum: `"Fire, Ice"`. |
| `ValueSource` | A number, or an attribute: `"Owner/Strength"` |
| `SemanticKey` | The key's name, or `null` for none. |
| `AttributeReference` | An attribute: `"Owner/Strength"` |
| `List<T>`, `T[]` | An array: `[1, 2, 3]` |
| A `[Serializable]` class or struct of your own | An object with all its fields: `{ "threshold": 10, "value": 2 }` |
| A `[SerializeReference]` field holding a logic | The logic, named as in a modifier: `{ "linear": { "coefficient": 3 } }` |
| `StatBlockCondition` | A condition (see [Conditions](#conditions)). |
| `AnimationCurve` | `{ "keys": [{ "time": 0, "value": 0, "inTangent": 0, "outTangent": 1 }] }`, plus `"preWrapMode"` and `"postWrapMode"` if they aren't the default. |

Fields that Unity doesn't save (a `Dictionary`, an interface without `[SerializeReference]`, a `[NonSerialized]` field) aren't saved in files either. Files can't hold Unity object references or Unity types other than `AnimationCurve` (such as `Vector3` or `Color`): saving a StatBlock whose logic has such a field set fails, with an error naming the field. Mark the field `[NonSerialized]` if it doesn't need saving.

### Conditions

| JSON | Built with | True while |
| ----- | ----- | ----- |
| `{ "hasTag": "Equipped" }` | `StatBlockCondition.HasTag(Tags.Equipped)` | The entity has the tag. |
| `{ "hasTag": "Owner/Equipped" }` | `StatBlockCondition.HasTag(Tags.Equipped, Links.Owner)` | The entity at the end of the path has it. |
| `{ "lacksTag": "Stunned" }` | `StatBlockCondition.LacksTag(Tags.Stunned)` | The entity doesn't have the tag. |
| `{ "compare": ["Health", "<", 50] }` | `StatBlockCondition.Compare(ValueSource.FromAttribute(Stats.Health), StatBlockCondition.Comparison.Less, 50f)` | The comparison holds: `==`, `!=`, `>`, `<`, `>=` or `<=` between two numbers or attributes. |
| `{ "all": [ ... ] }` | `StatBlockCondition.All(...)` | All the conditions in the list are true. |
| `{ "any": [ ... ] }` | `StatBlockCondition.Any(...)` | Any of them is. |
| `{}` | `StatBlockCondition.Always()` | Always. |

`==` and `!=` allow a difference of 0.001; add `"tolerance": 0.01` next to `"compare"` to change it. For example, "while equipped and Health is below half of MaxHealth":

```json
{
  "all": [
    { "hasTag": "Equipped" },
    { "compare": ["Health", "<", "HalfMaxHealth"] }
  ]
}
```

(`HalfMaxHealth` being an attribute, or a pointer, that holds half of MaxHealth.)

## Entity Profile Files

| Property | Builder call | Value |
| ----- | ----- | ----- |
| `profile` | `ProfileBuilder.Create(name)` | The profile's name. |
| `templates` | `AddTemplate(...)` | The profiles this one builds on, applied first and once per entity: `["Templates/Character"]` (see [Templates](EntityProfile.md#templates)). |
| `parentKey` | `SetParentKey(key)` | When nested in another entity, the key under which it reaches that entity: `"Owner"`. |
| `baseAttributes` | `AddBaseAttribute(attribute, value)` | `{ "Health": 40, "Strength": 8 }` |
| `pools` | `AddPool(resource, max, ...)` | Resources that are spent and restored, and their maximum: `{ "Health": "MaxHealth" }`, or `{ "Mana": { "max": "MaxMana", "onMaxChange": "AddDifference" } }` (see [Resource Pools](Resource%20Pools.md)). |
| `innateTags` | `AddInnateTag(tag)` | `["Undead"]` |
| `linkGroups` | `AddLinkGroup(group)` | `["Inventory"]` |
| `nestedEntities` | `AddNestedEntity(key, ...)` | `{ "RightHand": "Weapons/RustySword" }`: a profile ID, or a profile written in full (see below). |
| `pointers` | `AddPointer(alias, target, path)` | `{ "MainStat": "RightHand/Damage" }` |
| `innateStatBlocks` | `AddInnateStatBlock(...)` | StatBlock IDs and StatBlocks written in full: `["Passives/Brittle", { "statBlock": "Rage", ... }]` |
| `keys` | | The key table (see [Keys](#keys)). |

```json
{
  "profile": "Goblin",
  "templates": ["Templates/Character"],
  "baseAttributes": { "Health": 40, "Strength": 8 },
  "innateTags": ["Undead"],
  "linkGroups": ["Inventory"],
  "nestedEntities": { "RightHand": "Weapons/RustySword" },
  "pointers": { "MainStat": "RightHand/Damage" },
  "innateStatBlocks": [
    "Passives/Brittle",
    {
      "statBlock": "Goblin Frenzy",
      "condition": { "compare": ["Health", "<", 10] },
      "modifiers": [
        { "target": "Damage", "type": "Multiplicative", "value": 1.5 }
      ]
    }
  ],
  "keys": {
    "Health": "9a1b3c5d-7e8f-4a0b-b2c4-6d8e0f1a3b5c",
    "Strength": "5c7e9a1b-3d4f-4b6a-8c0e-1f3a5c7e9b2d",
    "Undead": "2b4d6f8a-0c1e-4a3b-9d5f-7a9c1e3b5d7f",
    "Inventory": "8c0e2a4b-6d8f-4b1a-a3c5-e7f9a1b3c5d6",
    "RightHand": "4e6a8c0d-2f3b-4d5e-b7a9-1c3e5f7a9b0c",
    "MainStat": "6f8b0d2e-4a5c-4e7f-89ab-3d5f7a9c1e2b",
    "Damage": "3a5c7e9b-1d2f-4a6c-8e0b-2c4d6f8a0b1e"
  }
}
```

A nested entity or a template is usually a profile ID, so one weapon profile serves many characters. A profile built in code (`AddNestedEntity(key, weapon => ...)`, `AddTemplate(profile)`) is written in full instead: `{ "RightHand": { "profile": "Bone Cleaver", "baseAttributes": { "Damage": 75 } } }`. The Entity Profile Editor only shows nested entities and templates by ID, so it doesn't open files like that; edit them as text.

## Reading and Writing in Code

```csharp
using ReactiveSolutions.AttributeSystem.Core.Data;

string json = StatBlockJson.ToJson(ironSword);
StatBlock copy = StatBlockJson.FromJson(json);

string profileJson = EntityProfileJson.ToJson(goblinProfile);
EntityProfile goblin = EntityProfileJson.FromJson(profileJson);
```

`StatBlockJsonLoader.Load(id)` and `EntityProfileJsonLoader.Load(id)` read files by ID from `Resources/Data/StatBlocks` and `Resources/Data/EntityProfiles`; see [StatBlock](StatBlock.md) and [EntityProfile](EntityProfile.md).

## Errors

Files are read strictly, so that a typo is reported instead of silently ignored: an unknown property or logic field, a modifier with two logics, a property written twice, and a key missing from the table are errors. Each error says where it is, as a path in the file and a line and column, and lists the names that are valid there:

```
modifiers[1].linear.cofficient: the Linear logic has no field 'cofficient' (its fields are input, coefficient, addend) (line 7, column 66)
```

-   The loaders log the error (`[StatBlockJsonLoader] Failed to parse JSON for Weapons/IronSword: ...`) and don't apply the file. The editor windows show it in a dialog.
    
-   `FromJson` throws a `JsonFormatException`, with the position in its `Line` and `Column`.
    
-   `ToJson` throws an `InvalidOperationException` for data a file can't hold: a logic field of a type files can't hold, a provider path with an empty entry, or a profile nested in itself.
    
-   JSON has no comments, and no comma after the last item of a list or object.
    

## Code Stripping

IL2CPP builds strip code that nothing refers to (see Unity's **Managed Stripping Level** setting), and a logic class that only JSON files name can be removed with it. The built-in logic classes are marked `[Preserve]`. Mark your own logic classes, and the `[Serializable]` classes they hold, with `[Preserve]` (namespace `UnityEngine.Scripting`) too.
