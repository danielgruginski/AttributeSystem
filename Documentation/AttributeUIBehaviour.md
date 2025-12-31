# AttributeUIBehaviour Documentation

## Overview

The `AttributeUIBehaviour` is an abstract base class designed to simplify connecting Unity UI elements (Text, Sliders, Images) to the Reactive Attribute System.

Instead of writing boilerplate code to find an `AttributeController`, get an attribute, subscribe to it, and handle disposal, this component handles all the plumbing for you. You simply inherit from it and implement a single method: `OnValueChanged(float newValue)`.

## Key Features

-   **Automatic Subscription:** Handles finding the target `AttributeController` and subscribing to the attribute's reactive stream.
    
-   **Safe Cleanup:** Automatically disposes of subscriptions when the UI object is destroyed, preventing memory leaks.
    
-   **Lazy Binding:** Waits for the attribute to be created if it doesn't exist yet (e.g., waiting for initialization code to run).
    
-   **Editor Integration:** Provides Inspector fields to select the target attribute by name.
    

## Class Definition

```
public abstract class AttributeUIBehaviour : MonoBehaviour
{
    // ...
}

```

## Inspector Properties

-   **`Source Controller`**
    
    -   The `AttributeController` to read from.
        
    -   _Default:_ If empty, it searches for one on the same GameObject or its parents.
        
-   **`Attribute Name`**
    
    -   The Semantic Key of the attribute to display (e.g., "Health", "Mana", "Gold").
        
-   **`Path` (Optional)**
    
    -   A list of keys to traverse (e.g., `["Owner"]`). This allows a UI element on a Sword to display the _Player's_ Strength.
        

## Included Implementations

The package comes with two common implementations ready to use:

### 1. `AttributeProgressBar`

Updates a Unity UI `Slider` or `Image` (Fill Amount) based on the ratio of two attributes (Current / Max).

-   **Inspector:**
    
    -   `Max Attribute Name`: The attribute defining the 100% value (e.g., "MaxHealth").
        
-   **Logic:** Calculates `Current / Max` and applies it to the Slider's value.
    

### 2. `AttributeDisplayText`

Updates a TextMeshPro or standard UI Text component with the raw value.

-   **Inspector:**
    
    -   `Format`: A standard C# format string (e.g., `"{0:0}"` for whole numbers, `"{0:0.0}"` for one decimal).
        
-   **Logic:** Sets `text.text = string.Format(Format, value)`.
    

## Creating Custom UI Components

You can easily create your own UI behaviors (e.g., a color-changing orb, a shaking icon) by inheriting from `AttributeUIBehaviour`.

```
using ReactiveSolutions.AttributeSystem.Unity.UI;
using UnityEngine;
using UnityEngine.UI;

public class HealthColorTint : AttributeUIBehaviour
{
    public Image TargetImage;
    public Gradient HealthGradient;

    // We override OnValueChanged to apply our custom logic
    protected override void OnValueChanged(float value)
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
