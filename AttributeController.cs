using System;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using UnityEngine;
using static Unity.VisualScripting.Member;

namespace algumacoisaqq.AttributeSystem
{
    /// <summary>
    /// The central manager for character attributes.
    /// Handles storage, retrieval, and modification of attributes.
    /// </summary>
    public class AttributeController : MonoBehaviour
    {
        // The core storage. ReactiveDictionary allows other systems to listen for adds/removes.
        private readonly ReactiveDictionary<string, Attribute> _attributes = new();
        public IReadOnlyReactiveDictionary<string, Attribute> Attributes => _attributes;

        /// <summary>
        /// A reactive stream that fires whenever a new Attribute is added.
        /// Bridges can use this to subscribe to attributes that may not exist yet.
        /// </summary>
        public IObservable<Attribute> OnAttributeAdded => _attributes.ObserveAdd().Select(evt => evt.Value);

        /// <summary>
        /// Retrieves an attribute if it exists, otherwise returns null.
        /// Use this for read-only checks where creating a new attribute is unintended.
        /// </summary>
        public Attribute GetAttribute(string name)
        {
            return _attributes.TryGetValue(name, out var attr) ? attr : null;
        }

        /// <summary>
        /// Safely gets an attribute, creating it with a default base value if it's missing.
        /// This is ideal for consumers (bridges, UI) that need to read an attribute's final value
        /// without necessarily setting its base.
        /// </summary>
        public Attribute GetOrCreateAttribute(string name, float defaultBaseIfMissing = 0f)
        {
            if (!_attributes.TryGetValue(name, out var attr))
            {
                attr = new Attribute(name, defaultBaseIfMissing, this);
                _attributes[name] = attr;
            }
            return attr;
        }

        /// <summary>
        /// Provides an observable stream for a specific attribute. If the attribute doesn't
        /// exist, the stream will wait until it's added. This is the safest way for
        /// systems to react to attribute changes.
        /// </summary>
        public IObservable<Attribute> GetAttributeObservable(string name)
        {
            return Observable.Create<Attribute>(observer =>
            {
                if (_attributes.TryGetValue(name, out var attr))
                {
                    observer.OnNext(attr);
                    observer.OnCompleted();
                }
                else
                {
                    return OnAttributeAdded
                        .Where(addedAttr => addedAttr.Name == name)
                        .Take(1)
                        .Subscribe(observer);
                }
                return Disposable.Empty;
            });
        }

        /// <summary>
        /// Explicitly sets or updates the base value of an attribute. This is the designated
        /// method for establishing a character's foundational stats.
        /// Currently GetAttribute(...) will autogenerate the Attribute if missing.
        /// </summary>
        public void SetOrUpdateBaseValue(string attributeName, float newBase)
        {
            var attr = GetOrCreateAttribute(attributeName);
            attr.SetBaseValue(newBase);
        }

        /// <summary>
        /// Adds a modifier to an attribute, creating the attribute if it doesn't exist.
        /// </summary>
        /// <param name="sourceId">The ID of the source adding the modifier (e.g., "SwordOfThePhoenix").</param>
        /// <param name="modifier">The IAttributeModifier instance (Flat, Formulaic, Clamp, etc.).</param>
        /// <param name="attributeName">The name of the attribute to modify (e.g., "AttackDamage").</param>
        public void AddModifier(string sourceId, IAttributeModifier modifier, string attributeName)
        {
            Debug.Assert(!string.IsNullOrEmpty(sourceId), "Modifier source ID cannot be null or empty.");
            Debug.Assert(modifier != null, "Modifier object cannot be null.");
            Debug.Assert(!string.IsNullOrEmpty(attributeName), "Target attribute name cannot be null or empty.");

            var attr = GetOrCreateAttribute(attributeName, 0f);

            // 1. Add to internal list
            attr.AddModifier(modifier);

            // 2. Trigger Lifecycle (Self-Managed Dependency Binding)
            modifier.OnAttach(attr, this);
        }

        /// <summary>
        /// Removes modifiers and triggers their Detach/Dispose lifecycle.
        /// </summary>
        public void RemoveModifiersBySource(string sourceId)
        {
            foreach (var attr in _attributes.Values)
            {
                // We need to get the specific modifiers to dispose them
                var removedMods = attr.RemoveModifiersBySource(sourceId);

                foreach (var mod in removedMods)
                {
                    mod.OnDetach(); // Or Dispose()
                }
            }
        }

