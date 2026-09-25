using System.Text;

namespace ReactiveSolutions.AttributeSystem.Core.Data.Json
{
    /// <summary>
    /// Writes <see cref="JsonNode"/>s as indented JSON. An array or object that fits in <see cref="MaxLineLength"/>
    /// characters stays on one line (e.g. <c>{ "target": "Damage", "value": 5 }</c>), unless it is marked Expanded.
    /// </summary>
    internal static class JsonWriter
    {
        public const int MaxLineLength = 100;
        private const string Indent = "  ";

        public static string Write(JsonNode root)
        {
            var sb = new StringBuilder();
            WriteNode(sb, root, 0, 0);
            sb.Append('\n');
            return sb.ToString();
        }

        /// <param name="lineUsed">The characters already on the line (indentation and property name).</param>
        private static void WriteNode(StringBuilder sb, JsonNode node, int depth, int lineUsed)
        {
            bool isObject = node.Kind == JsonKind.Object;
            if (!isObject && node.Kind != JsonKind.Array)
            {
                WriteScalar(sb, node);
                return;
            }

            int count = isObject ? node.Properties.Count : node.Items.Count;
            if (count == 0)
            {
                sb.Append(isObject ? "{}" : "[]");
                return;
            }

            if (!node.Expanded)
            {
                string inline = Inline(node);
                // + 1 for a comma after the value
                if (inline != null && lineUsed + inline.Length + 1 <= MaxLineLength)
                {
                    sb.Append(inline);
                    return;
                }
            }

            string childIndent = Repeat(depth + 1);
            sb.Append(isObject ? '{' : '[').Append('\n');
            for (int i = 0; i < count; i++)
            {
                sb.Append(childIndent);
                int used = childIndent.Length;

                JsonNode child;
                if (isObject)
                {
                    int before = sb.Length;
                    WriteString(sb, node.Properties[i].Key);
                    sb.Append(": ");
                    used += sb.Length - before;
                    child = node.Properties[i].Value;
                }
                else
                {
                    child = node.Items[i];
                }

                WriteNode(sb, child, depth + 1, used);
                if (i < count - 1) sb.Append(',');
                sb.Append('\n');
            }
            sb.Append(Repeat(depth)).Append(isObject ? '}' : ']');
        }

        /// <summary>The value on one line, or null if it holds an Expanded array or object.</summary>
        private static string Inline(JsonNode node)
        {
            var sb = new StringBuilder();
            return AppendInline(sb, node) ? sb.ToString() : null;
        }

        private static bool AppendInline(StringBuilder sb, JsonNode node)
        {
            switch (node.Kind)
            {
                case JsonKind.Array:
                    if (node.Items.Count == 0)
                    {
                        sb.Append("[]");
                        return true;
                    }
                    if (node.Expanded) return false;

                    sb.Append('[');
                    for (int i = 0; i < node.Items.Count; i++)
                    {
                        if (i > 0) sb.Append(", ");
                        if (!AppendInline(sb, node.Items[i])) return false;
                    }
                    sb.Append(']');
                    return true;

                case JsonKind.Object:
                    if (node.Properties.Count == 0)
                    {
                        sb.Append("{}");
                        return true;
                    }
                    if (node.Expanded) return false;

                    sb.Append("{ ");
                    for (int i = 0; i < node.Properties.Count; i++)
                    {
                        if (i > 0) sb.Append(", ");
                        WriteString(sb, node.Properties[i].Key);
                        sb.Append(": ");
                        if (!AppendInline(sb, node.Properties[i].Value)) return false;
                    }
                    sb.Append(" }");
                    return true;

                default:
                    WriteScalar(sb, node);
                    return true;
            }
        }

        private static void WriteScalar(StringBuilder sb, JsonNode node)
        {
            switch (node.Kind)
            {
                case JsonKind.Null: sb.Append("null"); break;
                case JsonKind.Bool: sb.Append(node.BoolValue ? "true" : "false"); break;
                case JsonKind.Number: sb.Append(node.Text); break;
                default: WriteString(sb, node.Text); break;
            }
        }

        private static void WriteString(StringBuilder sb, string text)
        {
            sb.Append('"');
            foreach (char c in text)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        // Control characters, and the line and paragraph separators (which break JavaScript).
                        if (c < ' ' || c == (char)0x2028 || c == (char)0x2029)
                        {
                            sb.Append("\\u").Append(((int)c).ToString("x4"));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            sb.Append('"');
        }

        private static string Repeat(int depth)
        {
            var sb = new StringBuilder(depth * Indent.Length);
            for (int i = 0; i < depth; i++) sb.Append(Indent);
            return sb.ToString();
        }
    }
}
