# Status Effects

## Overview

A **status effect** is a condition that lasts on an entity: poisoned, hasted, stunned, regenerating. While the entity has it:

-   its **StatBlock** applies to the entity (e.g. the Poisoned tag and -20% MoveSpeed), once per stack;
-   every **tick**, its tick [effect](Effects.md) is applied from the status's source to the entity (e.g. 3 damage).

It ends when its duration runs out, when its condition stops holding, or when it is removed.

```json
{
  "status": "Poison",
  "categories": ["Debuff"],
  "condition": { "compare": ["Target/Health", ">", 0] },
  "duration": 5,
  "stacking": "Stack",
  "maxStacks": 3,
  "statBlock": {
    "tags": ["Poisoned"],
    "modifiers": [
      { "target": "MoveSpeed", "type": "Multiplicative", "value": 0.8 }
    ]
  },
  "tick": { "every": 1, "effect": "Debuffs/PoisonTick" }
}
```

For 5 seconds, the entity is Poisoned and slower, and takes whatever the `Debuffs/PoisonTick` effect deals every second. Poisoning it again adds a stack, up to 3. The poison ends early if the entity's Health reaches 0.

```csharp
StatusEffect poison = StatusEffectJsonLoader.Load("Debuffs/Poison");
ActiveStatusEffect active = poison.Apply(goblin, hero); // The goblin poisons the hero

hero.TickStatusEffects(Time.deltaTime); // Every frame. An EntityController does it for you.
```

`StatusEffect` is in `ReactiveSolutions.AttributeSystem.Core.Data`, `ActiveStatusEffect` in `ReactiveSolutions.AttributeSystem.Core`, and `StatusEffectBuilder` in `ReactiveSolutions.AttributeSystem.Core.Builders`. The examples use keys from classes generated from your KeyDomains (see [Semantic Keys](Semantic%20Keys.md)).

## Time

Status effects advance when you tell them to: `entity.TickStatusEffects(deltaTime)`. Durations and tick intervals are in whatever unit you advance them in.

-   **Seconds:** an [EntityController](EntityController.md) calls `TickStatusEffects(Time.deltaTime)` on its entity every frame (its **Tick Status Effects** option). For entities without one, call it yourself, e.g. from your game loop.
    
-   **Turns:** turn **Tick Status Effects** off, and call `TickStatusEffects(1)` at the end of each turn. A duration of 3 is then three turns.
    
-   **Big steps work.** A step of 10 on a status with 3 seconds left ticks as many times as fit in those 3 seconds, and no more.
    
-   **Chance rolls.** `TickStatusEffects(deltaTime, random)` rolls the chances of tick and on-expire effects with your `System.Random`, as `Effect.Apply` does.
    

## While It Lasts

| Property | What it does |
| ----- | ----- |
| `statBlock` | A StatBlock applied to the entity while the status lasts, once per stack: its ID (`"Debuffs/Poison"`) or a StatBlock written in the status. Its paths are the entity's own, as when you apply a StatBlock to it (`"MoveSpeed"`, `"Owner/Strength"`). |
| `tick` | `{ "every": 1, "effect": ... }`: every `every`, the effect is applied from the status's source to the entity, once per stack. The first tick comes one interval after the status is applied. |
| `onApply` | Effects applied from the source to the entity each time the status is applied, including to refresh or stack it. |
| `onExpire` | Effects applied when the status runs out. Not when it is removed, or when its condition stops holding. |

An effect in a status is an effect file's ID, or an effect written in full (`{ "actions": [ ... ] }`). Its paths start with Source or Target, as in any [effect](Effects.md#source-and-target): the source is whoever applied the status, and the target is the entity that has it. So do the paths of the status's `condition` and `duration`.

## Duration and Stacking

-   `"duration"` is a number, an attribute or a formula, computed each time the status is applied, e.g. two turns per level of whoever applied it: `{ "linear": { "input": "Source/Level", "coefficient": 2 } }`. A status without a duration lasts until it is removed.
    
-   `"stacking"` decides what applying the status again does, while the entity still has it:
    

| `stacking` | Applying it again |
| ----- | ----- |
| `Refresh` (the default) | Restarts its duration. |
| `Extend` | Adds its duration to the time left. |
| `Stack` | Adds a stack, up to `maxStacks`, and restarts its duration. Each stack applies the StatBlock and the ticks once more: 3 stacks of the poison above are 0.8 x 0.8 x 0.8 MoveSpeed and 3 ticks a second. |
| `Independent` | Applies a separate instance, with its own duration, up to `maxStacks` instances. At the most, the instance that would end first starts over. |
| `Ignore` | Does nothing: the status can't be applied again until it ends. |

-   `"maxStacks"`: the most stacks (`Stack`) or instances (`Independent`) an entity can have. 0, the default, is no limit.
    
-   **Which statuses are the same:** a status loaded from the same file (even loaded twice) or the same object. Applying it again follows its stacking rule, and whoever applied it last becomes its source.
    

## The Condition

A status is only applied if its condition holds, and it ends as soon as the condition stops holding. `{ "compare": ["Target/Health", ">", 0] }` ends a poison when its entity falls. `{ "lacksTag": "Target/PoisonImmune" }` makes a tag an immunity.

## Removing Them

| Code | Removes |
| ----- | ----- |
| `active.Remove()` (or `Dispose()`) | That instance. |
| `entity.RemoveStatusEffect(status)` | Every instance of that status on the entity. |
| `entity.RemoveStatusEffects(Tags.Debuff)` | Every status in that category: a cleanse. |
| `entity.RemoveStatusEffects()` | Every status. |