        /// <summary>
        /// Applies a complete StatBlock (JSON Template) to this controller.
        /// Handles both modifier creation AND reactive dependency binding.
        /// </summary>
        public void ApplyStatBlock(StatBlock block, string sourceId = null)
        {
            if (block == null) return;

            // If no source ID provided, use the block's name
            string effectiveSourceId = string.IsNullOrEmpty(sourceId) ? block.BlockName : sourceId;

            foreach (var spec in block.Modifiers)
            {
                // 1. Create Modifier
                var modifier = spec.ToModifier(effectiveSourceId);

                // 2. Add (OnAttach is called inside AddModifier)
                AddModifier(effectiveSourceId, modifier, spec.AttributeName);
            }
        }


        public void AddFlatMod(string sourceId, string targetAttr, float value, AttributeMergeMode mode = AttributeMergeMode.Add, int priority = 10)
        {
            var mod = new ConstantAttributeModifier(value, mode, sourceId, priority);
            AddModifier(sourceId, mod, targetAttr);
        }

        public void AddLinearScaling(string sourceId, string targetAttr, string sourceAttr, float coefficient, AttributeMergeMode mode = AttributeMergeMode.Add, int priority = 10, float addend = 0f)
        {
            var source = new ValueSource { Type = ValueSource.SourceType.Attribute, AttributeName = sourceAttr };
            var mod = new LinearAttributeModifier(source, coefficient, addend, mode, sourceId, priority);
            AddModifier(sourceId, mod, targetAttr);
        }


        public void AddExponentialScaling(string sourceId, string targetAttr, string exponentAttr, float baseK, AttributeMergeMode mode = AttributeMergeMode.Multiply, int priority = 100)
        {
            var source = new ValueSource { Type = ValueSource.SourceType.Attribute, AttributeName = exponentAttr };
            var mod = new ExponentialAttributeModifier(source, baseK, mode, sourceId, priority);

            AddModifier(sourceId, mod, targetAttr);
        }

                public void AddClamp(string sourceId, string targetAttr, float min, float max, int priority = 1000)
        {
            var mod = new ClampAttributeModifier(min, max, sourceId, priority);
            AddModifier(sourceId, mod, targetAttr);
        }


        // --- NEW HELPERS (Fixing your previous errors) ---

        /// <summary>
        /// Calculates [Target] = [Dividend] / [Divisor].
        /// </summary>
        public void AddRatio(string sourceId, string targetAttr, string dividendAttr, string divisorAttr, AttributeMergeMode mode = AttributeMergeMode.Multiply, int priority = 100)
        {
            var mod = new RatioAttributeModifier(dividendAttr, divisorAttr, mode, sourceId, priority);
            AddModifier(sourceId, mod, targetAttr);
        }




        /// <summary>
        /// Multiplies [Target] by [MultiplierAttribute].
        /// </summary>
        // UPDATED: Re-implemented using Linear logic to deprecate ProductAttributeModifier
        public void AddProduct(string sourceId, string targetAttr, string multiplierAttr, AttributeMergeMode mode = AttributeMergeMode.Multiply, int priority = 100)
        {
            // Replaces ProductAttributeModifier with LinearAttributeModifier
            // Logic: Target = Target * (Multiplier * 1 + 0)
            AddLinearScaling(sourceId, targetAttr, multiplierAttr, 1f, mode, priority, 0f);
        }

        public void AddDiminishingReturns(string sourceId, string targetAttr, string inputAttr, float maxBonus, float softCapN, AttributeMergeMode mode = AttributeMergeMode.Add, int priority = 500)
        {
            var mod = new DiminishingReturnsAttributeModifier(inputAttr, maxBonus, softCapN, mode, sourceId, priority);
            AddModifier(sourceId, mod, targetAttr);
        }

        public void AddSegmentedMultiplier(string sourceId, string targetAttr, string inputAttr, Dictionary<float, float> breakpoints, AttributeMergeMode mode = AttributeMergeMode.Multiply, int priority = 400)
        {
            // Convert Dictionary to List<Vector2> for the Modifier storage
            // This ensures code compatibility with your existing Dictionary-based calls
            List<Vector2> points = breakpoints.Select(kvp => new Vector2(kvp.Key, kvp.Value)).OrderBy(v => v.x).ToList();

            var mod = new SegmentedMultiplierAttributeModifier(inputAttr, points, mode, sourceId, priority);
            AddModifier(sourceId, mod, targetAttr);
        }

        internal void SetupTriangularBonus(string sourceID, string attributeName)
        {
            var attributeBonusName = attributeName + "Bonus";
            SetOrUpdateBaseValue(attributeBonusName, 0);
            var triangularBonusMod = new TriangularBonusAttributeModifier(attributeName, AttributeMergeMode.Add, sourceID, 10);
            AddModifier(sourceID, triangularBonusMod, attributeBonusName);
        }
    }
}

