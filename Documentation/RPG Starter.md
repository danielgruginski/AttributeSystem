# RPG Starter

The package comes with a sample RPG: import it with **Window > Package Manager > Attribute System > Samples > RPG Starter > Import**, add the **RPG Starter Demo** component to an empty GameObject, and press Play. A Knight and a Mage fight a Goblin; buttons let you attack, cast, drink potions, swap weapons, carry an anvil, poison the Knight, level up and bless the party.

This page shows how it is built. The rules and numbers are data (JSON files in the sample's `Resources/Data/EntityProfiles/RPGStarter` and `Resources/Data/StatBlocks/RPGStarter` folders); the code (`Scripts/RPGGame.cs`) only spawns characters, runs the fights and moves items around. Key tables are left out of the excerpts below.

| Piece | Data | Package feature |
| ----- | ----- | ----- |
| Stats and formulas every character shares | `Templates/Character` | [Templates](EntityProfile.md#templates), [Modifier Logic](Modifier%20Logic.md) |
| Health and Mana | `Templates/Character`, `Templates/Caster` | [Resource Pools](Resource%20Pools.md) |
| Weapons and armor that add to their owner | `Templates/Weapon`, `Templates/Armor` | Parent keys, [conditions](JSON%20Format.md#conditions) |
| Inventory and encumbrance | `Templates/InventoryHolder` | [Link groups and group totals](LinkGroup.md#4-totals-over-a-group) |
| The party's aura | `Auras/Leadership` | [LinkGroup](LinkGroup.md) |
| The goblin's frenzy | `Passives/GoblinFrenzy` | Conditions |

## A Character Is Templates Plus Numbers

```json
{
  "profile": "Knight",
  "templates": ["RPGStarter/Templates/Character", "RPGStarter/Templates/InventoryHolder"],
  "baseAttributes": { "Strength": 14, "Vitality": 12 },
  "nestedEntities": {
    "MainHand": "RPGStarter/Items/LongSword",
    "Body": "RPGStarter/Items/ChainMail"
  }
}
```

The Character template gives every character its stats (10 in each), its Health pool and its formulas; the Knight only says what is different about him. The Mage builds on Caster (which builds on Character) instead, and the Goblin on Character alone. The Knight's sword and armor are nested entities, created with him.

## Formulas Live in the Templates

```json
{
  "statBlock": "Character Rules",
  "modifiers": [
    { "target": "MaxHealth", "linear": { "input": "Vitality", "coefficient": 10 } },
    { "target": "MaxHealth", "linear": { "input": "Level", "coefficient": 5 } },
    { "target": "AttackPower", "linear": { "input": "Strength", "coefficient": 2 } },
    { "target": "CritChance", "diminishingReturns": { "input": "Dexterity", "maxBonus": 0.5, "softCap": 50 } },
    { "target": "HealthPercent", "ratio": { "dividend": "Health", "divisor": "MaxHealth" } }
  ]
}
```

The Knight's MaxHealth is 12 x 10 + 1 x 5 = 125, and it follows Vitality and Level as they change: leveling up is `SetOrUpdateBaseValue(Level, level + 1)`. `HealthPercent` is a helper attribute that conditions can compare against.

## Health and Mana Are Pools

The Character template has `"pools": { "Health": "MaxHealth" }`, and Caster adds Mana. A pool starts full and stays between 0 and its maximum, so the game code is short:

```csharp
float damage = Get(attacker, RPGStats.AttackPower) * 100f / (100f + Get(target, RPGStats.Defense));
target.GetPool(RPGStats.Health).Reduce(damage);

if (caster.GetPool(RPGStats.Mana).TrySpend(15f)) { /* cast the fireball */ }

drinker.GetPool(RPGStats.Health).Restore(40f); // Never above MaxHealth
```

`Depleted` logs "... falls." when a character's Health reaches 0. When the Knight levels up or the party is blessed (+20 MaxHealth), Health keeps its percentage, and ending the blessing costs no Health.

## Gear Adds to Its Owner

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

A sword is just `"templates": ["RPGStarter/Templates/Weapon"]` with its Damage and Weight. `parentKey` lets a weapon reach the character it belongs to, as its Owner; the condition makes it count only when the Owner is a Character. Armor works the same way with Defense, and the Oak Staff adds SpellPower when its Owner is a Caster.

Swapping weapons at runtime moves them between the Inventory and the hand, with `Detach` and `Attach`, which link both ways:

```csharp
var previous = character.Detach(RPGLinks.MainHand);
if (previous != null) inventory.AddMember(previous);

inventory.RemoveMember(item);
character.Attach(RPGLinks.MainHand, item); // The item reaches the character as its Owner
```

## Inventory and Weight

```json
{
  "profile": "Inventory Holder",
  "baseAttributes": { "CarryCapacity": 10 },
  "linkGroups": ["Inventory"],
  "innateStatBlocks": [
    {
      "statBlock": "Carrying",
      "modifiers": [
        { "target": "CarryCapacity", "linear": { "input": "Strength", "coefficient": 2 } },
        { "target": "CarriedWeight", "groupTotal": { "group": "Inventory", "attribute": "Weight" } }
      ]
    },
    {
      "statBlock": "Encumbered",
      "condition": { "compare": ["CarriedWeight", ">", "CarryCapacity"] },
      "tags": ["Encumbered"],
      "modifiers": [
        { "target": "MoveSpeed", "type": "Multiplicative", "value": 0.5 }
      ]
    }
  ]
}
```

Items in the Inventory group add up to CarriedWeight. Pick up the anvil (40) and the Knight (capacity 38) is Encumbered and moves at half speed until he drops it.

## Auras and Reactions

The Knight leads a party: a link group of the Knight and the Mage. `party.ApplyStatBlock(leadership)` gives each member +10% AttackPower, including members who join later.

The Goblin has an innate StatBlock that reacts to its own state:

```json
{
  "statBlock": "Goblin Frenzy",
  "condition": { "compare": ["HealthPercent", "<", 0.5] },
  "tags": ["Enraged"],
  "modifiers": [
    { "target": "AttackPower", "type": "Multiplicative", "value": 1.5 }
  ]
}
```

## What the Sample Does by Hand

The poison applies its StatBlock (the Poisoned tag and slower movement), but its duration and its damage over time are counted in `RPGGame.Tick`. The package doesn't have timed effects yet.

## Make It Yours

-   **A new class:** a `Rogue` template built on Character, with more Dexterity; then a hero built on Rogue and Inventory Holder.
-   **A new item:** a profile built on Weapon or Armor, with its numbers.
-   **A new monster:** its templates, its numbers, and a StatBlock for how it fights (like the Goblin's frenzy).
-   **A new rule for everyone:** add a modifier to the Character Rules, e.g. Defense from Vitality.
