using System;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Core.Data
{
    /// <summary>
    /// Place this attribute on any static class containing public const string fields
    /// to expose them as attribute names in the Unity Editor dropdowns.
    /// This must be in the Runtime assembly so game code can reference it.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class AttributeProviderAttribute : System.Attribute
    {
    }
}