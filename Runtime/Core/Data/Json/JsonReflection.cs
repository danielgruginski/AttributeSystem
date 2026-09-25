using ReactiveSolutions.AttributeSystem.Core.Modifiers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Core.Data.Json
{
    /// <summary>
    /// A field that is saved in JSON. The rules are Unity's: public, [SerializeField] or [SerializeReference] fields
    /// that aren't static, readonly or [NonSerialized]. In JSON a field is named in camelCase without a "_" or "m_"
    /// prefix: Coefficient is "coefficient", _maxRange is "maxRange".
    /// </summary>
    internal sealed class JsonField
    {
        public FieldInfo Info { get; }
        public string Name { get; }
        public bool IsReference { get; }
        public Type Type => Info.FieldType;

        private JsonField(FieldInfo info, string name, bool isReference)
        {
            Info = info;
            Name = name;
            IsReference = isReference;
        }

        private static readonly Dictionary<Type, JsonField[]> Cache = new Dictionary<Type, JsonField[]>();
        private static readonly object Gate = new object();

        /// <summary>The saved fields of <paramref name="type"/>, base class fields first.</summary>
        public static JsonField[] Of(Type type)
        {
            lock (Gate)
            {
                if (Cache.TryGetValue(type, out var cached)) return cached;
            }

            var chain = new List<Type>();
            for (var t = type; t != null && t != typeof(object) && t != typeof(ValueType); t = t.BaseType) chain.Add(t);
            chain.Reverse();

            var fields = new List<JsonField>();
            foreach (var t in chain)
            {
                foreach (var info in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (!JsonTypes.IsSavedField(info, out bool isReference)) continue;

                    string name = ToJsonName(info.Name);
                    var clash = fields.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
                    if (clash != null)
                    {
                        throw new InvalidOperationException(
                            $"{type.Name} can't be saved to JSON: its fields '{clash.Info.Name}' and '{info.Name}' would both be saved as '{name}'.");
                    }
                    fields.Add(new JsonField(info, name, isReference));
                }
            }

            var result = fields.ToArray();
            lock (Gate)
            {
                Cache[type] = result;
            }
            return result;
        }

        /// <summary>The field named <paramref name="name"/> in JSON (ignoring case), or null.</summary>
        public static JsonField Find(JsonField[] fields, string name) =>
            fields.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));

        /// <summary>"Coefficient" -> "coefficient", "_maxRange" -> "maxRange", "m_Speed" -> "speed", "HPBonus" -> "hpBonus".</summary>
        public static string ToJsonName(string fieldName)
        {
            string name = fieldName;

            // An auto-property's backing field: "<Speed>k__BackingField"
            if (name.StartsWith("<"))
            {
                int end = name.IndexOf('>');
                if (end > 1) name = name.Substring(1, end - 1);
            }

            if (name.StartsWith("m_") && name.Length > 2) name = name.Substring(2);
            name = name.TrimStart('_');
            if (name.Length == 0) name = fieldName;

            return CamelCase(name);
        }

        /// <summary>Lowercases the leading capitals of <paramref name="name"/>: "Input" -> "input", "HPBonus" -> "hpBonus".</summary>
        public static string CamelCase(string name)
        {
            if (string.IsNullOrEmpty(name) || !char.IsUpper(name[0])) return name;

            var chars = name.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                // Keep the capital that starts the next word ("HPBonus": the 'B').
                if (i > 0 && i + 1 < chars.Length && !char.IsUpper(chars[i + 1])) break;
                if (!char.IsUpper(chars[i])) break;
                chars[i] = char.ToLowerInvariant(chars[i]);
            }
            return new string(chars);
        }

        public static string ListNames(JsonField[] fields) =>
            fields.Length == 0 ? "it has none" : "its fields are " + string.Join(", ", fields.Select(f => f.Name));
    }

    /// <summary>
    /// The logic classes that JSON files can name: the same ones as the Logic dropdown (concrete [Serializable]
    /// ModifierLogic classes with a public parameterless constructor). A file names a logic class in camelCase without
    /// the "Logic" suffix ("linear", "diminishingReturns"), by its class name ("LinearLogic"), or by its full name
    /// with the namespace, which is needed when two classes have the same name.
    /// </summary>
    internal static class LogicTypes
    {
        /// <summary>
        /// The names of a modifier's or an effect action's other properties, which a logic can't use as its short name.
        /// </summary>
        private static readonly string[] ReservedNames = { "target", "type", "priority", "source", "condition", "chance" };

        private static readonly object Gate = new object();
        private static Dictionary<string, List<Type>> _byName;
        private static readonly Dictionary<Type, ModifierLogic> Defaults = new Dictionary<Type, ModifierLogic>();

        public static bool CanBeSaved(Type type) =>
            typeof(ModifierLogic).IsAssignableFrom(type) && !type.IsAbstract && !type.ContainsGenericParameters &&
            type.IsDefined(typeof(SerializableAttribute), false) && type.GetConstructor(Type.EmptyTypes) != null;

        /// <summary>The logic's short name without "Logic": LinearLogic -> "Linear".</summary>
        public static string ShortName(Type type) => ModifierLogic.GetDisplayName(type);

        /// <summary>The name a file uses for <paramref name="type"/>: "linear", or a longer one if that is ambiguous.</summary>
        public static string NameOf(Type type)
        {
            if (!CanBeSaved(type))
            {
                throw new InvalidOperationException(
                    $"a {type.Name} can't be saved to JSON: a logic class needs [Serializable] and a public parameterless constructor.");
            }

            var byName = ByName();
            string shortName = JsonField.CamelCase(ShortName(type));
            if (!ReservedNames.Contains(shortName, StringComparer.OrdinalIgnoreCase) && IsOnly(byName, shortName, type)) return shortName;
            if (!ReservedNames.Contains(type.Name, StringComparer.OrdinalIgnoreCase) && IsOnly(byName, type.Name, type)) return type.Name;
            return type.FullName;
        }

        /// <summary>
        /// The logic class named <paramref name="name"/> (ignoring case). Null if there is none, or if the name is
        /// ambiguous: then <paramref name="ambiguity"/> says which classes it could be.
        /// </summary>
        public static Type Find(string name, out string ambiguity)
        {
            ambiguity = null;
            if (!ByName().TryGetValue(name, out var types)) return null;
            if (types.Count == 1) return types[0];

            ambiguity = $"'{name}' could be any of these logic classes: {string.Join(", ", types.Select(t => t.FullName))}. " +
                        $"Write the full name, e.g. \"{types[0].FullName}\"";
            return null;
        }

        /// <summary>A new logic object with the class's default values.</summary>
        public static ModifierLogic Create(Type type) => (ModifierLogic)Activator.CreateInstance(type);

        /// <summary>An instance with the class's default values, to leave out fields that have them. Never modified.</summary>
        public static ModifierLogic DefaultsOf(Type type)
        {
            lock (Gate)
            {
                if (!Defaults.TryGetValue(type, out var defaults))
                {
                    defaults = Create(type);
                    Defaults.Add(type, defaults);
                }
                return defaults;
            }
        }

        /// <summary>The short names of all logic classes, for error messages.</summary>
        public static IEnumerable<string> AllNames() =>
            ByName().Values.SelectMany(types => types).Distinct()
                .Select(t => JsonField.CamelCase(ShortName(t)))
                .Distinct()
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase);

        private static bool IsOnly(Dictionary<string, List<Type>> byName, string name, Type type) =>
            byName.TryGetValue(name, out var types) && types.Count == 1 && types[0] == type;

        private static Dictionary<string, List<Type>> ByName()
        {
            lock (Gate)
            {
                if (_byName != null) return _byName;

                var byName = new Dictionary<string, List<Type>>(StringComparer.OrdinalIgnoreCase);
                var package = typeof(ModifierLogic).Assembly;
                string packageName = package.GetName().Name;

                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    // Only assemblies that use this package can define logic classes.
                    if (assembly.IsDynamic) continue;
                    if (assembly != package && assembly.GetReferencedAssemblies().All(r => r.Name != packageName)) continue;

                    foreach (var type in GetTypes(assembly))
                    {
                        if (type == null || !CanBeSaved(type)) continue;
                        Register(byName, ShortName(type), type);
                        Register(byName, type.Name, type);
                        Register(byName, type.FullName, type);
                    }
                }

                _byName = byName;
                return _byName;
            }
        }

        private static void Register(Dictionary<string, List<Type>> byName, string name, Type type)
        {
            if (string.IsNullOrEmpty(name)) return;
            if (!byName.TryGetValue(name, out var types))
            {
                types = new List<Type>();
                byName.Add(name, types);
            }
            if (!types.Contains(type)) types.Add(type);
        }

        private static Type[] GetTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types;
            }
        }
    }

    /// <summary>How the types of fields are saved.</summary>
    internal static class JsonTypes
    {
        public static bool IsInteger(Type type) =>
            type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte) ||
            type == typeof(uint) || type == typeof(ulong) || type == typeof(ushort) || type == typeof(sbyte);

        /// <summary>The element type of T[] or List&lt;T&gt;.</summary>
        public static bool TryGetListElement(Type type, out Type element)
        {
            if (type.IsArray && type.GetArrayRank() == 1)
            {
                element = type.GetElementType();
                return true;
            }
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                element = type.GetGenericArguments()[0];
                return true;
            }
            element = null;
            return false;
        }

        /// <summary>A [Serializable] class or struct of your own: saved as an object with its fields.</summary>
        public static bool IsRecord(Type type) =>
            !type.IsPrimitive && !type.IsEnum && !type.IsArray && !type.IsAbstract && !type.IsInterface &&
            type != typeof(string) && !typeof(Delegate).IsAssignableFrom(type) &&
            !IsFrameworkType(type) && !IsUnityType(type) &&
            type.IsDefined(typeof(SerializableAttribute), false);

        /// <summary>A Unity object reference, or a Unity type such as Vector3 or Color.</summary>
        public static bool IsUnityType(Type type) =>
            typeof(UnityEngine.Object).IsAssignableFrom(type) ||
            (type.Namespace != null && (type.Namespace == "UnityEngine" || type.Namespace.StartsWith("UnityEngine.")));

        /// <summary>A field type that can hold a logic object ([SerializeReference] ModifierLogic, or a base of it).</summary>
        public static bool CanHoldLogic(Type type) =>
            typeof(ModifierLogic).IsAssignableFrom(type) || type.IsAssignableFrom(typeof(ModifierLogic));

        /// <summary>
        /// Whether Unity saves the field: a public, [SerializeField] or [SerializeReference] instance field that isn't
        /// readonly or [NonSerialized], of a type Unity saves. <paramref name="isReference"/> is whether it is
        /// [SerializeReference].
        /// </summary>
        public static bool IsSavedField(FieldInfo info, out bool isReference)
        {
            isReference = false;
            if (info.IsStatic || info.IsInitOnly || info.IsLiteral) return false;
            if (info.IsDefined(typeof(NonSerializedAttribute), false)) return false;

            isReference = info.IsDefined(typeof(SerializeReference), false);
            if (!info.IsPublic && !isReference && !info.IsDefined(typeof(SerializeField), false)) return false;
            return IsSavedByUnity(info.FieldType, isReference);
        }

        /// <summary>
        /// Whether Unity saves a field of this type. The fields Unity doesn't save (dictionaries, interfaces,
        /// delegates, ...) are left out of JSON files too.
        /// </summary>
        public static bool IsSavedByUnity(Type type, bool isReference)
        {
            if (TryGetListElement(type, out var element))
            {
                // Unity doesn't save lists of lists.
                return !TryGetListElement(element, out _) && IsSavedByUnity(element, isReference);
            }

            if (isReference) return !type.IsValueType && !typeof(UnityEngine.Object).IsAssignableFrom(type);
            if (type == typeof(string) || type.IsEnum) return true;
            if (type.IsPrimitive) return type != typeof(IntPtr) && type != typeof(UIntPtr);
            if (IsUnityType(type)) return !type.IsInterface && !typeof(Delegate).IsAssignableFrom(type);
            return IsRecord(type);
        }

        private static bool IsFrameworkType(Type type) =>
            type.Namespace != null && (type.Namespace == "System" || type.Namespace.StartsWith("System."));
    }
}
