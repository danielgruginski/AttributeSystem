# Getting Started with Reactive Attribute System

Welcome! This guide will walk you through setting up a basic character, creating an equipment item (a Sword) using the visual editor, and displaying stats on the screen.

**Before you start:** attribute names, aliases and modifier logic types are [Semantic Keys](Semantic%20Keys.md): you pick them from dropdowns in the Inspector and reference them in code through generated classes. Create these KeyDomains (**Create > SemanticKeys > Key Domain**):

-   `Stats` with the keys `Health`, `MaxHealth`, `Strength` and `Damage`. Select it and click **Generate Static Class**: the script below uses `Stats.Health` and `Stats.Strength`.
    
-   `Links` with the key `Owner`.
    
-   `Modifiers` with the keys `Static` and `Linear`. The package doesn't ship this domain; the modifier factory finds the built-in logic types by key name.
    

## 1. Setup Your Character

First, let's create a Game Object that can hold stats.

1.  Create a new Empty GameObject in your scene and name it **"Player"**.
    
2.  Add the `EntityController` component to it. Leave **Profile SO** empty for this guide: the script below sets the stats (the controller logs a warning that the entity starts empty).
    
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

1.  Open the editor: Go to **Window > Attribute System > Stat Block Editor (Unified)**.
    
2.  The window opens with an unsaved new block (click **New** to start over). In the **Filename (No ext)** field, type **"Weapons/IronSword"**, and set **Block Name** to "Iron Sword" (block names and each modifier's optional **Source Id** show up in logs and in the Attribute Debugger).
    
3.  **Add Base Damage:**
    
    -   Under **Modifier Pipeline**, click **+ Add Modifier**.
        
    -   **Target Attribute:** Select **"Damage"**.
        
    -   **Logic Type:** Select **"Static"**.
        
    -   **Value:** Set to **5**.
        
4.  **Add Strength Scaling:**
    
    -   Click **+ Add Modifier** again to add a second modifier.
        
    -   **Target Attribute:** Select **"Damage"**.
        
    -   **Logic Type:** Select **"Linear"**. The parameters are now labelled **Input**, **Coefficient** and **Addend**.
        
    -   **Input:** Change Mode to **Attribute**.
        
        -   **Name:** In the dropdown next to the mode, select **"Strength"**.
            
        -   **Context Path:** Click the foldout arrow next to the name and add **"Owner"**.
            
    -   **Coefficient:** Set to **0.5** (50% scaling).
        
    -   **Addend:** Set to **0**.
        
5.  Click **Save**. This creates `Assets/Resources/Data/StatBlocks/Weapons/IronSword.json`.
    

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
    
-   The system calculated Damage: `5 (Base) + (10 (Player Strength) * 0.5) = 10`. The order in which the scripts run doesn't matter: until the Player's Strength exists it reads as 0, and Damage updates as soon as it is set.
    

## 4. Display Stats (UI)

Finally, let's see the result.

1.  Create a **UI > Text - TextMeshPro** element in your Canvas.
    
2.  Add the `AttributeDisplayText` component to it.
    
3.  **Initial Controller:** Drag the **Sword** GameObject here.
    
4.  **Attribute Name:** Select **"Damage"**.
    
5.  **Format:** "Damage: {0:0}"
    

**Hit Play!** The text should read **"Damage: 10"**. If you change the Player's Strength to 20 in `PlayerSetup.cs`, the text will automatically update to **"Damage: 15"**.

## Next Steps

-   **Health Bars:** Use `AttributeProgressBar` to display "Health" / "MaxHealth".
    
-   **Custom Logic:** Inherit from `AttributeUIBehaviour` to make damage numbers pop up.
    
-   **Inventory:** Write a script that instantiates Sword prefabs and calls `contextLinker.SetProvider(player)` on their `AttributeContextLinker` dynamically (see [AttributeContextLinker](AttributeContextLinker.md)).
