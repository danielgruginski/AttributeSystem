# Getting Started with Reactive Attribute System

Welcome! This guide will walk you through setting up a basic character, creating an equipment item (a Sword) using the visual editor, and displaying stats on the screen.

**Before you start:** attribute names and aliases are [Semantic Keys](Semantic%20Keys.md): you pick them from dropdowns in the Inspector and reference them in code through generated classes. Create these KeyDomains (**Create > SemanticKeys > Key Domain**):

-   `Stats` with the keys `Health`, `MaxHealth`, `Strength` and `Damage`. Select it and click **Generate Static Class**: the script below uses `Stats.Health` and `Stats.Strength`.
    
-   `Links` with the key `Owner`.
    

## 1. Setup Your Character

First, let's create a Game Object that can hold stats.

1.  Choose **GameObject > Attribute System > Entity**, and name the new GameObject **"Player"**. It is an empty GameObject with an `EntityController` component.
    
2.  Leave the `EntityController`'s **Profile Id** and **Profile** empty for this guide: the script below sets the stats.
    
3.  Create a new C# script named `PlayerSetup.cs` and attach it to the Player.
    

**PlayerSetup.cs**

```csharp
using UnityEngine;
using UniRx;
using Game.Constants; // Namespace of your generated key classes (Stats)
using ReactiveSolutions.AttributeSystem.Unity;

public class PlayerSetup : MonoBehaviour
{
    void Start()
    {
        var controller = GetComponent<EntityController>();

        // Initialize Base Stats
        controller.Instance.SetOrUpdateBaseValue(Stats.Health, 100f);
        controller.Instance.SetOrUpdateBaseValue(Stats.Strength, 10f);
        
        // (Optional) Log changes for debugging
        controller.Instance.ObserveValue(Stats.Health)
            .Subscribe(val => Debug.Log($"Current Health: {val}"))
            .AddTo(this);
    }
}

```

## 2. Create an Item (StatBlock Editor)

We will use the visual editor to create our Sword's stats.

1.  Open the editor: Go to **Tools > Attribute System > Stat Block Editor**.
    
2.  The window opens with an unsaved new block (click **New** to start over). In the **ID** field, type **"Weapons/IronSword"** (the file's path, without `.json`), and set **Block Name** to "Iron Sword" (block names and each modifier's optional **Source Id** show up in logs and in the Attribute Debugger).
    
3.  **Add Base Damage:**
    
    -   Under **Modifier Pipeline**, click **+ Add Modifier**.
        
    -   **Target Attribute:** Select **"Damage"**.
        
    -   **Type:** Leave **Additive**.
        
    -   **Logic:** Leave **"Value"** (the default), and set its **Value** to **5**.
        
4.  **Add Strength Scaling:**
    
    -   Click **+ Add Modifier** again to add a second modifier.
        
    -   **Target Attribute:** Select **"Damage"**.
        
    -   **Logic:** Select **"Linear"**. Its fields are **Input**, **Coefficient** and **Addend**.
        
    -   **Input:** Change Mode to **Attribute**.
        
        -   **Name:** In the dropdown next to the mode, select **"Strength"**.
            
        -   **Context Path:** Click the foldout arrow next to the name and add **"Owner"**.
            
    -   **Coefficient:** Set to **0.5** (50% scaling).
        
    -   **Addend:** Set to **0**.
        
5.  Click **Save**. This creates `Assets/Resources/Data/StatBlocks/Weapons/IronSword.json`, which describes the block the way you would build it in code, with a table of your keys' GUIDs at the end (see [JSON Format](JSON%20Format.md)):
    
    ```json
    {
      "statBlock": "Iron Sword",
      "modifiers": [
        { "target": "Damage", "value": 5 },
        { "target": "Damage", "linear": { "input": "Owner/Strength", "coefficient": 0.5 } }
      ],
      "keys": {
        "Damage": "3a5c7e9b-1d2f-4a6c-8e0b-2c4d6f8a0b1e",
        "Owner": "e2d4f6a8-0c1b-4e3d-a5f7-9b1d3c5e7f90",
        "Strength": "5c7e9a1b-3d4f-4b6a-8c0e-1f3a5c7e9b2d"
      }
    }
    ```
    

