# Effects

## Overview

An **effect** is a one-off change that one entity makes to another: a sword hit, a fireball, a healing potion, a level up. Like a StatBlock it is data, saved as a JSON file, but it works differently:

| | StatBlock | Effect |
| ----- | ----- | ----- |
| What it changes | Final values, through modifiers | Base values and pools |
| How long | While it is applied: disposing its handle undoes it | For good: there is nothing to undo |
| Its numbers | Follow their inputs as they change | Are computed once, when it is applied |
| Who | The entity it is applied to | A source and a target |

```json
{
  "effect": "Fireball",
  "condition": { "lacksTag": "Source/Silenced" },
  "costs": { "Source/Mana": 15 },
  "actions": [
    {
      "target": "Target/Health",
      "type": "Reduce",
      "linear": { "input": "Source/SpellPower", "coefficient": 1.5 }
    }
  ]
}
```

The caster (the source) pays 15 Mana, and the target loses 1.5 times the caster's SpellPower in Health, unless the caster is Silenced. In code:

```csharp
Effect fireball = EffectJsonLoader.Load("Spells/Fireball");

EffectResult result = fireball.Apply(mage, goblin);
if (result.Status == EffectStatus.CannotPay) ShowMessage("Not enough Mana");
float damage = -result.ChangeOf(goblin, Stats.Health);
```

The examples use keys from classes generated from your KeyDomains (see [Semantic Keys](Semantic%20Keys.md)). Namespaces: `Effect` is in `ReactiveSolutions.AttributeSystem.Core.Data`, `EffectResult` and `EffectRoles` in `ReactiveSolutions.AttributeSystem.Core`, and `EffectBuilder` in `ReactiveSolutions.AttributeSystem.Core.Builders`.

## Source and Target

Everything an effect reads or changes belongs to its source or to its target, so every path in an effect starts with one of them:

-   `"Source/SpellPower"` is the caster's SpellPower, and `"Target/Health"` the target's Health.
-   `"Target/MainHand/Durability"` is the Durability of the target's weapon.

This goes for action targets, costs, the inputs of formulas and conditions (`{ "hasTag": "Target/Undead" }`).

-   **The same entity, or none.** The source can also be the target: a potion is `potion.Apply(hero, hero)`. It can be null, for a trap. Then the source's attributes read as 0, and actions on it are skipped.
    
-   **In code**, `Effect.Source(Stats.SpellPower)` and `Effect.Target(Stats.Health)` are these references. `Effect.Target(Stats.Durability, Links.MainHand)` goes further, and conditions take the role as their path: `StatBlockCondition.HasTag(Tags.Undead, EffectRoles.Target)`.
    
-   **In the Inspector and the Effect Editor**, a path's first key is **Source** or **Target**, from the package's **Effect Roles** KeyDomain (`EffectRoles.Source` and `EffectRoles.Target` in code). Don't use **Generate Static Class** on that domain: the package already has the `EffectRoles` class, and a second one would clash with it.
    
-   **In files**, they are written by name, and don't go in the key table. A key of your own named "Target" still works after the first step: `"Source/Target/Health"` is the Health of the source's Target.
    

## What Happens When It Is Applied

1.  **The condition.** If the effect has one and it doesn't hold, nothing happens (`Status` is `ConditionNotMet`).
    
2.  **The costs.** If they can't all be paid, nothing happens: `Status` is `CannotPay`, and `UnpaidCost` says which cost (e.g. Source/Mana). Two costs of the same resource are added up.
    
3.  **The actions, in order.** Each one checks its own condition, rolls its chance, computes its amount and changes its attribute. An action sees the changes made by the ones before it.
    
