using System.Collections;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Core.Data
{
    /// <summary>
    /// Place this attribute on any string field to turn it into a dropdown
    /// that lists all constants found in the AttributeNames class.
    /// Usage: [AttributeName] public string TargetAttribute;
    /// </summary>
    public class AttributeNameAttribute : PropertyAttribute
    {
        // No logic needed here, just a marker.
    }
}