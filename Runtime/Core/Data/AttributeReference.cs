using System;
using System.Collections.Generic;
using UnityEngine;
using SemanticKeys;

namespace ReactiveSolutions.AttributeSystem.Core.Data
{
    /// <summary>
    /// Represents a complete reference to an attribute, including its context path.
    /// Replaces the loose coupling of 'AttributeName' and 'ProviderPath'.
    /// </summary>
    [Serializable]
    public struct AttributeReference
    {
        [Tooltip("The name of the attribute (e.g. Strength, Damage).")]
        public SemanticKey Name;

        [Tooltip("The path to the provider (e.g. Owner -> EquippedWeapon). Empty means 'Local'.")]
        public List<SemanticKey> Path;

        public AttributeReference(SemanticKey name, List<SemanticKey> path = null)
        {
            Name = name;
            Path = path ?? new List<SemanticKey>();
        }
    }
}