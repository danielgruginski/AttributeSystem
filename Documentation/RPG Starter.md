# RPG Starter

The package comes with a sample RPG: import it with **Window > Package Manager > Attribute System > Samples > RPG Starter > Import**, add the **RPG Starter Demo** component to an empty GameObject, and press Play. A Knight and a Mage fight a Goblin; buttons let you attack, cast, drink potions, swap weapons, carry an anvil, poison the Knight and cure him, level up and bless the party.

This page shows how it is built. The rules and numbers are data: JSON files in the `RPGStarter` folders of the sample's `Resources/Data/EntityProfiles`, `StatBlocks`, `Effects` and `StatusEffects`. The code (`Scripts/RPGGame.cs`) only spawns characters, applies effects, moves items around and counts time. Key tables are left out of the excerpts below.

| Piece | Data | Package feature |
| ----- | ----- | ----- |
| Stats and formulas every character shares | `Templates/Character` | [Templates](EntityProfile.md#templates), [Modifier Logic](Modifier%20Logic.md) |
| Health and Mana | `Templates/Character`, `Templates/Caster` | [Resource Pools](Resource%20Pools.md) |
| Hits, spells, potions, poison ticks and leveling up | `Combat/WeaponHit`, `Spells/Fireball`, `Consumables/HealingPotion`, `Debuffs/PoisonTick`, `Progression/LevelUp` | [Effects](Effects.md) |
| The poison, the venomous dagger and the antidote | `Debuffs/Poison` (a status effect), the Weapon Hit's `statuses`, `Consumables/Antidote` | [Status Effects](Status%20Effects.md) |
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

The Knight's MaxHealth is 12 x 10 + 1 x 5 = 125, and it follows Vitality and Level as they change: leveling up is the Level Up effect, which adds 1 to Level. `HealthPercent` is a helper attribute that conditions can compare against.

## Health and Mana Are Pools

The Character template has `"pools": { "Health": "MaxHealth" }`, and Caster adds Mana. A pool starts full and stays between 0 and its maximum, so the effects that damage and heal need no checks of their own.

`Depleted` logs "... falls." when a character's Health reaches 0:

```csharp
entity.GetPool(RPGStats.Health).Depleted.Subscribe(_ => Log($"{entity.Name} falls."));
```

When the Knight levels up or the party is blessed (+20 MaxHealth), Health keeps its percentage, and ending the blessing costs no Health.

## Hits, Spells and Potions Are Effects

A weapon hit is an effect from the attacker (its source) to its target:

```json
{
  "effect": "Weapon Hit",
  "condition": {
    "all": [{ "compare": ["Source/Health", ">", 0] }, { "compare": ["Target/Health", ">", 0] }]
  },
  "actions": [
    {
      "target": "Target/Health",
      "type": "Reduce",
      "ratio": {
        "dividend": { "linear": { "input": "Source/AttackPower", "coefficient": 100 } },
        "divisor": { "linear": { "input": "Target/Defense", "addend": 100 } }
      }
    },
    {
      "target": "Target/Health",
      "type": "Reduce",
      "chance": "Source/CritChance",
      "ratio": {
        "dividend": { "linear": { "input": "Source/AttackPower", "coefficient": 100 } },
        "divisor": { "linear": { "input": "Target/Defense", "addend": 100 } }
      }
    }
  ],
  "statuses": [
    {
      "status": "RPGStarter/Debuffs/Poison",
      "condition": { "hasTag": "Source/MainHand/Venomous" },
      "chance": 0.3
    }
  ]
}
```

-   **The condition:** neither of them has fallen.
-   **The first action** deals the attacker's AttackPower, reduced by the target's Defense (AttackPower x 100 / (Defense + 100)).
-   **The second action** is a critical hit: the same damage again, as often as the attacker's CritChance (8% for the Knight).
-   **The status:** a Venomous weapon poisons 30% of the time. The Goblin's Rusty Dagger has the Venomous tag, so its hits poison, and so do the Knight's once he wields the dagger.

The game code applies it and reports what it did:

```csharp
var hit = _weaponHit.Apply(attacker, target, _random);
if (!hit.Applied) return 0f; // One of them has fallen

float dealt = -hit.ChangeOf(target, RPGStats.Health);
```

The other effects work the same way:

-   **Fireball** costs 15 of the caster's Mana (`"costs": { "Source/Mana": 15 }`) and deals 1.5 x SpellPower. Without the Mana, it does nothing and `Status` is `CannotPay`, so the game logs "Mage doesn't have the Mana for a fireball."
-   **Healing Potion** adds 40 Health, never above MaxHealth.
-   **Poison Tick** deals 3 damage (the poison applies it every second).
-   **Antidote** removes the drinker's Debuffs (`"removeStatuses": ["Debuff"]`).
-   **Level Up** adds 1 to Level.

`RPGGame` takes a `System.Random` for the critical hits and the venom, so a seeded game (or a test) is repeatable.

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

## The Poison Is a Status Effect

```json
{
  "status": "Poison",
  "categories": ["Debuff"],
  "condition": { "compare": ["Target/Health", ">", 0] },
  "duration": 5,
  "stacking": "Stack",
  "maxStacks": 3,
  "statBlock": "RPGStarter/Debuffs/Poison",
  "tick": { "every": 1, "effect": "RPGStarter/Debuffs/PoisonTick" }
}
```

-   **While it lasts:** for 5 seconds, the Poison StatBlock applies to the poisoned character (the Poisoned tag and -20% MoveSpeed), and the Poison Tick effect deals 3 damage every second.
-   **Stacking:** poisoning again adds a stack, up to 3. Each stack slows and damages as much again, and restarts the 5 seconds.
-   **The condition:** it ends as soon as its character falls.
-   **The category:** Debuff, which the Antidote removes.

The game code only advances time, and applies the poison when the button asks for it:

```csharp
// RPGGame.Tick, every frame:
foreach (var entity in _spawned.ToArray()) entity.TickStatusEffects(deltaTime, _random);

// RPGGame.Poison, when the button is pressed:
var poison = _poison.Apply(null, target, _random);
```

The demo panel lists each character's status effects ("Poison x2 (3.5 left)"), and the log reports when one wears off or is cured: `RPGGame` watches each character's `StatusEffects` and the `Ended` of each status.

## Make It Yours

-   **A new class:** a `Rogue` template built on Character, with more Dexterity; then a hero built on Rogue and Inventory Holder.
-   **A new item:** a profile built on Weapon or Armor, with its numbers.
-   **A new monster:** its templates, its numbers, and a StatBlock for how it fights (like the Goblin's frenzy).
-   **A new rule for everyone:** add a modifier to the Character Rules, e.g. Defense from Vitality.
-   **A new spell:** an effect file, with its costs and its formula (e.g. a Heal that restores 2 x SpellPower for 10 Mana), and a line in `RPGGame` that applies it.
-   **A new status:** a Regeneration status that Adds 2 Health every second for 10 seconds, and a potion whose effect applies it (`"statuses": ["RPGStarter/Buffs/Regeneration"]`).
