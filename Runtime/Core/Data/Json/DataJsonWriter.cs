using ReactiveSolutions.AttributeSystem.Core.Modifiers;
using SemanticKeys;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Core.Data.Json
{
    /// <summary>
    /// Writes StatBlocks and EntityProfiles in the format DataJsonReader reads: one property per builder call,
    /// keys by name, and a "keys" table at the end with each name's GUID. Entries that do nothing (a tag or base
    /// value with no key) are left out, and so are logic fields that have their class's default value.
    /// </summary>
    internal sealed class DataJsonWriter
    {
        private const int MaxDepth = 64;

        private readonly KeyTableWriter _keys;
        private readonly HashSet<object> _writing = new HashSet<object>(ReferenceComparer.Instance);
        private int _depth;

        private DataJsonWriter(KeyTableWriter keys)
        {
            _keys = keys;
        }

        public static string WriteStatBlock(StatBlock block)
        {
            if (block == null) throw new ArgumentNullException(nameof(block));
            var writer = new DataJsonWriter(new KeyTableWriter());
            var root = writer.StatBlock(block, "");
            writer.AddKeyTable(root);
            return JsonWriter.Write(root);
        }

        public static string WriteProfile(EntityProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            var writer = new DataJsonWriter(new KeyTableWriter());
            var root = writer.Profile(profile, "");
            writer.AddKeyTable(root);
            return JsonWriter.Write(root);
        }

        private void AddKeyTable(JsonNode root)
        {
            if (_keys.Table.Properties.Count > 0) root.Add("keys", _keys.Table);
        }

        // ---------------------------------------------------------------- StatBlocks and profiles

        private JsonNode StatBlock(StatBlock block, string path)
        {
            var node = JsonNode.NewObject();
            node.Expanded = true;

            if (!string.IsNullOrEmpty(block.BlockName)) node.Add("statBlock", JsonNode.From(block.BlockName));

            var condition = Condition(block.ActivationCondition, Child(path, "condition"), topLevel: true);
            if (condition != null) node.Add("condition", condition);

            AddMap(node, "baseValues", block.BaseValues, path, entry => entry.Name, (entry, at) => JsonNode.From(entry.Value));
            AddKeys(node, "tags", block.Tags);

            var remoteTags = JsonNode.NewArray();
            for (int i = 0; i < Count(block.RemoteTags); i++)
            {
                var spec = block.RemoteTags[i];
                if (spec == null || spec.Tag == SemanticKey.None) continue;
                remoteTags.Add(JsonNode.From(PathText(spec.TargetPath, spec.Tag, Index(Child(path, "remoteTags"), i))));
            }
            if (remoteTags.Items.Count > 0) node.Add("remoteTags", remoteTags);

            AddMap(node, "pointers", block.Pointers, path, pointer => pointer.Alias, (pointer, at) => Reference(pointer.Target, at));

            var modifiers = JsonNode.NewArray();
            modifiers.Expanded = true;
            for (int i = 0; i < Count(block.Modifiers); i++)
            {
                if (block.Modifiers[i] != null) modifiers.Add(Modifier(block.Modifiers[i], Index(Child(path, "modifiers"), i)));
            }
            if (modifiers.Items.Count > 0) node.Add("modifiers", modifiers);

            return node;
        }

        private JsonNode Profile(EntityProfile profile, string path)
        {
            if (!_writing.Add(profile))
            {
                throw Error(path, $"profile '{profile.ProfileName}' contains itself, as a template or a nested entity");
            }

            try
            {
                var node = JsonNode.NewObject();
                node.Expanded = true;

                if (!string.IsNullOrEmpty(profile.ProfileName)) node.Add("profile", JsonNode.From(profile.ProfileName));

                // A template built in code is written in full; one from a file, by its ID.
                var templates = JsonNode.NewArray();
                for (int i = 0; i < Count(profile.Templates); i++)
                {
                    var entry = profile.Templates[i];
                    if (entry.Profile != null) templates.Add(Profile(entry.Profile, Index(Child(path, "templates"), i)));
                    else if (!string.IsNullOrEmpty(entry.ProfileId)) templates.Add(JsonNode.From(entry.ProfileId));
                }
                if (templates.Items.Count > 0) node.Add("templates", templates);

                if (profile.ParentKey != SemanticKey.None) node.Add("parentKey", KeyNode(profile.ParentKey));

                AddMap(node, "baseAttributes", profile.BaseAttributes, path, entry => entry.Attribute, (entry, at) => JsonNode.From(entry.BaseValue));

                // "Health": "MaxHealth", or an object when the pool doesn't keep its percentage.
                AddMap(node, "pools", profile.Pools, path, pool => pool.Resource, (pool, at) =>
                    pool.OnMaxChange == PoolMaxChange.KeepPercent
                        ? ValueSource(pool.Max, at)
                        : JsonNode.NewObject()
                            .Add("max", ValueSource(pool.Max, Child(at, "max")))
                            .Add("onMaxChange", EnumNode(typeof(PoolMaxChange), pool.OnMaxChange, Child(at, "onMaxChange"))));
                AddKeys(node, "innateTags", profile.InnateTags);
                AddKeys(node, "linkGroups", profile.LinkGroups);

                // Likewise for nested profiles.
                AddMap(node, "nestedEntities", profile.NestedEntities, path, entry => entry.ProviderKey,
                    (entry, at) => entry.Profile != null ? Profile(entry.Profile, at) : JsonNode.From(entry.ProfileId),
                    entry => entry.Profile != null || !string.IsNullOrEmpty(entry.ProfileId));

                AddMap(node, "pointers", profile.Pointers, path, pointer => pointer.Alias,
                    (pointer, at) => pointer.TargetAttribute == SemanticKey.None
                        ? JsonNode.NewNull()
                        : JsonNode.From(PathText(pointer.ProviderPath, pointer.TargetAttribute, at)));

                // The IDs, then the StatBlocks stored in the profile: the order they are applied in.
                var statBlocks = JsonNode.NewArray();
                foreach (var id in profile.InnateStatBlockIds ?? new List<StatBlockID>())
                {
                    if (!string.IsNullOrEmpty(id)) statBlocks.Add(JsonNode.From((string)id));
                }
                for (int i = 0; i < Count(profile.InnateStatBlocks); i++)
                {
                    var block = profile.InnateStatBlocks[i];
                    if (block != null) statBlocks.Add(StatBlock(block, Index(Child(path, "innateStatBlocks"), i)));
                }
                if (statBlocks.Items.Count > 0) node.Add("innateStatBlocks", statBlocks);

                return node;
            }
            finally
            {
                _writing.Remove(profile);
            }
        }

        // ---------------------------------------------------------------- Modifiers and logic

        private JsonNode Modifier(AttributeModifierSpec spec, string path)
        {
            var node = JsonNode.NewObject();

            if (spec.TargetAttribute != SemanticKey.None)
            {
                node.Add("target", JsonNode.From(PathText(spec.TargetPath, spec.TargetAttribute, Child(path, "target"))));
            }
            if (spec.Type != ModifierType.Additive) node.Add("type", EnumNode(typeof(ModifierType), spec.Type, Child(path, "type")));
            if (spec.Priority != 0) node.Add("priority", JsonNode.From(spec.Priority));
            if (!string.IsNullOrEmpty(spec.SourceId)) node.Add("source", JsonNode.From(spec.SourceId));

            if (spec.Logic != null)
            {
                string name = LogicName(spec.Logic, path);
                node.Add(name, Logic(spec.Logic, Child(path, name)));
            }
            return node;
        }

        private static string LogicName(ModifierLogic logic, string path)
        {
            try
            {
                return LogicTypes.NameOf(logic.GetType());
            }
            catch (InvalidOperationException e)
            {
                throw Error(path, e.Message);
            }
        }

        /// <summary>The logic's fields that don't have their default value, or the value alone for a one-field logic.</summary>
        private JsonNode Logic(ModifierLogic logic, string path)
        {
            var type = logic.GetType();
            var fields = Fields(type, path);

            if (fields.Length == 1 && CanBeWrittenAlone(fields[0]))
            {
                var field = fields[0];
                return Value(field.Type, field.IsReference, field.Info.GetValue(logic), path);
            }

            var defaults = LogicTypes.DefaultsOf(type);
            var node = JsonNode.NewObject();
            foreach (var field in fields)
            {
                string at = Child(path, field.Name);
                object value = field.Info.GetValue(logic);
                if (IsDefault(field, value, field.Info.GetValue(defaults), at)) continue;
                node.Add(field.Name, Value(field.Type, field.IsReference, value, at));
            }
            return node;
        }

        /// <summary>
        /// Compares the values as JSON, with keys named by GUID so two keys that share a name are told apart.
        /// Equal values are skipped before that, so a field that can't be saved is only an error if it was changed.
        /// </summary>
        private static bool IsDefault(JsonField field, object value, object defaultValue, string path)
        {
            if (Equals(value, defaultValue)) return true;
            var scratch = new DataJsonWriter(new KeyTableWriter(useGuids: true));
            return JsonNode.DeepEquals(
                scratch.Value(field.Type, field.IsReference, value, path),
                scratch.Value(field.Type, field.IsReference, defaultValue, path));
        }

        /// <summary>
        /// Whether a one-field logic can be written as the field's value: when the value is a number, text or an
        /// array. (An object would read as the logic's fields, and a type JSON can't hold is only left out as a default.)
        /// </summary>
        private static bool CanBeWrittenAlone(JsonField field)
        {
            var type = field.Type;
            if (JsonTypes.TryGetListElement(type, out _)) return true;
            if (field.IsReference) return false;
            return type == typeof(ValueSource) || type == typeof(SemanticKey) || type == typeof(AttributeReference) ||
                   type == typeof(string) || type == typeof(bool) || type == typeof(float) || type == typeof(double) ||
                   type == typeof(char) || JsonTypes.IsInteger(type) || type.IsEnum;
        }

        private static JsonField[] Fields(Type type, string path)
        {
            try
            {
                return JsonField.Of(type);
            }
            catch (InvalidOperationException e)
            {
                throw Error(path, e.Message);
            }
        }

        // ---------------------------------------------------------------- Field values

        /// <summary>The JSON of a field's value. Mirrors DataJsonReader.Value.</summary>
        private JsonNode Value(Type type, bool isReference, object value, string path)
        {
            if (++_depth > MaxDepth) throw Error(path, "the data is nested too deeply (does an object contain itself?)");
            try
            {
                if (type == typeof(ValueSource)) return ValueSource((ValueSource)value, path);
                if (type == typeof(SemanticKey)) return KeyNode((SemanticKey)value);
                if (type == typeof(AttributeReference)) return Reference((AttributeReference)value, path);
                if (type == typeof(StatBlockCondition)) return value == null ? JsonNode.NewNull() : Condition((StatBlockCondition)value, path, topLevel: false);

                if (JsonTypes.TryGetListElement(type, out var element))
                {
                    if (value == null) return JsonNode.NewNull();
                    var array = JsonNode.NewArray();
                    int i = 0;
                    foreach (var item in (IEnumerable)value)
                    {
                        array.Add(Value(element, isReference, item, Index(path, i++)));
                    }
                    return array;
                }

                if (isReference) return LogicReference(value, path);
                if (value == null) return JsonNode.NewNull();

                if (type == typeof(string)) return JsonNode.From((string)value);
                if (type == typeof(bool)) return JsonNode.From((bool)value);
                if (type == typeof(float)) return JsonNode.From((float)value);
                if (type == typeof(double)) return JsonNode.From((double)value);
                if (type == typeof(char)) return JsonNode.From(value.ToString());
                if (type == typeof(ulong)) return JsonNode.From((ulong)value);
                if (JsonTypes.IsInteger(type)) return JsonNode.From(Convert.ToInt64(value));
                if (type.IsEnum) return EnumNode(type, value, path);
                if (type == typeof(AnimationCurve)) return Curve((AnimationCurve)value);
                if (JsonTypes.IsRecord(type)) return Record(value, type, path);

                throw Error(path, $"{type.Name} values can't be saved in JSON files. " +
                                  "Mark the field [NonSerialized] if it doesn't need to be saved.");
            }
            finally
            {
                _depth--;
            }
        }

        private JsonNode LogicReference(object value, string path)
        {
            if (value == null) return JsonNode.NewNull();
            if (!(value is ModifierLogic logic))
            {
                throw Error(path, $"a {value.GetType().Name} in a [SerializeReference] field can't be saved in JSON files: only logic objects can");
            }

            string name = LogicName(logic, path);
            return JsonNode.NewObject().Add(name, Logic(logic, Child(path, name)));
        }

        private JsonNode Record(object value, Type type, string path)
        {
            if (!type.IsValueType && !_writing.Add(value)) throw Error(path, $"the {type.Name} contains itself");
            try
            {
                var node = JsonNode.NewObject();
                foreach (var field in Fields(type, path))
                {
                    string at = Child(path, field.Name);
                    node.Add(field.Name, Value(field.Type, field.IsReference, field.Info.GetValue(value), at));
                }
                return node;
            }
            finally
            {
                if (!type.IsValueType) _writing.Remove(value);
            }
        }

        private static JsonNode Curve(AnimationCurve curve)
        {
            var keys = JsonNode.NewArray();
            foreach (var key in curve.keys)
            {
                var keyNode = JsonNode.NewObject()
                    .Add("time", JsonNode.From(key.time))
                    .Add("value", JsonNode.From(key.value))
                    .Add("inTangent", JsonNode.From(key.inTangent))
                    .Add("outTangent", JsonNode.From(key.outTangent));
                if (key.weightedMode != WeightedMode.None)
                {
                    keyNode.Add("inWeight", JsonNode.From(key.inWeight))
                        .Add("outWeight", JsonNode.From(key.outWeight))
                        .Add("weightedMode", JsonNode.From(key.weightedMode.ToString()));
                }
                keys.Add(keyNode);
            }

            var node = JsonNode.NewObject().Add("keys", keys);
            var defaults = new AnimationCurve();
            if (curve.preWrapMode != defaults.preWrapMode) node.Add("preWrapMode", JsonNode.From(curve.preWrapMode.ToString()));
            if (curve.postWrapMode != defaults.postWrapMode) node.Add("postWrapMode", JsonNode.From(curve.postWrapMode.ToString()));
            return node;
        }

        private static JsonNode EnumNode(Type type, object value, string path)
        {
            if (!type.IsDefined(typeof(FlagsAttribute), false) && !Enum.IsDefined(type, value))
            {
                throw Error(path, $"{Convert.ToInt64(value)} isn't one of the values of {type.Name}");
            }
            return JsonNode.From(value.ToString());
        }

        // ---------------------------------------------------------------- Conditions

        /// <summary>Null for an always-true condition at the top level (the default); {} further down.</summary>
        private JsonNode Condition(StatBlockCondition condition, string path, bool topLevel)
        {
            if (condition == null || condition.Type == StatBlockCondition.Mode.Always)
            {
                return topLevel ? null : JsonNode.NewObject();
            }
            if (!_writing.Add(condition)) throw Error(path, "the condition contains itself");

            try
            {
                switch (condition.Type)
                {
                    case StatBlockCondition.Mode.Tag:
                        var tag = condition.Tag == SemanticKey.None
                            ? JsonNode.NewNull()
                            : JsonNode.From(PathText(condition.TagTarget, condition.Tag, path));
                        return JsonNode.NewObject().Add(condition.InvertTag ? "lacksTag" : "hasTag", tag);

                    case StatBlockCondition.Mode.ValueComparison:
                        string at = Child(path, "compare");
                        string symbol = DataJsonReader.Symbol(condition.CompareOp)
                                        ?? throw Error(at, $"{(int)condition.CompareOp} isn't a comparison");
                        var node = JsonNode.NewObject().Add("compare", JsonNode.NewArray()
                            .Add(ValueSource(condition.ValueA, Index(at, 0)))
                            .Add(JsonNode.From(symbol))
                            .Add(ValueSource(condition.ValueB, Index(at, 2))));
                        if (condition.Tolerance != StatBlockCondition.DefaultTolerance) node.Add("tolerance", JsonNode.From(condition.Tolerance));
                        return node;

                    case StatBlockCondition.Mode.Composite:
                        string group = condition.GroupOp == StatBlockCondition.Operator.And ? "all" : "any";
                        var conditions = JsonNode.NewArray();
                        for (int i = 0; i < Count(condition.SubConditions); i++)
                        {
                            // A missing sub-condition counts as true, like {}.
                            conditions.Add(Condition(condition.SubConditions[i], Index(Child(path, group), i), topLevel: false));
                        }
                        return JsonNode.NewObject().Add(group, conditions);

                    default:
                        throw Error(path, $"unknown condition type {(int)condition.Type}");
                }
            }
            finally
            {
                _writing.Remove(condition);
            }
        }

        // ---------------------------------------------------------------- Keys, paths and values

        private JsonNode ValueSource(ValueSource source, string path)
        {
            if (source == null) return JsonNode.NewNull();

            switch (source.Mode)
            {
                case Core.ValueSource.SourceMode.Constant:
                    if (float.IsNaN(source.ConstantValue) || float.IsInfinity(source.ConstantValue))
                    {
                        throw Error(path, "the constant is NaN or infinite, which JSON can't hold");
                    }
                    return JsonNode.From(source.ConstantValue);
                case Core.ValueSource.SourceMode.Attribute:
                    // An attribute not picked yet reads as 0, like a missing value.
                    return Reference(source.AttributeRef, path);
                default:
                    throw Error(path, $"unknown value source mode {(int)source.Mode}");
            }
        }

        /// <summary>"Owner/Strength", or null when no attribute is set.</summary>
        private JsonNode Reference(AttributeReference reference, string path) =>
            reference.Name == SemanticKey.None
                ? JsonNode.NewNull()
                : JsonNode.From(PathText(reference.Path, reference.Name, path));

        /// <summary>The key names of the path and then <paramref name="name"/>, separated by '/'.</summary>
        private string PathText(IList<SemanticKey> providerPath, SemanticKey name, string path)
        {
            var sb = new StringBuilder();
            if (providerPath != null)
            {
                foreach (var step in providerPath)
                {
                    if (step == SemanticKey.None) throw Error(path, "the provider path has an entry with no key: pick one, or remove the entry");
                    sb.Append(_keys.NameOf(step)).Append('/');
                }
            }
            sb.Append(_keys.NameOf(name));
            return sb.ToString();
        }

        private JsonNode KeyNode(SemanticKey key) => key == SemanticKey.None ? JsonNode.NewNull() : JsonNode.From(_keys.NameOf(key));

        /// <summary>An array of key names. Unassigned keys do nothing, so they are left out.</summary>
        private void AddKeys(JsonNode node, string name, List<SemanticKey> keys)
        {
            var array = JsonNode.NewArray();
            foreach (var key in keys ?? new List<SemanticKey>())
            {
                if (key != SemanticKey.None) array.Add(JsonNode.From(_keys.NameOf(key)));
            }
            if (array.Items.Count > 0) node.Add(name, array);
        }

        /// <summary>An object of key names and values. Entries with no key (or that <paramref name="include"/> rejects) do nothing, so they are left out.</summary>
        private void AddMap<T>(JsonNode node, string name, List<T> entries, string path, Func<T, SemanticKey> key,
            Func<T, string, JsonNode> value, Func<T, bool> include = null)
        {
            if (entries == null) return;

            var map = JsonNode.NewObject();
            foreach (var entry in entries)
            {
                var entryKey = key(entry);
                if (entryKey == SemanticKey.None || (include != null && !include(entry))) continue;

                string label = _keys.NameOf(entryKey);
                map.Add(label, value(entry, Child(Child(path, name), label)));
            }
            if (map.Properties.Count > 0) node.Add(name, map);
        }

        private static int Count(IList list) => list?.Count ?? 0;

        private static string Child(string path, string name) => DataJsonReader.Child(path, name);

        private static string Index(string path, int index) => $"{path}[{index}]";

        private static InvalidOperationException Error(string path, string message) =>
            new InvalidOperationException($"Can't save {(path.Length == 0 ? "the data" : path)}: {message}");

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();
            public new bool Equals(object x, object y) => ReferenceEquals(x, y);
            public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
        }
    }
}
