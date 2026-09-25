using System;
using System.Collections.Generic;
using System.Globalization;

namespace ReactiveSolutions.AttributeSystem.Core.Data.Json
{
    internal enum JsonKind { Null, Bool, Number, String, Array, Object }

    /// <summary>
    /// A JSON value. Objects keep their properties in the order they were written (duplicates included), so
    /// files are read in the order they are written in.
    /// </summary>
    internal sealed class JsonNode
    {
        public JsonKind Kind { get; private set; }
        public bool BoolValue { get; private set; }

        /// <summary>A string's text, or a number as written (e.g. "0.5").</summary>
        public string Text { get; private set; }

        public List<JsonNode> Items { get; private set; }
        public List<KeyValuePair<string, JsonNode>> Properties { get; private set; }

        /// <summary>Where the value starts in the parsed text (1-based), or 0 for a value built in code.</summary>
        public int Line { get; set; }
        public int Column { get; set; }

        /// <summary>For a property's value: where the property's name starts, or 0.</summary>
        public int NameLine { get; set; }
        public int NameColumn { get; set; }

        /// <summary>For the writer: one item per line, even if they would fit on one.</summary>
        public bool Expanded { get; set; }

        private JsonNode() { }

        public static JsonNode NewNull() => new JsonNode { Kind = JsonKind.Null };
        public static JsonNode NewArray() => new JsonNode { Kind = JsonKind.Array, Items = new List<JsonNode>() };
        public static JsonNode NewObject() => new JsonNode { Kind = JsonKind.Object, Properties = new List<KeyValuePair<string, JsonNode>>() };

        public static JsonNode From(bool value) => new JsonNode { Kind = JsonKind.Bool, BoolValue = value };
        public static JsonNode From(string value) => value == null ? NewNull() : new JsonNode { Kind = JsonKind.String, Text = value };
        public static JsonNode From(long value) => Number(value.ToString(CultureInfo.InvariantCulture));
        public static JsonNode From(ulong value) => Number(value.ToString(CultureInfo.InvariantCulture));

        /// <summary>The shortest text that reads back as the same float. NaN and infinities, which JSON can't hold, become strings.</summary>
        public static JsonNode From(float value) =>
            float.IsNaN(value) || float.IsInfinity(value)
                ? From(value.ToString(CultureInfo.InvariantCulture))
                : Number(value.ToString("R", CultureInfo.InvariantCulture));

        public static JsonNode From(double value) =>
            double.IsNaN(value) || double.IsInfinity(value)
                ? From(value.ToString(CultureInfo.InvariantCulture))
                : Number(value.ToString("R", CultureInfo.InvariantCulture));

        /// <summary>A number from its JSON text (e.g. "-1.5e3"), which the parser has already validated.</summary>
        public static JsonNode Number(string text) => new JsonNode { Kind = JsonKind.Number, Text = text };

        public double NumberValue => double.Parse(Text, NumberStyles.Float, CultureInfo.InvariantCulture);

        /// <summary>Adds an item to an array.</summary>
        public JsonNode Add(JsonNode item)
        {
            Items.Add(item);
            return this;
        }

        /// <summary>Adds a property to an object.</summary>
        public JsonNode Add(string name, JsonNode value)
        {
            Properties.Add(new KeyValuePair<string, JsonNode>(name, value));
            return this;
        }

        /// <summary>The first property named <paramref name="name"/> (ignoring case), or null.</summary>
        public JsonNode Find(string name)
        {
            foreach (var property in Properties)
            {
                if (string.Equals(property.Key, name, StringComparison.OrdinalIgnoreCase)) return property.Value;
            }
            return null;
        }

        /// <summary>The same value: numbers compare by value, objects by their properties in order.</summary>
        public static bool DeepEquals(JsonNode a, JsonNode b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null || a.Kind != b.Kind) return false;

            switch (a.Kind)
            {
                case JsonKind.Null: return true;
                case JsonKind.Bool: return a.BoolValue == b.BoolValue;
                case JsonKind.Number: return a.Text == b.Text || a.NumberValue.Equals(b.NumberValue);
                case JsonKind.String: return string.Equals(a.Text, b.Text, StringComparison.Ordinal);
                case JsonKind.Array:
                    if (a.Items.Count != b.Items.Count) return false;
                    for (int i = 0; i < a.Items.Count; i++)
                    {
                        if (!DeepEquals(a.Items[i], b.Items[i])) return false;
                    }
                    return true;
                default:
                    if (a.Properties.Count != b.Properties.Count) return false;
                    for (int i = 0; i < a.Properties.Count; i++)
                    {
                        if (!string.Equals(a.Properties[i].Key, b.Properties[i].Key, StringComparison.Ordinal)) return false;
                        if (!DeepEquals(a.Properties[i].Value, b.Properties[i].Value)) return false;
                    }
                    return true;
            }
        }

        /// <summary>Describes the value for error messages: "true", "a string", "an object", ...</summary>
        public string Describe()
        {
            switch (Kind)
            {
                case JsonKind.Null: return "null";
                case JsonKind.Bool: return BoolValue ? "true" : "false";
                case JsonKind.Number: return "the number " + Text;
                case JsonKind.String: return "\"" + Text + "\"";
                case JsonKind.Array: return "an array";
                default: return "an object";
            }
        }
    }
}
