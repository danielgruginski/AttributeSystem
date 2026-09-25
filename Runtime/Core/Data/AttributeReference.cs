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

        /// <summary>
        /// The attribute <paramref name="name"/>, local or at the end of the provider <paramref name="path"/>:
        /// <c>Of(Stats.Strength, Links.Owner)</c> is the Owner's Strength.
        /// </summary>
        public static AttributeReference Of(SemanticKey name, params SemanticKey[] path) =>
            new AttributeReference(name, new List<SemanticKey>(path ?? new SemanticKey[0]));
    }
}