`"categories"` are labels for removing statuses (Debuff, Magic, Poison), and they are keys like any other. Unlike the StatBlock's tags, they aren't added to the entity. Removing a status doesn't apply its on-expire effects, and disposing an entity removes its statuses.

An effect cleanses too, with `"removeStatuses"`: an Antidote is `{ "effect": "Antidote", "removeStatuses": ["Debuff"] }`.

## Applying Them From Effects

An effect applies status effects after its actions, by ID. A string alone is the ID, and an object adds a condition, a chance and who gets it:

```json
{
  "effect": "Weapon Hit",
  "actions": [
    { "target": "Target/Health", "type": "Reduce", "value": "Source/AttackPower" }
  ],
  "statuses": [
    { "status": "Debuffs/Poison", "condition": { "hasTag": "Source/MainHand/Venomous" }, "chance": 0.3 }
  ]
}
```

A venomous weapon poisons 30% of the time. `"to": "Source"` gives the status to the source instead: a self-buff. The effect's result lists the statuses it applied (`result.Statuses`) and how many it removed (`result.StatusesRemoved`).

## Watching Them

`entity.StatusEffects` lists the entity's statuses, in the order they were applied. It is a reactive collection, so a UI can follow it with `ObserveAdd()` and `ObserveRemove()`. Each entry is an `ActiveStatusEffect`:

| Member | Description |
| ----- | ----- |
| `Status`, `Name` | The status effect, and its name. |
| `Target`, `Source` | The entity that has it, and the one that applied it last (may be null). |
| `Stacks`, `ObservableStacks` | Its stacks: 1, or more for a status that stacks. |
| `TimeLeft`, `ObservableTimeLeft`, `Duration` | The time left, and the duration it had when last applied, e.g. for a timer bar. Infinity for a status that lasts until removed. |
| `IsActive`, `Ended` | Whether it still lasts; `Ended` emits why it ended: `Expired`, `Removed` or `ConditionFailed`. |
| `Remove()` | Removes it now. |

`entity.GetStatusEffect(status)` finds the instance an entity has, or returns null. `ToString()` reads like "Poison x2 (3.5 left)".

## In Code

```csharp
StatusEffect poison = StatusEffectBuilder.Create("Poison")
    .AddCategory(Tags.Debuff)
    .SetCondition(StatBlockCondition.Compare(Effect.Target(Stats.Health), StatBlockCondition.Comparison.Greater, 0f))
    .SetDuration(5f)
    .SetStacking(StatusStacking.Stack, maxStacks: 3)
    .SetStatBlock("Debuffs/Poison")
    .SetTick(1f, "Debuffs/PoisonTick")
    .Build();

Effect venomousHit = EffectBuilder.Create("Venomous Hit")
    .Reduce(Effect.Target(Stats.Health), Effect.Source(Stats.AttackPower))
    .ApplyStatus("Debuffs/Poison", chance: 0.3f)
    .Build();
```

-   A status built without `SetDuration` lasts until it is removed.
-   `SetStatBlock`, `SetTick`, `AddOnApply` and `AddOnExpire` take a file's ID or an object.
-   `EffectBuilder.ApplyStatus` also takes a `StatusEffect` built in code. An effect file can only save a status by its ID, though.

## Files and the Status Effect Editor

Status effect files live in `Resources/Data/StatusEffects`. `StatusEffectJsonLoader.Load("Debuffs/Poison")` loads `Resources/Data/StatusEffects/Debuffs/Poison.json`. If it can't, it logs an error and returns null.

-   **Tools > Attribute System > Status Effect Editor** creates and edits them. **Assets > Create > Attribute System > Status Effect** starts a new one, and double-clicking a file opens it.
-   A string field marked `[StatusEffectID]` shows a dropdown of them.
-   In code, `StatusEffectJson.ToJson(status)` and `StatusEffectJson.FromJson(json)` convert statuses to and from JSON.

| Property | Builder call | Value |
| ----- | ----- | ----- |
| `status` | `StatusEffectBuilder.Create(name)` | The status's name. |
| `categories` | `AddCategory(key)` | Labels to remove it by: `["Debuff"]`. |
| `condition` | `SetCondition(condition)` | It is only applied, and only lasts, while this holds. |
| `duration` | `SetDuration(value)` | How long it lasts. Leave it out for until removed. |
| `stacking` | `SetStacking(stacking, maxStacks)` | `Refresh`, `Extend`, `Stack`, `Independent` or `Ignore`. |
| `maxStacks` | `SetStacking(stacking, maxStacks)` | The most stacks or instances, 0 for no limit. |
| `statBlock` | `SetStatBlock(...)` | A StatBlock ID, or a StatBlock written in full. |
| `tick` | `SetTick(interval, ...)` | `{ "every": 1, "effect": "Debuffs/PoisonTick" }` |
| `onApply`, `onExpire` | `AddOnApply(...)`, `AddOnExpire(...)` | Effect IDs and effects written in full. |
| `keys` | | The key table (see [Keys](JSON%20Format.md#keys)). |

## Good to Know

-   **The StatBlock reads the entity.** A status's StatBlock applies to the entity that has it, so its modifiers can't read the source's attributes. To use them, write the value with an on-apply effect. For example, a shield that absorbs twice the caster's SpellPower Adds `Source/SpellPower x 2` to `Target/Shield` when it is applied, and Sets `Target/Shield` to 0 when it expires.
    
-   **Base values in the StatBlock are set for good**, as with any StatBlock: use modifiers for what should end with the status.
    
-   **A status can't apply itself.** While its on-apply effects run, applying it to the same entity again (from those effects, or through another status's) does nothing, so they can't loop forever.
    
-   **Saving:** status effects aren't part of a save game yet.
