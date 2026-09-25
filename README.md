# **Attribute System for Unity**

A reactive attribute management system designed for scalability and maintainability. This system decouples character statistics from game logic, allowing for complex modifier calculations without the "spaghetti code" often found in RPG or Shooter stat systems.

## **Features**

* **Reactive Core**: Powered by **UniRx**, allowing systems to subscribe to attribute changes rather than polling in Update.  
* **Modular Modifiers**: Built-in support for various mathematical modifier types:  
  * Linear & Constant bonuses.  
  * Exponential & Ratio scaling.  
  * Diminishing Returns & Segmented Multipliers.  
  * Clamping and Triangular bonuses.  
* **Entity Graph**: Entities can read and modify each other's attributes through provider paths (e.g. a sword reading `Owner.Strength`), with pointers (aliases), reference-counted tags and conditional StatBlocks.  
* **Data-Driven**: Author StatBlocks and entity profiles as JSON files (each with its own editor window) or right in the Inspector, facilitating easy balancing.  
* **Custom Editor Tools**: Includes StatBlock and Entity Profile editor windows, an Attribute Debugger and custom property drawers for an improved designer workflow.  
* **No Magic Strings**: Attribute names, tags and aliases are GUID-backed [Semantic Keys](Documentation/Semantic%20Keys.md).  
* **Loose Coupling**: Easily integrates with existing projects (like TopDown Engine) without creating rigid dependencies.

## **Installation**

Requires Unity 2021.3 or newer. Install the dependencies first, then this package, via **Window > Package Manager > + > Add package from git URL...**:

1. **UniRx**: `https://github.com/neuecc/UniRx.git?path=Assets/Plugins/UniRx/Scripts` (or from the [Asset Store](https://assetstore.unity.com/packages/tools/integration/unirx-reactive-extensions-for-unity-17276)).
2. **SemanticKeys**: `https://github.com/danielgruginski/SemanticKeys.git`
3. **Attribute System**: `https://github.com/danielgruginski/AttributeSystem.git`

The optional UI components (`AttributeDisplayText`, `AttributeProgressBar`) also need TextMeshPro and uGUI.

## **Quick Start**

### 1. Define your keys

Attribute names are [Semantic Keys](Documentation/Semantic%20Keys.md), not strings. Create a KeyDomain (**Create > SemanticKeys > Key Domain**) named `Stats` with the keys `Strength`, `Damage` and `Speed`, then click **Generate Static Class**. You can now write `Stats.Strength` in code and pick the same keys from dropdowns in the Inspector.

### 2. Create an entity and add modifiers

```csharp
using System;
using System.Collections.Generic;
using Game.Constants;                              // namespace of your generated key classes
using ReactiveSolutions.AttributeSystem.Core;
using ReactiveSolutions.AttributeSystem.Core.Builders;
using ReactiveSolutions.AttributeSystem.Core.Data;  // StatBlock, AttributeReference
using UniRx;
using UnityEngine;

var player = new Entity();
player.SetOrUpdateBaseValue(Stats.Strength, 10f);
player.SetOrUpdateBaseValue(Stats.Damage, 5f);

// Observe instead of polling.
player.ObserveValue(Stats.Damage).Subscribe(value => Debug.Log($"Damage: {value}"));

// Damage += Strength * 0.5  (built-in "Linear" logic: Input * Coefficient + Addend)
var factory = new ModifierFactory();
IAttributeModifier strengthScaling = factory.Create(new AttributeModifierSpec
{
    Type = ModifierType.Additive,
    LogicType = sk.Modifiers.Linear,
    Arguments = new List<ValueSource>
    {
        new ValueSource { Mode = ValueSource.SourceMode.Attribute, AttributeRef = new AttributeReference(Stats.Strength) },
        ValueSource.Const(0.5f),
        ValueSource.Const(0f)
    }
});
IDisposable scaling = player.AddModifier("StrengthScaling", strengthScaling, Stats.Damage); // Damage: 10

player.SetOrUpdateBaseValue(Stats.Strength, 20f);  // Damage: 15

// Group modifiers and tags into StatBlocks.
StatBlock rage = StatBlockBuilder.Create("Rage")
    .AddFlatModifier(Stats.Damage, 10f)            // +10
    .AddMultiplierModifier(Stats.Speed, 0.25f)     // +25%
    .Build();
ActiveStatBlock activeRage = rage.ApplyToEntity(player, factory); // Damage: 25

activeRage.Dispose();                              // Damage: 15
scaling.Dispose();                                 // Damage: 5
```

### 3. Use it in a scene

Add an `EntityController` to a GameObject and use `controller.Instance` to reach its `Entity`; give it a profile (a JSON file or one authored in its Inspector) to set its starting stats. Load JSON StatBlocks with `StatBlockLinker`, connect entities (e.g. a sword to its owner) with `AttributeContextLinker`, and bind UI with `AttributeDisplayText` / `AttributeProgressBar`. See [Getting Started](Documentation/Getting%20Started.md) for a step-by-step walkthrough.

## **Documentation**

* [Getting Started](Documentation/Getting%20Started.md) and [Semantic Keys](Documentation/Semantic%20Keys.md)
* Core: [Entity](Documentation/Entity.md), [Attribute](Documentation/Attribute.md), [Attribute Modifiers](Documentation/Attribute%20Modifiers.md), [ModifierFactory](Documentation/ModifierFactory.md), [ValueSource](Documentation/ValueSource.md), [AttributeReference](Documentation/AttributeReference.md), [Attribute Pointers](Documentation/Attribute%20Pointers.md), [AttributeConnection](Documentation/AttributeConnection.md)
* Data: [StatBlock](Documentation/StatBlock.md), [ActiveStatBlock](Documentation/ActiveStatBlock.md), [EntityProfile](Documentation/EntityProfile.md), [LinkGroup](Documentation/LinkGroup.md), [Fluent Builders](Documentation/Fluent%20Builders.md)
* Unity: [EntityController](Documentation/EntityController.md), [StatBlockLinker](Documentation/StatBlockLinker.md), [AttributeContextLinker](Documentation/AttributeContextLinker.md), [AttributeUIBehaviour](Documentation/AttributeUIBehaviour.md)

## **License**

MIT
