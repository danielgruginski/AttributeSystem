using System;
using UnityEditor;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Editor
{ 
/// <summary>
/// Place this attribute on any static class containing public const string fields
/// to expose them as attribute names in the Unity Editor dropdowns.
/// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class AttributeProviderAttribute : System.Attribute
    {

    }
}