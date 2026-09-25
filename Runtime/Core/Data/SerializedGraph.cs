using ReactiveSolutions.AttributeSystem.Core.Data.Json;
using ReactiveSolutions.AttributeSystem.Core.Modifiers;
using SemanticKeys;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Core.Data
{
    /// <summary>
    /// Walks data the way Unity saves it (the fields that JSON files hold too): copies it, and gives each place that
    /// holds a [SerializeReference] object (such as a logic) its own.
    /// </summary>
    internal static class SerializedGraph
    {
        private const int MaxDepth = 64;

        private static readonly MethodInfo MemberwiseCloneMethod =
            typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly Dictionary<Type, (FieldInfo info, bool isReference)[]> FieldCache =
            new Dictionary<Type, (FieldInfo, bool)[]>();
        private static readonly object Gate = new object();

        [ThreadStatic] private static int _copyDepth;

        /// <summary>
        /// A copy of <paramref name="value"/> that shares no saved data with it: its lists, ValueSources, logic objects
        /// and serializable classes are copied too, all the way down. Unity objects, and fields Unity doesn't save,
        /// are shared.
        /// </summary>
        public static object Copy(object value)
        {
            if (value == null || IsImmutable(value.GetType()) || value is UnityEngine.Object) return value;

            if (++_copyDepth > MaxDepth)
            {
                _copyDepth--;
                throw new InvalidOperationException("The data is nested too deeply to copy (does an object contain itself?)");
            }

            try
            {
                // Logic may override Clone (e.g. to copy objects of its own); the default copies with CopyFields.
                if (value is ModifierLogic logic) return logic.Clone();
                if (value is AnimationCurve curve) return CopyCurve(curve);
                if (value is IList list) return CopyList(list);
                if (!JsonTypes.IsRecord(value.GetType())) return value;

                object copy = MemberwiseCloneMethod.Invoke(value, null);
                CopyFields(copy);
                return copy;
            }
            finally
            {
                _copyDepth--;
            }
        }

        /// <summary>
        /// Replaces the saved fields of <paramref name="target"/> (a shallow copy) with copies, so that it shares no
        /// saved data with the object it was copied from.
        /// </summary>
        public static void CopyFields(object target)
        {
            foreach (var (info, _) in SavedFields(target.GetType()))
            {
                object value = info.GetValue(target);
                if (value == null || IsImmutable(value.GetType())) continue;
                info.SetValue(target, Copy(value));
            }
        }

        /// <summary>
        /// Gives each [SerializeReference] field in <paramref name="root"/> its own object. A list entry duplicated in
        /// the Inspector points to the same objects (e.g. its logic, and the formulas in it) as the entry it was copied
        /// from, so editing one would change both; this replaces the second and later references with copies.
        /// </summary>
        public static void Unshare(object root)
        {
            if (root == null) return;
            Visit(root, isReference: false, new HashSet<object>(ReferenceComparer.Instance), 0);
        }

        // Returns what to store where the value was: the value itself, or a copy of a reference seen before.
        private static object Visit(object value, bool isReference, HashSet<object> seen, int depth)
        {
            if (value == null || depth > MaxDepth) return value;

            var type = value.GetType();
            if (IsImmutable(type) || value is UnityEngine.Object || value is AnimationCurve) return value;

            if (value is IList list)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    object item = list[i];
                    object result = Visit(item, isReference, seen, depth + 1);
                    // A struct item was boxed as a copy: write it back.
                    if (!ReferenceEquals(item, result) || (item != null && item.GetType().IsValueType)) list[i] = result;
                }
                return value;
            }

            // A copy shares nothing with the original, so there is nothing below it to check.
            if (isReference && !type.IsValueType && !seen.Add(value)) return Copy(value);

            if (value is ModifierLogic || JsonTypes.IsRecord(type))
            {
                foreach (var (info, fieldIsReference) in SavedFields(type))
                {
                    object fieldValue = info.GetValue(value);
                    if (fieldValue == null || IsImmutable(fieldValue.GetType())) continue;

                    object result = Visit(fieldValue, fieldIsReference, seen, depth + 1);
                    if (!ReferenceEquals(fieldValue, result) || info.FieldType.IsValueType) info.SetValue(value, result);
                }
            }
            return value;
        }

        private static IList CopyList(IList list)
        {
            var type = list.GetType();
            if (type.IsArray)
            {
                var array = Array.CreateInstance(type.GetElementType(), list.Count);
                for (int i = 0; i < list.Count; i++) array.SetValue(Copy(list[i]), i);
                return array;
            }

            var copy = (IList)Activator.CreateInstance(type);
            foreach (var item in list) copy.Add(Copy(item));
            return copy;
        }

        private static AnimationCurve CopyCurve(AnimationCurve curve) => new AnimationCurve(curve.keys)
        {
            preWrapMode = curve.preWrapMode,
            postWrapMode = curve.postWrapMode
        };

        /// <summary>Values with nothing to copy or unshare: numbers, text, enums and keys.</summary>
        private static bool IsImmutable(Type type) =>
            type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) || type == typeof(SemanticKey);

        private static (FieldInfo info, bool isReference)[] SavedFields(Type type)
        {
            lock (Gate)
            {
                if (FieldCache.TryGetValue(type, out var cached)) return cached;
            }

            var fields = new List<(FieldInfo, bool)>();
            for (var t = type; t != null && t != typeof(object) && t != typeof(ValueType); t = t.BaseType)
            {
                foreach (var info in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (JsonTypes.IsSavedField(info, out bool isReference)) fields.Add((info, isReference));
                }
            }

            var result = fields.ToArray();
            lock (Gate)
            {
                FieldCache[type] = result;
            }
            return result;
        }

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();
            public new bool Equals(object x, object y) => ReferenceEquals(x, y);
            public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
        }
    }
}