4.  **Status effects.** The effect removes from the target the status effects in its `removeStatuses` categories (a cleanse), then applies its `statuses` (see [Status Effects](Status%20Effects.md#applying-them-from-effects)).
    

Amounts are computed when the effect is applied, from the attributes as they are then. Applying an effect doesn't change it, so one effect can be applied any number of times, to any entities.

### Actions

| `type` | On a pool (e.g. Health) | On another attribute (e.g. Level) |
| ----- | ----- | ----- |
| `Add` (the default) | Heals, up to the pool's maximum. | Adds to the base value: +1 Level. |
| `Reduce` | Damages, down to 0. | Subtracts from the base value. |
| `Set` | Sets the amount, kept between 0 and the maximum. | Sets the base value. |

-   `Add` and `Reduce` only go their own way: an amount of 0 or less does nothing, so damage that armor brings below 0 doesn't heal. Neither does an amount that isn't a finite number (from a formula that divides by 0, say).
    
-   An attribute that the entity doesn't have is created, as `SetOrUpdateBaseValue` does. An action on an entity that isn't there (e.g. `"Target/MainHand/Durability"` on a target with nothing in its MainHand) is skipped.
    
-   `"condition"`: the action only happens if this holds when its turn comes. After a hit, `{ "compare": ["Target/Health", "<", 20] }` makes an execute.
    
-   `"chance"`: how likely the action is to happen, from 0 to 1: a number, or an attribute such as `"Source/CritChance"` (see [Chance](#chance)).
    

### Costs

`"costs": { "Source/Mana": 15, "Source/Stamina": 5 }` spends from pools, or from the base value of other attributes (e.g. Gold). An amount can be a number, an attribute or a formula.

## Amounts Are Formulas

An action's amount is a logic, as a modifier's value is: `"value": 40`, `"value": "Source/SpellPower"` or `"linear": { ... }` (see [Modifier Logic](Modifier%20Logic.md)). A logic's inputs can be formulas too, so one formula can combine the attributes of both entities:

```json
{
  "target": "Target/Health",
  "type": "Reduce",
  "ratio": {
    "dividend": { "linear": { "input": "Source/AttackPower", "coefficient": 100 } },
    "divisor": { "linear": { "input": "Target/Defense", "addend": 100 } }
  }
}
```

That is AttackPower x 100 / (Defense + 100): an attack of 40 against a Defense of 25 deals 32. See [ValueSource](ValueSource.md#formulas).

## Chance

```json
{ "target": "Target/Health", "type": "Reduce", "chance": "Source/CritChance", "value": "Source/AttackPower" }
```

-   `Apply` rolls with a shared `System.Random`. To make the rolls repeatable (a seeded game, a replay, a test), pass your own: `effect.Apply(source, target, new System.Random(seed))`. To choose the rolls in a test, derive from `System.Random` and override `Sample()`.
    
-   A chance of 1 or more always happens, and one of 0 or less never does. Neither rolls.
    

## The Result

`Apply` returns an `EffectResult`:

| Member | Description |
| ----- | ----- |
| `Status`, `Applied` | `Applied`, `ConditionNotMet` or `CannotPay`. `Applied` is `Status == EffectStatus.Applied`. |
| `UnpaidCost` | The cost that couldn't be paid (e.g. Source/Mana), when `Status` is `CannotPay`. |
| `Statuses`, `StatusesRemoved` | The status effects it applied (as their entities now have them), and how many it removed. |
| `Changes` | Every change, in order: first the costs (`IsCost`), then the actions. Each has `Entity`, `Attribute`, `Before`, `After` and `Amount` (`After - Before`). |
| `ChangeOf(entity, attribute)` | The total change of one attribute: `-result.ChangeOf(goblin, Stats.Health)` is the damage dealt. |
| `Effect`, `Source`, `Target` | What was applied, and from whom to whom. |

Changes count a pool's limits: 30 damage to a target with 10 Health is -10. When an effect empties a pool, the pool's `Depleted` fires, as with any other damage.

## In Code

```csharp
Effect fireball = EffectBuilder.Create("Fireball")
    .SetCondition(StatBlockCondition.LacksTag(Tags.Silenced, EffectRoles.Source))
    .AddCost(Effect.Source(Stats.Mana), 15f)
    .Reduce(Effect.Target(Stats.Health), new LinearLogic { Input = Effect.Source(Stats.SpellPower), Coefficient = 1.5f })
    .Build();
```

-   `Add`, `Reduce` and `Set` take an amount: a number, an attribute (`Effect.Source(Stats.AttackPower)`) or a logic. After it come an optional condition and chance.
    
-   `AddAction(target, type, logic, condition, chance)` is the general form.
    
-   The builder only accepts paths that start with the source or the target: `Reduce(AttributeReference.Of(Stats.Health), 5f)` throws an `ArgumentException`.
    

## Files and the Effect Editor

Effect files live in `Resources/Data/Effects`. `EffectJsonLoader.Load("Spells/Fireball")` loads `Resources/Data/Effects/Spells/Fireball.json`. If it can't, it logs an error and returns an effect that does nothing.

-   **Window > Attribute System > Effect Editor** creates and edits them.
-   A string field marked `[EffectID]` shows a dropdown of them: `[EffectID] public string Attack;`.
-   In code, `EffectJson.ToJson(effect)` and `EffectJson.FromJson(json)` convert effects to and from JSON.

| Property | Builder call | Value |
| ----- | ----- | ----- |
| `effect` | `EffectBuilder.Create(name)` | The effect's name. |
| `condition` | `SetCondition(condition)` | The effect only happens if this holds (see [Conditions](JSON%20Format.md#conditions)). |
| `costs` | `AddCost(resource, amount)` | `{ "Source/Mana": 15 }` |
| `actions` | `AddAction(...)` | The actions, in order. |
| `removeStatuses` | `RemoveStatuses(category)` | Categories of status effects to remove from the target: `["Debuff"]`. |
| `statuses` | `ApplyStatus(...)` | Status effects to apply, by ID: `["Debuffs/Poison"]`, or `{ "status": "Debuffs/Poison", "to": "Source", "condition": ..., "chance": 0.3 }`. |
| `keys` | | The key table (see [Keys](JSON%20Format.md#keys)). |

An action:

| Property | Default | Value |
| ----- | ----- | ----- |
| `target` | | The attribute changed: `"Target/Health"`. |
| `type` | `Add` | `Add`, `Reduce` or `Set`. |
| `condition` | Always | The action only happens if this holds. |
| `chance` | 1 | How likely it is to happen, from 0 to 1. |
| the logic | | One property named after the logic that computes the amount, e.g. `"value": 40` or `"linear": { ... }`. |

Files are read strictly (see [JSON Format](JSON%20Format.md#errors)). A path that doesn't start with Source or Target is an error, which says where it is and what to write instead:

```
actions[0].target: in an effect, "Health" must start with Source or Target, the entity it is on: e.g. "Target/Health" (line 7, column 17)
```

## Good to Know

-   **Lasting changes are StatBlocks.** "+20 MaxHealth while blessed" is a StatBlock. An effect changes base values and pools for good, so use it for damage, healing, costs and permanent gains (a level, a point of Strength from a tome).
    
-   **Over time.** What lasts, or ticks, is a [status effect](Status%20Effects.md): a poison is a status whose tick applies a Poison Tick effect every second.
    
-   **How paths work.** An effect reads its formulas and conditions through a temporary entity whose providers are the source and the target. That is why paths start with Source or Target, and why everything that works in a StatBlock (paths, pointers, link groups, formulas) works in an effect too.
