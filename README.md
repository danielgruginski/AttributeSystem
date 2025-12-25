# **Attribute System for Unity**

A reactive attribute management system designed for scalability and maintainability. This system decouples character statistics from game logic, allowing for complex modifier calculations without the "spaghetti code" often found in RPG or Shooter stat systems.

## **Features**

* **Reactive Core**: Powered by **UniRx**, allowing systems to subscribe to attribute changes rather than polling in Update.  
* **Modular Modifiers**: Built-in support for various mathematical modifier types:  
  * Linear & Constant bonuses.  
  * Exponential & Ratio scaling.  
  * Diminishing Returns & Segmented Multipliers.  
  * Clamping and Triangular bonuses.  
* **Data-Driven**: Load and save character StatBlocks via JSON, facilitating easy balancing and modding support.  
* **Custom Editor Tools**: Includes a StatBlock Editor Window and custom property drawers for an improved designer workflow.  
* **Loose Coupling**: Easily integrates with existing projects (like TopDown Engine) without creating rigid dependencies.

## **Installation**

via Git URL

Ensure UniRx is installed in your project. (asset store: https://assetstore.unity.com/packages/tools/integration/unirx-reactive-extensions-for-unity-17276)

Open the Unity Package Manager (Window > Package Manager).

Click the + icon and select Add package from git URL....

Paste: https://github.com/danielgruginski/AttributeSystem.git

## **Quick Start**

1. Define your Attributes

In your main project, tag your static constants class:

using DanielGruginski.AttributeSystem;

[AttributeProvider]
public static class MyAttributes 
{
    public const string Health = "Health";
    public const string MovementSpeed = "MovementSpeed";
}


2. Observe Changes

attributeController.GetAttribute(MyAttributes.Health)
    .CurrentValue
    .Subscribe(val => UpdateUI(val));





1. **Define a Stat**: Create a new Attribute within an AttributeController.  
2. **Add Modifiers**: Use the ModifierFactoryRegistry to apply modifiers (e.g., a "Speed Potion" adding a 20% Ratio modifier).  
3. **Observe**: Subscribe to changes in your UI or logic:  
   attributeController.GetAttribute("Health")  
       .CurrentValue  
       .Subscribe(val \=\> UpdateHealthBar(val));

## **License**

MIT