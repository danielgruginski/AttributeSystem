# Getting Started with Reactive Attribute System

Welcome! This guide will walk you through setting up a basic character, creating an equipment item (a Sword) using the visual editor, and displaying stats on the screen.

## 1. Setup Your Character

First, let's create a Game Object that can hold stats.

1.  Create a new Empty GameObject in your scene and name it **"Player"**.
    
2.  Add the `AttributeController` component to it.
    
3.  Create a new C# script named `PlayerSetup.cs` and attach it to the Player.
    

**PlayerSetup.cs**

```
using UnityEngine;
using ReactiveSolutions.AttributeSystem.Unity;
using SemanticKeys;

public class PlayerSetup : MonoBehaviour
{
    void Start()
    {
        var controller = GetComponent<AttributeController>();

        // Initialize Base Stats
        controller.AddAttribute(new SemanticKey("Health"), 100f);
        controller.AddAttribute(new SemanticKey("Strength"), 10f);
        
        // (Optional) Log changes for debugging
        controller.GetAttributeObservable(new SemanticKey("Health"))
            .SelectMany(attr => attr.ReactivePropertyAccess)
            .Subscribe(val => Debug.Log($"Current Health: {val}"));
    }
}

```

## 2. Create an Item (StatBlock Editor)

We will use the visual editor to create our Sword's stats.

1.  Open the editor: Go to **Window > Attribute System > Stat Block Editor**.
    
2.  In the "Load / Create" section, type **"Weapons/IronSword"** and click **Create New**.
    
3.  Set the **Block Name** to "Iron Sword".
    
4.  **Add Base Damage:**
    
    -   Find the **Modifiers** list and click **+**.
        
    -   **Target Attribute:** "Damage"
        
    -   **Source ID:** "BaseDamage"
        
    -   **Logic Type:** Select **"Static"**.
        
    -   **Value:** Set to **5**.
        
5.  **Add Strength Scaling:**
    
    -   Click **+** again to add a second modifier.
        
    -   **Target Attribute:** "Damage"
        
    -   **Source ID:** "StrScaling"
        
    -   **Logic Type:** Select **"Linear"**.
        
    -   **Input:** Change Mode to **Attribute**.
        
        -   **Name:** "Strength"
            
        -   **Path:** Add "Owner".
            
    -   **Coefficient:** Set to **0.5** (50% scaling).
        
    -   **Addend:** Set to **0**.
        
6.  Click **Save JSON**. This creates `Resources/Data/StatBlocks/Weapons/IronSword.json`.
    

## 3. Equip the Item

Now, let's put the sword in the game.

1.  Create a Cube (or a sword model) in the scene named **"Sword"**.
    
2.  Add the `AttributeController` component (so the sword has its own stats).
    
3.  Add the `StatBlockLinker` component.
    
    -   **Stat Block:** Select "Weapons/IronSword" from the dropdown.
        
    -   **Apply On Awake:** Checked.
        
4.  Add the `AttributeContextLinker` component.
    
    -   **Size:** 1
        
    -   **Key:** "Owner"
        
    -   **Target:** Drag your **Player** GameObject here.
        

**What just happened?**

-   The **StatBlockLinker** loaded your JSON and applied the stats to the Sword.
    
-   The **ContextLinker** told the Sword that "Owner" is the Player.
    
-   The system calculated Damage: `5 (Base) + (10 (Player Strength) * 0.5) = 10`.
    

## 4. Display Stats (UI)

Finally, let's see the result.

1.  Create a **UI Text (MeshPro)** element in your Canvas.
    
2.  Add the `AttributeDisplayText` component to it.
    
3.  **Source Controller:** Drag the **Sword** GameObject here.
    
4.  **Attribute Name:** "Damage"
    
5.  **Format:** "Damage: {0:0}"
    

**Hit Play!** The text should read **"Damage: 10"**. If you change the Player's Strength to 20 in `PlayerSetup.cs`, the text will automatically update to **"Damage: 15"**.

## Next Steps

-   **Health Bars:** Use `AttributeProgressBar` to display "Health" / "MaxHealth".
    
-   **Custom Logic:** Inherit from `AttributeUIBehaviour` to make damage numbers pop up.
    
-   **Inventory:** Write a script that instantiates Sword prefabs and calls `linker.SetTarget(player)` dynamically.
