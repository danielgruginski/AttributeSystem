using ReactiveSolutions.AttributeSystem.Core.Builders;
using ReactiveSolutions.AttributeSystem.Core.Modifiers;
using SemanticKeys;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace ReactiveSolutions.AttributeSystem.Core.Data.Json
{
    /// <summary>
    /// Builds StatBlocks, EntityProfiles, Effects and StatusEffects from JSON by calling their builders: each property of
    /// the file is a builder call ("tags": ["Magical"] is AddTag(Magical), a modifier is AddModifier(...)). Keys are written by
    /// name and resolved with the file's "keys" table. Every error says where it is: its path in the file, line
    /// and column. See Documentation/JSON Format.md.
    /// </summary>
    internal sealed class DataJsonReader
    {
        private static readonly string[] StatBlockProperties =
            { "statBlock", "condition", "baseValues", "tags", "remoteTags", "pointers", "modifiers", "keys" };
        private static readonly string[] ProfileProperties =
        {
            "profile", "templates", "parentKey", "baseAttributes", "pools", "innateTags", "linkGroups", "nestedEntities",
            "pointers", "innateStatBlocks", "keys"
        };
        private static readonly string[] EffectProperties = { "effect", "condition", "costs", "actions", "removeStatuses", "statuses", "keys" };
        private static readonly string[] StatusProperties =
        {
            "status", "categories", "condition", "duration", "stacking", "maxStacks", "statBlock", "tick", "onApply", "onExpire", "keys"
        };
        private static readonly string[] ModifierProperties = { "target", "type", "priority", "source" };
        private static readonly string[] ActionProperties = { "target", "type", "condition", "chance" };
        private static readonly string[] ConditionKinds = { "hasTag", "lacksTag", "compare", "all", "any" };

        private const string AttributeExample = "an attribute such as \"Strength\" or \"Owner/Strength\"";
        private const string TagExample = "a tag such as \"Stunned\" or \"Owner/Stunned\"";
        private const string FormulaExample = "{ \"linear\": { \"input\": \"Defense\", \"addend\": 100 } }";

        private readonly KeyTableReader _keys;

        // In an effect, every path starts with a role: "Source/Mana", "Target/Health".
        private readonly bool _roles;

        private DataJsonReader(KeyTableReader keys, bool roles)
        {
            _keys = keys;
            _roles = roles;
        }

        public static StatBlock ReadStatBlock(string json, Func<string, SemanticKey> findKey)
        {
            var root = JsonParser.Parse(json);
            return Create(root, findKey).StatBlock(root, "", isRoot: true);
        }

        public static EntityProfile ReadProfile(string json, Func<string, SemanticKey> findKey)
        {
            var root = JsonParser.Parse(json);
            return Create(root, findKey).Profile(root, "", isRoot: true);
        }

        public static Effect ReadEffect(string json, Func<string, SemanticKey> findKey)
        {
            var root = JsonParser.Parse(json);
            return Create(root, findKey, roles: true).Effect(root, "", isRoot: true);
        }

        public static StatusEffect ReadStatusEffect(string json, Func<string, SemanticKey> findKey)
        {
            var root = JsonParser.Parse(json);
            return Create(root, findKey, roles: true).Status(root, "");
        }

        private static DataJsonReader Create(JsonNode root, Func<string, SemanticKey> findKey, bool roles = false)
        {
            if (root.Kind != JsonKind.Object) throw Error(root, "", $"expected an object ({{ ... }}), found {root.Describe()}");
            CheckDuplicates(root, "");
            return new DataJsonReader(new KeyTableReader(root.Find("keys"), findKey), roles);
        }

        // ---------------------------------------------------------------- StatBlocks and profiles

        private StatBlock StatBlock(JsonNode node, string path, bool isRoot)
        {
            ExpectObject(node, path, "a StatBlock");
            CheckDuplicates(node, path);

            var name = node.Find("statBlock");
            var builder = StatBlockBuilder.Create(name != null ? String(name, Child(path, "statBlock")) ?? "" : "");

            foreach (var property in node.Properties)
            {
                string at = Child(path, property.Key);
                var value = property.Value;

                switch (Match(property.Key, StatBlockProperties))
                {
                    case "statBlock":
                        break;

                    case "condition":
                        builder.SetCondition(Condition(value, at));
                        break;

                    case "baseValues":
                        foreach (var entry in Map(value, at, "attributes and their base values, e.g. { \"Durability\": 100 }"))
                        {
                            string entryPath = Child(at, entry.Key);
                            builder.AddBaseValue(KeyName(entry.Key, entry.Value, entryPath), Float(entry.Value, entryPath));
                        }
                        break;

                    case "tags":
                        foreach (var (item, itemPath) in Array(value, at, "an array of tags, e.g. [\"Magical\"]"))
                        {
                            builder.AddTag(Key(item, itemPath));
                        }
                        break;

                    case "remoteTags":
                        foreach (var (item, itemPath) in Array(value, at, "an array of tags on other entities, e.g. [\"Owner/Armed\"]"))
                        {
                            var tag = Reference(item, itemPath, TagExample);
                            builder.AddRemoteTag(tag.Name, tag.Path.ToArray());
                        }
                        break;

                    case "pointers":
                        foreach (var entry in Map(value, at, "aliases and the attributes they point to, e.g. { \"MainStat\": \"Owner/Strength\" }"))
                        {
                            string entryPath = Child(at, entry.Key);
                            var target = Reference(entry.Value, entryPath, AttributeExample);
                            builder.AddPointer(KeyName(entry.Key, entry.Value, entryPath), target.Name, target.Path.ToArray());
                        }
                        break;

                    case "modifiers":
                        foreach (var (item, itemPath) in Array(value, at, "an array of modifiers"))
                        {
                            Modifier(builder, item, itemPath);
                        }
                        break;

                    case "keys":
                        if (!isRoot) throw Error(value, at, "the \"keys\" table belongs at the top level of the file");
                        break;

                    default:
                        throw UnknownProperty(property, at, StatBlockProperties, isRoot ? FileKindHint(property.Key, "StatBlock") : "");
                }
            }

            return builder.Build();
        }

        private EntityProfile Profile(JsonNode node, string path, bool isRoot)
        {
            ExpectObject(node, path, "an entity profile");
            CheckDuplicates(node, path);

            var name = node.Find("profile");
            var builder = ProfileBuilder.Create(name != null ? String(name, Child(path, "profile")) ?? "" : "");

            foreach (var property in node.Properties)
            {
                string at = Child(path, property.Key);
                var value = property.Value;

                switch (Match(property.Key, ProfileProperties))
                {
                    case "profile":
                        break;

                    case "templates":
                        foreach (var (item, itemPath) in Array(value, at, "an array of template profile IDs, e.g. [\"Templates/Character\"]"))
                        {
                            if (item.Kind == JsonKind.Object)
                            {
                                builder.AddTemplate(Profile(item, itemPath, isRoot: false));
                            }
                            else
                            {
                                builder.AddTemplate(String(item, itemPath, "a template's profile ID such as \"Templates/Character\", or a profile object"));
                            }
                        }
                        break;

                    case "parentKey":
                        builder.SetParentKey(Key(value, at));
                        break;

                    case "baseAttributes":
                        foreach (var entry in Map(value, at, "attributes and their base values, e.g. { \"Health\": 100 }"))
                        {
                            string entryPath = Child(at, entry.Key);
                            builder.AddBaseAttribute(KeyName(entry.Key, entry.Value, entryPath), Float(entry.Value, entryPath));
                        }
                        break;

                    case "pools":
                        foreach (var entry in Map(value, at, "resources and their maximum, e.g. { \"Health\": \"MaxHealth\" }"))
                        {
                            string entryPath = Child(at, entry.Key);
                            var resource = KeyName(entry.Key, entry.Value, entryPath);
                            if (entry.Value.Kind == JsonKind.Object)
                            {
                                var (max, onMaxChange) = Pool(entry.Value, entryPath);
                                builder.AddPool(resource, max, onMaxChange);
                            }
                            else
                            {
                                builder.AddPool(resource, ValueSource(entry.Value, entryPath));
                            }
                        }
                        break;

                    case "innateTags":
                        foreach (var (item, itemPath) in Array(value, at, "an array of tags, e.g. [\"Undead\"]"))
                        {
                            builder.AddInnateTag(Key(item, itemPath));
                        }
                        break;

                    case "linkGroups":
                        foreach (var (item, itemPath) in Array(value, at, "an array of link group names, e.g. [\"Inventory\"]"))
                        {
                            builder.AddLinkGroup(Key(item, itemPath));
                        }
                        break;

                    case "nestedEntities":
                        foreach (var entry in Map(value, at, "provider keys and profile IDs, e.g. { \"RightHand\": \"Weapons/Sword\" }"))
                        {
                            string entryPath = Child(at, entry.Key);
                            var key = KeyName(entry.Key, entry.Value, entryPath);
                            if (entry.Value.Kind == JsonKind.Object)
                            {
                                builder.AddNestedEntity(key, Profile(entry.Value, entryPath, isRoot: false));
                            }
                            else
                            {
                                builder.AddNestedEntity(key, String(entry.Value, entryPath, "a profile ID such as \"Weapons/Sword\", or a profile object"));
                            }
                        }
                        break;

                    case "pointers":
                        foreach (var entry in Map(value, at, "aliases and the attributes they point to, e.g. { \"MainStat\": \"RightHand/Damage\" }"))
                        {
                            string entryPath = Child(at, entry.Key);
                            var target = Reference(entry.Value, entryPath, AttributeExample);
                            builder.AddPointer(KeyName(entry.Key, entry.Value, entryPath), target.Name, target.Path.ToArray());
                        }
                        break;

                    case "innateStatBlocks":
                        foreach (var (item, itemPath) in Array(value, at, "an array of StatBlock IDs and StatBlocks"))
                        {
                            if (item.Kind == JsonKind.Object)
                            {
                                builder.AddInnateStatBlock(StatBlock(item, itemPath, isRoot: false));
                            }
                            else
                            {
                                builder.AddInnateStatBlock((StatBlockID)String(item, itemPath, "a StatBlock ID such as \"Passives/Tough\", or a StatBlock object"));
                            }
                        }
                        break;

                    case "keys":
                        if (!isRoot) throw Error(value, at, "the \"keys\" table belongs at the top level of the file");
                        break;

                    default:
                        throw UnknownProperty(property, at, ProfileProperties, isRoot ? FileKindHint(property.Key, "profile") : "");
                }
            }

            return builder.Build();
        }

        /// <summary>A pool's settings: { "max": "MaxMana", "onMaxChange": "AddDifference" }.</summary>
        private (ValueSource max, PoolMaxChange onMaxChange) Pool(JsonNode node, string path)
        {
            CheckDuplicates(node, path);
            var names = new[] { "max", "onMaxChange" };
            ValueSource max = null;
            bool hasMax = false;
            var onMaxChange = PoolMaxChange.KeepPercent;

            foreach (var property in node.Properties)
            {
                string at = Child(path, property.Key);
                switch (Match(property.Key, names))
                {
                    case "max":
                        max = ValueSource(property.Value, at);
                        hasMax = true;
                        break;
                    case "onMaxChange":
                        onMaxChange = (PoolMaxChange)Enum(typeof(PoolMaxChange), property.Value, at);
                        break;
                    default:
                        string hint = LogicTypes.Find(property.Key, out _) != null
                            ? $" (a formula as the maximum goes in \"max\": {{ \"max\": {{ \"{property.Key}\": ... }} }})"
                            : "";
                        throw UnknownProperty(property, at, names, hint);
                }
            }

            if (!hasMax) throw Error(node, path, "a pool needs a \"max\": an attribute such as \"MaxHealth\", or a number");
            return (max, onMaxChange);
        }

        /// <summary>
        /// " (is this an effect file?)" when <paramref name="property"/> belongs to other kinds of file than
        /// <paramref name="kind"/> (a StatBlock, a profile or an effect), or "".
        /// </summary>
        private static string FileKindHint(string property, string kind)
        {
            var kinds = new List<string>();
            if (kind != "StatBlock" && Match(property, StatBlockProperties) != null) kinds.Add("a StatBlock");
            if (kind != "profile" && Match(property, ProfileProperties) != null) kinds.Add("an entity profile");
            if (kind != "effect" && Match(property, EffectProperties) != null) kinds.Add("an effect");
            if (kind != "status effect" && Match(property, StatusProperties) != null) kinds.Add("a status effect");
            return kinds.Count == 0 ? "" : $" (is this {string.Join(" or ", kinds)} file?)";
        }

        // ---------------------------------------------------------------- Effects

        private Effect Effect(JsonNode node, string path, bool isRoot)
        {
            ExpectObject(node, path, "an effect");
            CheckDuplicates(node, path);

            var name = node.Find("effect");
            var builder = EffectBuilder.Create(name != null ? String(name, Child(path, "effect")) ?? "" : "");

            foreach (var property in node.Properties)
            {
                string at = Child(path, property.Key);
                var value = property.Value;

                switch (Match(property.Key, EffectProperties))
                {
                    case "effect":
                        break;

                    case "keys":
                        if (!isRoot) throw Error(value, at, "the \"keys\" table belongs at the top level of the file");
                        break;

                    case "condition":
                        builder.SetCondition(Condition(value, at));
                        break;

                    case "costs":
                        foreach (var entry in Map(value, at, "resources and how much of each is spent, e.g. { \"Source/Mana\": 15 }"))
                        {
                            string entryPath = Child(at, entry.Key);
                            var resource = ParsePath(entry.Key, entry.Value, entryPath, isName: true);
                            builder.AddCost(resource, ValueSource(entry.Value, entryPath));
                        }
                        break;

                    case "actions":
                        foreach (var (item, itemPath) in Array(value, at, "an array of actions"))
                        {
                            Action(builder, item, itemPath);
                        }
                        break;

                    case "removeStatuses":
                        foreach (var (item, itemPath) in Array(value, at, "an array of status effect categories, e.g. [\"Debuff\"]"))
                        {
                            builder.RemoveStatuses(Key(item, itemPath));
                        }
                        break;

                    case "statuses":
                        foreach (var (item, itemPath) in Array(value, at, "an array of status effects, e.g. [\"Debuffs/Poison\"]"))
                        {
                            StatusEntry(builder, item, itemPath);
                        }
                        break;

                    default:
                        throw UnknownProperty(property, at, EffectProperties, isRoot ? FileKindHint(property.Key, "effect") : "");
                }
            }

            return builder.Build();
        }

        private void Action(EffectBuilder builder, JsonNode node, string path)
        {
            ExpectObject(node, path, "an action, e.g. { \"target\": \"Target/Health\", \"type\": \"Reduce\", \"value\": 10 }");
            CheckDuplicates(node, path);

            var target = new AttributeReference(SemanticKey.None);
            var type = EffectActionType.Add;
            StatBlockCondition condition = null;
            ValueSource chance = null;
            ModifierLogic logic = null;
            string logicProperty = null;

            foreach (var property in node.Properties)
            {
                string at = Child(path, property.Key);
                var value = property.Value;

                switch (Match(property.Key, ActionProperties))
                {
                    case "target":
                        target = Reference(value, at, AttributeExample);
                        break;
                    case "type":
                        type = (EffectActionType)Enum(typeof(EffectActionType), value, at);
                        break;
                    case "condition":
                        condition = Condition(value, at);
                        break;
                    case "chance":
                        chance = ValueSource(value, at);
                        break;
                    default:
                        // Any other property is the amount's logic, named after its class: "linear": { ... }
                        var logicType = LogicTypes.Find(property.Key, out string ambiguity);
                        if (logicType == null)
                        {
                            throw NameError(property, at, ambiguity ??
                                $"'{property.Key}' is neither an action property ({string.Join(", ", ActionProperties)}) nor a logic. " +
                                $"The logic names are: {string.Join(", ", LogicTypes.AllNames())}");
                        }
                        if (logicProperty != null)
                        {
                            throw NameError(property, at, $"an action has one logic, but this one has both '{logicProperty}' and '{property.Key}'");
                        }
                        logicProperty = property.Key;
                        logic = Logic(logicType, value, at);
                        break;
                }
            }

            builder.AddAction(target, type, logic, condition, chance);
        }

        /// <summary>A status effect an effect applies: its file's ID, or { "status": ID, "to", "condition", "chance" }.</summary>
        private void StatusEntry(EffectBuilder builder, JsonNode node, string path)
        {
            const string Expected = "a status effect ID such as \"Debuffs/Poison\", or { \"status\": \"Debuffs/Poison\", \"chance\": 0.3 }";
            if (node.Kind == JsonKind.String)
            {
                builder.ApplyStatus(node.Text);
                return;
            }
            ExpectObject(node, path, Expected);
            CheckDuplicates(node, path);

            var names = new[] { "status", "to", "condition", "chance" };
            string id = null;
            var to = EffectRole.Target;
            StatBlockCondition condition = null;
            ValueSource chance = null;

            foreach (var property in node.Properties)
            {
                string at = Child(path, property.Key);
                var value = property.Value;
                switch (Match(property.Key, names))
                {
                    case "status":
                        if (value.Kind == JsonKind.Object)
                        {
                            throw Error(value, at, "an effect refers to a status effect by its file's ID, e.g. \"Debuffs/Poison\": save the status as its own file");
                        }
                        id = String(value, at, "a status effect ID such as \"Debuffs/Poison\"");
                        break;
                    case "to":
                        to = (EffectRole)Enum(typeof(EffectRole), value, at);
                        break;
                    case "condition":
                        condition = Condition(value, at);
                        break;
                    case "chance":
                        chance = ValueSource(value, at);
                        break;
                    default:
                        throw UnknownProperty(property, at, names, "");
                }
            }

            if (string.IsNullOrEmpty(id)) throw Error(node, path, "a status entry needs a \"status\": the ID of a status effect file");
            builder.ApplyStatus(id, to, condition, chance);
        }

        // ---------------------------------------------------------------- Status effects

        private StatusEffect Status(JsonNode node, string path)
        {
            ExpectObject(node, path, "a status effect");
            CheckDuplicates(node, path);

            var name = node.Find("status");
            var builder = StatusEffectBuilder.Create(name != null ? String(name, Child(path, "status")) ?? "" : "");
            var stacking = StatusStacking.Refresh;
            int maxStacks = 0;

            foreach (var property in node.Properties)
            {
                string at = Child(path, property.Key);
                var value = property.Value;
                string matched = Match(property.Key, StatusProperties);

                switch (matched)
                {
                    case "status":
                    case "keys":
                        break;

                    case "categories":
                        foreach (var (item, itemPath) in Array(value, at, "an array of categories, e.g. [\"Debuff\"]"))
                        {
                            builder.AddCategory(Key(item, itemPath));
                        }
                        break;

                    case "condition":
                        builder.SetCondition(Condition(value, at));
                        break;

                    case "duration":
                        // No duration (or null) lasts until removed.
                        if (value.Kind != JsonKind.Null) builder.SetDuration(ValueSource(value, at));
                        break;

                    case "stacking":
                        stacking = (StatusStacking)Enum(typeof(StatusStacking), value, at);
                        break;

                    case "maxStacks":
                        maxStacks = (int)Integer(value, at, typeof(int));
                        if (maxStacks < 0) throw Error(value, at, "the most stacks can't be negative (0 is no limit)");
                        break;

                    case "statBlock":
                        if (value.Kind == JsonKind.Object)
                        {
                            // Applied to the entity that has the status: its paths are the entity's own, not roles.
                            builder.SetStatBlock(new DataJsonReader(_keys, roles: false).StatBlock(value, at, isRoot: false));
                        }
                        else if (value.Kind != JsonKind.Null)
                        {
                            builder.SetStatBlock(String(value, at, "a StatBlock ID such as \"Debuffs/Poison\", or a StatBlock object"));
                        }
                        break;

                    case "tick":
                        // null: no ticks.
                        if (value.Kind == JsonKind.Null) break;
                        var (every, effect) = Tick(value, at);
                        builder.SetTick(every, effect);
                        break;

                    case "onApply":
                    case "onExpire":
                        foreach (var (item, itemPath) in Array(value, at, "an array of effect IDs and effects"))
                        {
                            var entry = EffectEntry(item, itemPath);
                            if (matched == "onApply") builder.AddOnApply(entry);
                            else builder.AddOnExpire(entry);
                        }
                        break;

                    default:
                        throw UnknownProperty(property, at, StatusProperties, FileKindHint(property.Key, "status effect"));
                }
            }

            builder.SetStacking(stacking, maxStacks);
            return builder.Build();
        }

        /// <summary>A status's ticks: { "every": 1, "effect": "Debuffs/PoisonTick" }.</summary>
        private (float every, EffectEntry effect) Tick(JsonNode node, string path)
        {
            ExpectObject(node, path, "a tick, e.g. { \"every\": 1, \"effect\": \"Debuffs/PoisonTick\" }");
            CheckDuplicates(node, path);

            var names = new[] { "every", "effect" };
            float? every = null;
            EffectEntry effect = null;
            foreach (var property in node.Properties)
            {
                string at = Child(path, property.Key);
                switch (Match(property.Key, names))
                {
                    case "every":
                        every = Float(property.Value, at);
                        if (!(every > 0f)) throw Error(property.Value, at, "the time between ticks must be more than 0");
                        break;
                    case "effect":
                        effect = EffectEntry(property.Value, at);
                        break;
                    default:
                        throw UnknownProperty(property, at, names, "");
                }
            }

            if (every == null || effect == null)
            {
                throw Error(node, path, "a tick needs \"every\" (the time between ticks) and \"effect\" (what each tick does)");
            }
            return (every.Value, effect);
        }

        /// <summary>An effect a status applies: its file's ID, or an effect written in full.</summary>
        private EffectEntry EffectEntry(JsonNode node, string path)
        {
            if (node.Kind == JsonKind.String) return new EffectEntry { EffectId = node.Text };
            if (node.Kind == JsonKind.Object) return new EffectEntry { Effect = Effect(node, path, isRoot: false) };
            throw Error(node, path, $"expected an effect ID such as \"Debuffs/PoisonTick\", or an effect object, found {node.Describe()}");
        }

        // ---------------------------------------------------------------- Modifiers and logic

        private void Modifier(StatBlockBuilder builder, JsonNode node, string path)
        {
            ExpectObject(node, path, "a modifier, e.g. { \"target\": \"Damage\", \"value\": 5 }");
            CheckDuplicates(node, path);

            var target = new AttributeReference(SemanticKey.None);
            var type = ModifierType.Additive;
            int priority = 0;
            string source = null;
            ModifierLogic logic = null;
            string logicProperty = null;

            foreach (var property in node.Properties)
            {
                string at = Child(path, property.Key);
                var value = property.Value;

                switch (Match(property.Key, ModifierProperties))
                {
                    case "target":
                        target = Reference(value, at, AttributeExample);
                        break;
                    case "type":
                        type = (ModifierType)Enum(typeof(ModifierType), value, at);
                        break;
                    case "priority":
                        priority = (int)Integer(value, at, typeof(int));
                        break;
                    case "source":
                        source = String(value, at);
                        break;
                    default:
                        // Any other property is the logic, named after its class: "linear": { ... }
                        var logicType = LogicTypes.Find(property.Key, out string ambiguity);
                        if (logicType == null)
                        {
                            throw NameError(property, at, ambiguity ??
                                $"'{property.Key}' is neither a modifier property ({string.Join(", ", ModifierProperties)}) nor a logic. " +
                                $"The logic names are: {string.Join(", ", LogicTypes.AllNames())}");
                        }
                        if (logicProperty != null)
                        {
                            throw NameError(property, at, $"a modifier has one logic, but this one has both '{logicProperty}' and '{property.Key}'");
                        }
                        logicProperty = property.Key;
                        logic = Logic(logicType, value, at);
                        break;
                }
            }

            builder.AddModifier(target, logic, type, priority, source);
        }

        private ModifierLogic Logic(Type type, JsonNode value, string path)
        {
            var logic = LogicTypes.Create(type);
            var fields = Fields(type, value, path);
            string what = $"the {ModifierLogic.GetDisplayName(type)} logic";

            if (value.Kind == JsonKind.Object)
            {
                ReadFields(logic, fields, value, path, what);
            }
            else if (fields.Length == 1)
            {
                // A logic with one field can be written as that field's value: "value": 5
                fields[0].Info.SetValue(logic, Value(fields[0].Type, fields[0].IsReference, value, path));
            }
            else
            {
                throw Error(value, path, $"expected an object with the fields of {what} ({JsonField.ListNames(fields)}), found {value.Describe()}");
            }

            return logic;
        }

        /// <summary>A logic object in a [SerializeReference] field: { "linear": { ... } }.</summary>
        private ModifierLogic LogicReference(Type fieldType, JsonNode node, string path)
        {
            if (node.Kind == JsonKind.Null) return null;
            if (node.Kind != JsonKind.Object || node.Properties.Count != 1)
            {
                throw Error(node, path, $"expected a logic, e.g. {{ \"linear\": {{ \"input\": \"Strength\" }} }}, found {node.Describe()}");
            }

            var property = node.Properties[0];
            string at = Child(path, property.Key);
            var type = LogicTypes.Find(property.Key, out string ambiguity);
            if (type == null)
            {
                throw NameError(property, at, ambiguity ??
                    $"there is no logic named '{property.Key}'. The logic names are: {string.Join(", ", LogicTypes.AllNames())}");
            }
            if (!fieldType.IsAssignableFrom(type))
            {
                throw NameError(property, at, $"expected a {ModifierLogic.GetDisplayName(fieldType)} logic, found {ModifierLogic.GetDisplayName(type)}");
            }
            return Logic(type, property.Value, at);
        }

        private void ReadFields(object target, JsonField[] fields, JsonNode node, string path, string what)
        {
            CheckDuplicates(node, path);
            foreach (var property in node.Properties)
            {
                string at = Child(path, property.Key);
                var field = JsonField.Find(fields, property.Key);
                if (field == null) throw NameError(property, at, $"{what} has no field '{property.Key}' ({JsonField.ListNames(fields)})");
                field.Info.SetValue(target, Value(field.Type, field.IsReference, property.Value, at));
            }
        }

        private static JsonField[] Fields(Type type, JsonNode at, string path)
        {
            try
            {
                return JsonField.Of(type);
            }
            catch (InvalidOperationException e)
            {
                throw Error(at, path, e.Message);
            }
        }

        // ---------------------------------------------------------------- Field values

        /// <summary>The value of a field of type <paramref name="type"/>. Mirrors DataJsonWriter.Value.</summary>
        private object Value(Type type, bool isReference, JsonNode node, string path)
        {
            if (type == typeof(ValueSource)) return ValueSource(node, path);
            if (type == typeof(SemanticKey)) return Key(node, path);
            if (type == typeof(AttributeReference)) return Reference(node, path, AttributeExample);
            if (type == typeof(StatBlockCondition)) return node.Kind == JsonKind.Null ? null : Condition(node, path);
            if (JsonTypes.TryGetListElement(type, out var element)) return List(type, element, isReference, node, path);
            if (isReference)
            {
                if (node.Kind == JsonKind.Null) return null;
                if (!JsonTypes.CanHoldLogic(type)) throw Error(node, path, $"a [SerializeReference] {type.Name} can't be read from JSON: only logic objects can");
                return LogicReference(type, node, path);
            }

            if (type == typeof(string)) return String(node, path);
            if (type == typeof(bool)) return Bool(node, path);
            if (type == typeof(float)) return Float(node, path);
            if (type == typeof(double)) return Double(node, path);
            if (type == typeof(char)) return Char(node, path);
            if (JsonTypes.IsInteger(type)) return Integer(node, path, type);
            if (type.IsEnum) return Enum(type, node, path);
            if (type == typeof(AnimationCurve)) return Curve(node, path);
            if (JsonTypes.IsRecord(type)) return Record(type, node, path);

            throw Error(node, path, $"{type.Name} values can't be read from JSON");
        }

        private object List(Type type, Type element, bool isReference, JsonNode node, string path)
        {
            if (node.Kind == JsonKind.Null) return null;
            if (node.Kind != JsonKind.Array) throw Error(node, path, $"expected an array, found {node.Describe()}");

            if (type.IsArray)
            {
                var array = System.Array.CreateInstance(element, node.Items.Count);
                for (int i = 0; i < node.Items.Count; i++)
                {
                    array.SetValue(Value(element, isReference, node.Items[i], Index(path, i)), i);
                }
                return array;
            }

            var list = (IList)Activator.CreateInstance(type);
            for (int i = 0; i < node.Items.Count; i++)
            {
                list.Add(Value(element, isReference, node.Items[i], Index(path, i)));
            }
            return list;
        }

        private object Record(Type type, JsonNode node, string path)
        {
            if (node.Kind == JsonKind.Null && !type.IsValueType) return null;
            ExpectObject(node, path, $"an object with the fields of {type.Name}");

            object record;
            try
            {
                record = Activator.CreateInstance(type, nonPublic: true);
            }
            catch (MissingMethodException)
            {
                throw Error(node, path, $"{type.Name} can't be read from JSON: it needs a parameterless constructor");
            }

            // A struct is boxed here, so setting its fields changes the box that is returned.
            ReadFields(record, Fields(type, node, path), node, path, type.Name);
            return record;
        }

        private static AnimationCurve Curve(JsonNode node, string path)
        {
            if (node.Kind == JsonKind.Null) return null;
            ExpectObject(node, path, "a curve, e.g. { \"keys\": [{ \"time\": 0, \"value\": 0 }, { \"time\": 1, \"value\": 1 }] }");
            CheckDuplicates(node, path);

            var curve = new AnimationCurve();
            foreach (var property in node.Properties)
            {
                string at = Child(path, property.Key);
                switch (Match(property.Key, new[] { "keys", "preWrapMode", "postWrapMode" }))
                {
                    case "keys":
                        var keys = new List<Keyframe>();
                        foreach (var (item, itemPath) in Array(property.Value, at, "an array of keys"))
                        {
                            keys.Add(Keyframe(item, itemPath));
                        }
                        curve.keys = keys.ToArray();
                        break;
                    case "preWrapMode":
                        curve.preWrapMode = (WrapMode)Enum(typeof(WrapMode), property.Value, at);
                        break;
                    case "postWrapMode":
                        curve.postWrapMode = (WrapMode)Enum(typeof(WrapMode), property.Value, at);
                        break;
                    default:
                        throw UnknownProperty(property, at, new[] { "keys", "preWrapMode", "postWrapMode" }, "");
                }
            }
            return curve;
        }

        private static Keyframe Keyframe(JsonNode node, string path)
        {
            var names = new[] { "time", "value", "inTangent", "outTangent", "inWeight", "outWeight", "weightedMode" };
            ExpectObject(node, path, "a key, e.g. { \"time\": 0, \"value\": 1 }");
            CheckDuplicates(node, path);

            var key = new Keyframe();
            foreach (var property in node.Properties)
            {
                string at = Child(path, property.Key);
                switch (Match(property.Key, names))
                {
                    case "time": key.time = Float(property.Value, at); break;
                    case "value": key.value = Float(property.Value, at); break;
                    case "inTangent": key.inTangent = Float(property.Value, at); break;
                    case "outTangent": key.outTangent = Float(property.Value, at); break;
                    case "inWeight": key.inWeight = Float(property.Value, at); break;
                    case "outWeight": key.outWeight = Float(property.Value, at); break;
                    case "weightedMode": key.weightedMode = (WeightedMode)Enum(typeof(WeightedMode), property.Value, at); break;
                    default: throw UnknownProperty(property, at, names, "");
                }
            }
            return key;
        }

        // ---------------------------------------------------------------- Conditions

        private StatBlockCondition Condition(JsonNode node, string path)
        {
            ExpectObject(node, path, "a condition, e.g. { \"hasTag\": \"Equipped\" }");
            CheckDuplicates(node, path);
            if (node.Properties.Count == 0) return StatBlockCondition.Always();

            string kind = null;
            JsonNode kindValue = null;
            string kindPath = null;
            JsonNode tolerance = null;

            foreach (var property in node.Properties)
            {
                string at = Child(path, property.Key);
                if (Match(property.Key, new[] { "tolerance" }) != null)
                {
                    tolerance = property.Value;
                    continue;
                }

                string match = Match(property.Key, ConditionKinds);
                if (match == null)
                {
                    throw NameError(property, at,
                        $"unknown property '{property.Key}': a condition is one of {string.Join(", ", ConditionKinds)}, or {{}} for always");
                }
                if (kind != null)
                {
                    throw NameError(property, at, $"a condition is one of {string.Join(", ", ConditionKinds)}, but this one has both " +
                                                  $"'{kind}' and '{property.Key}'. Combine conditions with \"all\" or \"any\"");
                }
                kind = match;
                kindValue = property.Value;
                kindPath = at;
            }

            if (kind == null) throw Error(node, path, $"a condition needs one of {string.Join(", ", ConditionKinds)}");
            if (tolerance != null && kind != "compare") throw Error(tolerance, Child(path, "tolerance"), "\"tolerance\" only applies to \"compare\"");

            switch (kind)
            {
                case "hasTag":
                case "lacksTag":
                    var tag = Reference(kindValue, kindPath, TagExample);
                    return kind == "hasTag"
                        ? StatBlockCondition.HasTag(tag.Name, tag.Path.ToArray())
                        : StatBlockCondition.LacksTag(tag.Name, tag.Path.ToArray());

                case "compare":
                    if (kindValue.Kind != JsonKind.Array || kindValue.Items.Count != 3)
                    {
                        throw Error(kindValue, kindPath, $"expected [value, operator, value], e.g. [\"Health\", \"<\", 50], found {kindValue.Describe()}");
                    }
                    return StatBlockCondition.Compare(
                        ValueSource(kindValue.Items[0], Index(kindPath, 0)),
                        Comparison(kindValue.Items[1], Index(kindPath, 1)),
                        ValueSource(kindValue.Items[2], Index(kindPath, 2)),
                        tolerance != null ? Float(tolerance, Child(path, "tolerance")) : StatBlockCondition.DefaultTolerance);

                default:
                    var conditions = Array(kindValue, kindPath, "an array of conditions")
                        .Select(item => Condition(item.node, item.path))
                        .ToArray();
                    return kind == "all" ? StatBlockCondition.All(conditions) : StatBlockCondition.Any(conditions);
            }
        }

        private static readonly string[] ComparisonSymbols = { "==", "!=", ">", "<", ">=", "<=" };

        private static readonly StatBlockCondition.Comparison[] ComparisonValues =
        {
            StatBlockCondition.Comparison.Equal, StatBlockCondition.Comparison.NotEqual,
            StatBlockCondition.Comparison.Greater, StatBlockCondition.Comparison.Less,
            StatBlockCondition.Comparison.GreaterOrEqual, StatBlockCondition.Comparison.LessOrEqual
        };

        internal static string Symbol(StatBlockCondition.Comparison comparison)
        {
            int index = System.Array.IndexOf(ComparisonValues, comparison);
            return index >= 0 ? ComparisonSymbols[index] : null;
        }

        private static StatBlockCondition.Comparison Comparison(JsonNode node, string path)
        {
            if (node.Kind == JsonKind.String)
            {
                int index = System.Array.IndexOf(ComparisonSymbols, node.Text.Trim());
                if (index >= 0) return ComparisonValues[index];

                foreach (var name in System.Enum.GetNames(typeof(StatBlockCondition.Comparison)))
                {
                    if (string.Equals(name, node.Text, StringComparison.OrdinalIgnoreCase))
                    {
                        return (StatBlockCondition.Comparison)System.Enum.Parse(typeof(StatBlockCondition.Comparison), name);
                    }
                }
            }
            throw Error(node, path, $"expected a comparison ({string.Join(", ", ComparisonSymbols)}), found {node.Describe()}");
        }

        // ---------------------------------------------------------------- Keys, paths and values

        /// <summary>A number (a constant), an attribute ("Owner/Strength") or a formula ({ "linear": { ... } }).</summary>
        private ValueSource ValueSource(JsonNode node, string path)
        {
            switch (node.Kind)
            {
                case JsonKind.Null:
                    return null;
                case JsonKind.Number:
                    return Core.ValueSource.Const(Float(node, path));
                case JsonKind.String:
                    var reference = Reference(node, path, AttributeExample);
                    return new ValueSource { Mode = Core.ValueSource.SourceMode.Attribute, AttributeRef = reference };
                case JsonKind.Object:
                    if (node.Properties.Count != 1)
                    {
                        throw Error(node, path, $"a formula is one logic, e.g. {FormulaExample}, found {node.Describe()}");
                    }
                    return Core.ValueSource.From(LogicReference(typeof(ModifierLogic), node, path));
                default:
                    throw Error(node, path, $"expected a number, {AttributeExample}, or a formula such as {FormulaExample}, found {node.Describe()}");
            }
        }

        /// <summary>"Owner/Strength": the key names of the provider path, then the attribute (or tag). Null is no key.</summary>
        private AttributeReference Reference(JsonNode node, string path, string expected)
        {
            if (node.Kind == JsonKind.Null) return new AttributeReference(SemanticKey.None);
            if (node.Kind != JsonKind.String) throw Error(node, path, $"expected {expected}, found {node.Describe()}");
            return ParsePath(node.Text, node, path, isName: false);
        }

        /// <summary>
        /// The path in <paramref name="text"/>: a value, or a property name (<paramref name="isName"/>) such as the
        /// "Source/Mana" of a cost. In an effect, its first step is a role: Source or Target.
        /// </summary>
        private AttributeReference ParsePath(string text, JsonNode at, string path, bool isName)
        {
            JsonFormatException PathError(string message) =>
                isName && at.NameLine > 0 ? new JsonFormatException($"{path}: {message}", at.NameLine, at.NameColumn) : Error(at, path, message);

            var steps = text.Split('/');
            var keys = new List<SemanticKey>(steps.Length);
            for (int i = 0; i < steps.Length; i++)
            {
                string name = steps[i].Trim();
                if (name.Length == 0) throw PathError($"\"{text}\" has an empty step: a path is key names separated by '/'");

                if (_roles && i == 0)
                {
                    var role = EffectRoles.Find(name);
                    if (role == SemanticKey.None || steps.Length < 2)
                    {
                        throw PathError($"in an effect, \"{text}\" must start with Source or Target, the entity it is on: " +
                                        $"e.g. \"Target/{(role == SemanticKey.None ? name : "Health")}\"");
                    }
                    keys.Add(role);
                    continue;
                }
                keys.Add(_keys.Resolve(name, at, path));
            }

            var target = keys[keys.Count - 1];
            keys.RemoveAt(keys.Count - 1);
            return new AttributeReference(target, keys);
        }

        /// <summary>A key on its own (not a path). Null is no key.</summary>
        private SemanticKey Key(JsonNode node, string path)
        {
            if (node.Kind == JsonKind.Null) return SemanticKey.None;
            if (node.Kind != JsonKind.String) throw Error(node, path, $"expected a key name, found {node.Describe()}");
            return KeyName(node.Text, node, path);
        }

        /// <summary>A key name, e.g. a property name in "baseValues".</summary>
        private SemanticKey KeyName(string name, JsonNode at, string path)
        {
            if (name.Contains("/")) throw Error(at, path, $"expected a key name, found the path \"{name}\"");
            string trimmed = name.Trim();
            if (trimmed.Length == 0) throw Error(at, path, "expected a key name, found an empty name");
            return _keys.Resolve(trimmed, at, path);
        }

        private static string String(JsonNode node, string path, string expected = "text in double quotes")
        {
            if (node.Kind == JsonKind.Null) return null;
            if (node.Kind != JsonKind.String) throw Error(node, path, $"expected {expected}, found {node.Describe()}");
            return node.Text;
        }

        private static char Char(JsonNode node, string path)
        {
            if (node.Kind != JsonKind.String || node.Text.Length != 1) throw Error(node, path, $"expected a single character in double quotes, found {node.Describe()}");
            return node.Text[0];
        }

        private static bool Bool(JsonNode node, string path)
        {
            if (node.Kind != JsonKind.Bool) throw Error(node, path, $"expected true or false, found {node.Describe()}");
            return node.BoolValue;
        }

        private static float Float(JsonNode node, string path)
        {
            if (node.Kind == JsonKind.String && TryParseSpecial(node.Text, out double special)) return (float)special;
            if (node.Kind != JsonKind.Number) throw Error(node, path, $"expected a number, found {node.Describe()}");

            float value;
            try
            {
                value = float.Parse(node.Text, NumberStyles.Float, CultureInfo.InvariantCulture);
            }
            catch (OverflowException)
            {
                value = float.PositiveInfinity;
            }
            if (float.IsInfinity(value)) throw Error(node, path, $"{node.Text} is too large for a float");
            return value;
        }

        private static double Double(JsonNode node, string path)
        {
            if (node.Kind == JsonKind.String && TryParseSpecial(node.Text, out double special)) return special;
            if (node.Kind != JsonKind.Number) throw Error(node, path, $"expected a number, found {node.Describe()}");
            return node.NumberValue;
        }

        /// <summary>NaN and infinities, which JSON numbers can't hold, are written as strings.</summary>
        private static bool TryParseSpecial(string text, out double value)
        {
            switch (text)
            {
                case "NaN": value = double.NaN; return true;
                case "Infinity": value = double.PositiveInfinity; return true;
                case "-Infinity": value = double.NegativeInfinity; return true;
                default: value = 0; return false;
            }
        }

        private static object Integer(JsonNode node, string path, Type type)
        {
            if (node.Kind != JsonKind.Number) throw Error(node, path, $"expected a whole number, found {node.Describe()}");

            try
            {
                decimal value = decimal.Parse(node.Text, NumberStyles.Float, CultureInfo.InvariantCulture);
                if (value != decimal.Truncate(value)) throw Error(node, path, $"expected a whole number, found {node.Text}");
                return Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
            }
            catch (OverflowException)
            {
                throw Error(node, path, $"{node.Text} is out of range for {type.Name}");
            }
        }

        private static object Enum(Type type, JsonNode node, string path)
        {
            var names = System.Enum.GetNames(type);
            if (node.Kind == JsonKind.String)
            {
                bool isFlags = type.IsDefined(typeof(FlagsAttribute), false);
                var parts = isFlags ? node.Text.Split(',') : new[] { node.Text };
                var matched = new List<string>();
                foreach (var part in parts)
                {
                    string name = names.FirstOrDefault(n => string.Equals(n, part.Trim(), StringComparison.OrdinalIgnoreCase));
                    if (name == null) break;
                    matched.Add(name);
                }
                if (matched.Count == parts.Length) return System.Enum.Parse(type, string.Join(", ", matched));
            }
            throw Error(node, path, $"expected one of {string.Join(", ", names)}, found {node.Describe()}");
        }

        // ---------------------------------------------------------------- Structure

        private static IEnumerable<(JsonNode node, string path)> Array(JsonNode node, string path, string expected)
        {
            if (node.Kind == JsonKind.Null) return Enumerable.Empty<(JsonNode, string)>();
            if (node.Kind != JsonKind.Array) throw Error(node, path, $"expected {expected}, found {node.Describe()}");
            return node.Items.Select((item, i) => (item, Index(path, i))).ToList();
        }

        private static List<KeyValuePair<string, JsonNode>> Map(JsonNode node, string path, string expected)
        {
            if (node.Kind == JsonKind.Null) return new List<KeyValuePair<string, JsonNode>>();
            if (node.Kind != JsonKind.Object) throw Error(node, path, $"expected {expected}, found {node.Describe()}");
            return node.Properties;
        }

        private static void ExpectObject(JsonNode node, string path, string expected)
        {
            if (node.Kind != JsonKind.Object) throw Error(node, path, $"expected {expected} ({{ ... }}), found {node.Describe()}");
        }

        private static void CheckDuplicates(JsonNode node, string path)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in node.Properties)
            {
                if (!seen.Add(property.Key)) throw NameError(property, Child(path, property.Key), $"'{property.Key}' appears twice");
            }
        }

        /// <summary>The name in <paramref name="names"/> that <paramref name="name"/> is (ignoring case), or null.</summary>
        private static string Match(string name, string[] names) =>
            names.FirstOrDefault(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));

        private static JsonFormatException UnknownProperty(KeyValuePair<string, JsonNode> property, string path, string[] names, string hint) =>
            NameError(property, path, $"unknown property '{property.Key}'{hint}. The properties here are: {string.Join(", ", names)}");

        internal static string Child(string path, string name) => path.Length == 0 ? name : path + "." + name;

        private static string Index(string path, int index) => $"{path}[{index}]";

        internal static JsonFormatException Error(JsonNode at, string path, string message) =>
            new JsonFormatException($"{(path.Length == 0 ? "The file" : path)}: {message}", at?.Line ?? 0, at?.Column ?? 0);

        /// <summary>An error about a property's name (e.g. a typo): it points at the name rather than the value.</summary>
        private static JsonFormatException NameError(KeyValuePair<string, JsonNode> property, string path, string message) =>
            property.Value.NameLine > 0
                ? new JsonFormatException($"{path}: {message}", property.Value.NameLine, property.Value.NameColumn)
                : Error(property.Value, path, message);
    }
}
