# RPG Starter

A small RPG built with the Attribute System. The rules and numbers are JSON data; the code only spawns characters, applies effects, moves items around and counts time.

-   **Templates:** Character (stats, a Health pool, the stat formulas), Caster (a Mana pool, SpellPower), Inventory Holder (an Inventory, carried weight, encumbrance), Weapon and Armor (add their stats to their owner).
-   **Characters:** a Knight and a Mage in a party, and a Goblin that goes into a frenzy below half Health.
-   **Items and StatBlocks:** a sword, a dagger, a staff, chain mail, an anvil; the party's Leadership aura, a Blessing, a Poison.
-   **Effects:** a weapon hit (with critical hits), a Fireball that costs Mana, a Healing Potion, the Poison's ticks and leveling up.

## Run It

1.  Import the sample: **Window > Package Manager > Attribute System > Samples > RPG Starter > Import**.
2.  In a new scene, add the **RPG Starter Demo** component to an empty GameObject.
3.  Press Play, and use the buttons: attack, cast, drink a potion, swap weapons, pick up the anvil, poison the Knight, level up, bless the party.

## What's Where

| Path | Contents |
| ----- | ----- |
| `Keys/` | The sample's KeyDomains: RPGStats, RPGTags, RPGLinks and RPGGroups. |
| `Resources/Data/EntityProfiles/RPGStarter/` | Profiles: `Templates/`, `Heroes/`, `Monsters/`, `Items/`. |
| `Resources/Data/StatBlocks/RPGStarter/` | StatBlocks: `Auras/`, `Buffs/`, `Debuffs/`, `Passives/`. |
| `Resources/Data/Effects/RPGStarter/` | Effects: `Combat/`, `Spells/`, `Consumables/`, `Debuffs/`, `Progression/`. |
| `Scripts/RPGKeys.cs` | The keys as C#, as **Generate Static Class** writes them. |
| `Scripts/RPGGame.cs` | The game code: spawning, applying effects, equipment, inventory and the poison's timer. |
| `Scripts/RPGStarterDemo.cs` | The on-screen panel. |

## Good to Know

-   The Stat Block Editor, the Entity Profile Editor, the Effect Editor and the ID dropdowns show the files under `Assets/Resources/Data`. To edit the sample's files with them, move the sample's `RPGStarter` folders from `Resources/Data/EntityProfiles`, `Resources/Data/StatBlocks` and `Resources/Data/Effects` there (the IDs stay the same), or edit them as text.
-   The poison's duration, and the second between its ticks, are counted by hand in `RPGGame.Tick`.

The package's documentation walks through the sample: see `Documentation/RPG Starter.md`.
