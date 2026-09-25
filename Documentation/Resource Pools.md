# Resource Pools

## Overview

Health, Mana and Stamina are resources that are spent and restored, unlike stats such as Strength. A **pool** makes an attribute behave like one:

-   The amount is the attribute's base value, and it always stays between 0 and the pool's maximum: an attribute such as MaxHealth, or a constant. Every write is kept in range, `SetOrUpdateBaseValue` included, so healing a full pool changes nothing and the next hit counts in full.
    
-   A new pool is full, and stays full while its maximum settles (for example, while a profile's formulas apply) until it is first used.
    
-   Once it has been used, the pool's `OnMaxChange` decides what a change of the maximum does to the amount:
    

| `OnMaxChange` | 70/100, then the maximum drops to 50 | then goes back to 100 |
| ----- | ----- | ----- |
| `KeepPercent` (the default) | 35/50 | 70/100 |
| `AddDifference` | 20/50 | 70/100 |
| `KeepAmount` | 50/50 | 50/100 |

With `KeepPercent` and `AddDifference`, a temporary "+50 Max Health" buff costs no health when it ends. With `KeepAmount`, health above a lowered maximum is lost for good.

The examples use keys from classes generated from your KeyDomains (see [Semantic Keys](Semantic%20Keys.md)).

## In a Profile

A profile lists its pools and their maximum. Templates usually define them, since every character has Health (see [Templates](EntityProfile.md#templates)):

```json
{
  "profile": "Character",
  "pools": {
    "Health": "MaxHealth",
    "Mana": { "max": "MaxMana", "onMaxChange": "AddDifference" }
  }
}
```

-   A maximum is an attribute (`"MaxHealth"`, or `"Owner/MaxHealth"` through a provider path) or a number (`"Stamina": 100`).
    
-   `ApplyProfile` creates the pools after the profile's base attributes. A pool that a profile defines again (for example, one its template also defines) replaces the earlier one and keeps its amount.
    
-   In code: `ProfileBuilder.AddPool(Stats.Health, ValueSource.FromAttribute(Stats.MaxHealth))`.
    

## In Code

```csharp
ResourcePool health = hero.GetPool(Stats.Health); // or hero.AddPool(Stats.Health, ValueSource.FromAttribute(Stats.MaxHealth))

float dealt = health.Reduce(30f);   // Damage: returns how much was removed (never below 0)
float healed = health.Restore(50f); // Healing: returns how much was added (never above Max)

ResourcePool mana = hero.GetPool(Stats.Mana);
if (mana.TrySpend(15f))
{
    // Spent: cast the spell
}

health.Depleted.Subscribe(_ => Debug.Log("The hero falls")); // Each time Health drops to 0
```

| Member | Description |
| ----- | ----- |
| `Current`, `Max` | The amount and the maximum. |
| `Percent` | `Current / Max`, from 0 to 1 (0 when Max is 0). |
| `IsEmpty`, `IsFull` | Whether the amount is 0, or Max. |
| `Reduce(amount)`, `Restore(amount)` | Remove or add up to `amount`, and return how much actually changed. |
| `TrySpend(amount)` | Removes `amount` only if the pool holds that much, and returns whether it did. |
| `Set(amount)`, `Fill()` | Set the amount (kept between 0 and Max), or fill the pool. |
| `ObservableCurrent`, `ObservableMax` | The amount and the maximum as they change, e.g. for a health bar. |
| `Depleted` | Emits each time the amount drops to 0. |

`AttributeProgressBar` works with pools as with any attribute: bind it to Health and MaxHealth.

Damage and healing can also be data: an [effect](Effects.md) whose action Reduces or Adds to `"Target/Health"` changes the pool the same way, and a cost of `"Source/Mana"` spends like `TrySpend`.

## Good to Know

-   **Don't set a pool's attribute in base attributes.** A pool starts full; to start it at another amount, call `Set` after the entity is created. A base value written after the pool exists counts as using the pool, and is kept within the maximum it has at that moment.
    
-   **Modify the maximum, not the amount.** "+50 Max Health while blessed" is a modifier on MaxHealth. A modifier on the pool's own attribute changes the attribute's final value (`ObservableValue`), but not the pool's amount.
    
-   `Entity.GetPool(key)` returns `null` for an attribute that isn't a pool, and `Entity.Pools` lists an entity's pools. Disposing the entity disposes them.
