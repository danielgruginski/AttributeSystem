# AttributeUIBehaviour Documentation

## Overview

The `AttributeUIBehaviour` is an abstract base class designed to simplify connecting Unity UI elements (Text, Sliders, Images) to the Reactive Attribute System.

Instead of writing boilerplate code to track an `EntityController`, get an attribute, subscribe to it, and handle disposal, this component handles all the plumbing for you. You simply inherit from it and call `MonitorAttribute(attributeName, callback)` in `Awake` for each attribute you want to react to.

## Key Features

-   **Automatic Subscription:** Handles subscribing to the attribute's reactive stream on the target `EntityController`.
    
-   **Safe Cleanup:** Automatically disposes of subscriptions when the UI object is destroyed, preventing memory leaks.
    
-   **Lazy Binding:** Waits for the attribute to be created if it doesn't exist yet (e.g., waiting for initialization code to run), and for a controller to be assigned.
    
-   **Retargeting:** The controller can be changed at any time with `SetController` (e.g., a health bar moving to a new enemy). Monitors follow the new controller and drop the previous one, so values from the old target never arrive after the switch.
    
-   **Editor Integration:** Implementations declare `SemanticKey` fields, so the target attribute is selected from a dropdown in the Inspector.
    

## Class Definition

```csharp
public abstract class AttributeUIBehaviour : MonoBehaviour
{
    // ...
}

```

Namespace: `ReactiveSolutions.AttributeSystem.Unity.UI` (requires TextMeshPro and uGUI).

-   **`void SetController(EntityController controller)`**
    
    -   Sets the controller to read from, replacing the previous one. Passing `null` stops the updates (the last value stays displayed).
        
-   **`protected void MonitorAttribute(SemanticKey attributeName, Action<float> onValueChanged)`**
    
    -   Calls `onValueChanged` with the attribute's final value on the current controller: as soon as the attribute exists, and then whenever it changes. Call it once per attribute, in `Awake` (after `base.Awake()`) or `Start`.
        
-   **`protected virtual void Awake()`** / **`protected virtual void OnDestroy()`**
    
    -   Apply the Initial Controller / dispose the subscriptions. Call the base implementation when you override them.
        

## Inspector Properties

-   **`Initial Controller`**
    
    -   The `EntityController` to read from, applied in `Awake`.
        
    -   _Default:_ If empty, nothing is displayed until you call `SetController(...)`. The component does not search the GameObject or its parents.
        
-   **`Attribute Name`**
    
    -   The Semantic Key of the attribute to display (e.g., `Health`, `Mana`, `Gold`). Declared by `AttributeDisplayText`; other implementations declare their own key fields (see below).
        

There is no path field. To display another entity's attribute (e.g., the _Player's_ Strength on a Sword's UI), assign that entity's controller.

## Included Implementations

The package comes with two common implementations ready to use:

### 1. `AttributeProgressBar`

Updates a Unity UI `Slider` (required on the same GameObject) based on two attributes (Current / Max).

-   **Inspector:**
    
    -   `Current Attribute Name`: The attribute defining the current value (e.g., `Health`).
        
    -   `Max Attribute Name`: The attribute defining the 100% value (e.g., `MaxHealth`).
        
    -   `Sync Slider Max`: On by default (see Logic).
        
-   **Logic:** With `Sync Slider Max`, sets the Slider's range to `0..Max` and its value to `Current`. Otherwise, sets the Slider's value to `Current / Max` clamped to 0-1 (0 when Max is 0 or less), for a Slider with a 0-1 range.
    

### 2. `AttributeDisplayText`

Updates a TextMeshPro text component (`TMP_Text`, required on the same GameObject) with the raw value.

-   **Inspector:**
    
    -   `Attribute Name`: The attribute to display.
        
    -   `Prefix` / `Postfix`: Text placed before / after the formatted value.
        
    -   `Format`: A standard C# format string (e.g., `"{0:0}"` for whole numbers, the default, or `"{0:0.0}"` for one decimal).
        
-   **Logic:** Sets `text.text = Prefix + string.Format(Format, value) + Postfix`.
    

`AttributeTagActiveUI` (**Attribute System > UI > Tag Active Toggle**), which enables or disables a GameObject while a tag is present, is a separate component and does not derive from `AttributeUIBehaviour`.

## Creating Custom UI Components

You can easily create your own UI behaviors (e.g., a color-changing orb, a shaking icon) by inheriting from `AttributeUIBehaviour`. `Stats.Health` below is a key from a class generated from a `Stats` KeyDomain (see [Semantic Keys](Semantic%20Keys.md)); to choose the attribute in the Inspector instead, declare a `[SerializeField] private SemanticKey` field, as `AttributeDisplayText` does.

```csharp
using Game.Constants; // Namespace of your generated key classes (Stats)
using ReactiveSolutions.AttributeSystem.Unity.UI;
using UnityEngine;
using UnityEngine.UI;

public class HealthColorTint : AttributeUIBehaviour
{
    public Image TargetImage;
    public Gradient HealthGradient;

    protected override void Awake()
    {
        base.Awake(); // Applies the Initial Controller

        // Register our custom logic; subscription, retargeting and cleanup are handled by the base class
        MonitorAttribute(Stats.Health, OnHealthChanged);
    }

    private void OnHealthChanged(float value)
    {
        // Example: Assuming 'value' is normalized (0-1), or you'd need to read MaxHealth too.
        // For simplicity, let's say 'value' is percentage 0-100 here.
        float normalized = Mathf.Clamp01(value / 100f);
        
        if (TargetImage != null)
        {
            TargetImage.color = HealthGradient.Evaluate(normalized);
        }
    }
}

```
