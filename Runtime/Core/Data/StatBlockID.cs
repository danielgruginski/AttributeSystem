using UnityEngine;
using System;

namespace ReactiveSolutions.AttributeSystem.Core.Data
{

    /// <summary>
    /// A wrapper for a string that represents a StatBlock JSON filename.
    /// Using a struct allows us to create a custom PropertyDrawer for the *element*
    /// rather than trying to manage the entire List in the Editor.
    /// </summary>
    [Serializable]
    public struct StatBlockID
    {
        public string ID;

        // implicit conversion lets you use StatBlockID as a string in your code seamlessly
        public static implicit operator string(StatBlockID statBlock) => statBlock.ID;
        public static implicit operator StatBlockID(string id) => new StatBlockID { ID = id };
        public override string ToString() => ID;
    }
}