## 3. Equip the Item

Now, let's put the sword in the game.

1.  Create a Cube (or a sword model) in the scene named **"Sword"**.
    
2.  Add the `EntityController` component (so the sword has its own stats).
    
3.  Add the `StatBlockLinker` component.
    
    -   **Stat Block Ids:** Add an element and select "Weapons/IronSword" from its dropdown.
        
    -   **Controller:** Leave empty to use the Sword's own `EntityController`.
        
4.  Add the `AttributeContextLinker` component.
    
    -   **Receiver:** Leave empty to use the Sword's own `EntityController`.
        
    -   **Provider:** Drag your **Player** GameObject here.
        
    -   **Alias:** Select **"Owner"**.
        
    -   **Link On Awake:** Checked (the default).
        

**What just happened?**

-   The **StatBlockLinker** loaded your JSON and applied the stats to the Sword (in `Start`).
    
-   The **ContextLinker** told the Sword that "Owner" is the Player (in `Awake`).
    
-   The system calculated Damage: `5 (the Value modifier) + 10 (Player Strength) * 0.5 = 10`. The order in which the scripts run doesn't matter: until the Player's Strength exists it reads as 0, and Damage updates as soon as it is set.
    

## 4. Display Stats (UI)

Finally, let's see the result.

1.  Create a **UI > Text - TextMeshPro** element in your Canvas.
    
2.  Add the `AttributeDisplayText` component to it.
    
3.  **Initial Controller:** Drag the **Sword** GameObject here.
    
4.  **Attribute Name:** Select **"Damage"**.
    
5.  **Format:** "Damage: {0:0}"
    

**Hit Play!** The text should read **"Damage: 10"**. If you change the Player's Strength to 20 in `PlayerSetup.cs`, the text will automatically update to **"Damage: 15"**.

## Next Steps

-   **Profiles:** Instead of setting stats in a script, save them as a JSON profile with **Tools > Attribute System > Entity Profile Editor** and pick it in the `EntityController`'s **Profile Id**. Stats and formulas that many entities share go in a template (e.g. `Templates/Character`) that their profiles build on (see [EntityProfile](EntityProfile.md#templates)).
    
-   **Health Bars:** Make Health a pool up to MaxHealth (see [Resource Pools](Resource%20Pools.md)): damage and healing then stay between 0 and MaxHealth. Use `AttributeProgressBar` to display "Health" / "MaxHealth".
    
-   **Poisons and Buffs:** A status effect lasts on an entity: a StatBlock while it lasts, an effect every tick, and stacking rules (see [Status Effects](Status%20Effects.md)). An `EntityController` advances them every frame.
    
-   **Hits, Spells and Potions:** Write what an attack or a spell does as an effect, with **Tools > Attribute System > Effect Editor**: its cost, its formula over the attacker's and the target's stats, and a chance to crit. Then apply it with `effect.Apply(attacker, target)` (see [Effects](Effects.md)).
    
-   **A Whole Game:** Import the RPG Starter sample (**Window > Package Manager > Attribute System > Samples**) to see templates, pools, effects, status effects, gear, inventory weight and a party working together (see [RPG Starter](RPG%20Starter.md)).
    
-   **Your Own Logic:** Write a small `[Serializable]` class to compute a modifier's value; it shows up in the **Logic** dropdown (see [Modifier Logic](Modifier%20Logic.md)).
    
-   **Custom Logic:** Inherit from `AttributeUIBehaviour` to make damage numbers pop up.
    
-   **Inventory:** Write a script that instantiates Sword prefabs and calls `contextLinker.SetProvider(player)` on their `AttributeContextLinker` dynamically (see [AttributeContextLinker](AttributeContextLinker.md)).
