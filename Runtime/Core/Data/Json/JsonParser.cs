using System.Globalization;
using System.Text;

namespace ReactiveSolutions.AttributeSystem.Core.Data.Json
{
    /// <summary>
    /// Parses standard JSON (RFC 8259) into <see cref="JsonNode"/>s. Errors are JsonFormatExceptions with the line
    /// and column, and a hint for common hand-editing mistakes (trailing commas, comments, single quotes).
    /// </summary>
    internal static class JsonParser
    {
        private const int MaxDepth = 128;

        public static JsonNode Parse(string text) => new Parser(text ?? string.Empty).ParseDocument();

        private sealed class Parser
        {
            private readonly string _text;
            private int _pos;
            private int _line = 1;
            private int _lineStart;

            public Parser(string text)
            {
                _text = text;
            }

            private int Column => _pos - _lineStart + 1;
            private bool AtEnd => _pos >= _text.Length;

            public JsonNode ParseDocument()
            {
                // A byte order mark can survive decoding.
                if (_text.Length > 0 && _text[0] == (char)0xFEFF)
                {
                    _pos = 1;
                    _lineStart = 1;
                }

                SkipWhitespace();
                if (AtEnd) throw Error("the text is empty");

                var root = ParseValue(0);

                SkipWhitespace();
                if (!AtEnd) throw Error($"unexpected {DescribeNext()} after the end of the JSON");
                return root;
            }

            private JsonNode ParseValue(int depth)
            {
                if (depth > MaxDepth) throw Error("the JSON is nested too deeply");

                int line = _line;
                int column = Column;
                JsonNode node;

                char c = AtEnd ? '\0' : _text[_pos];
                switch (c)
                {
                    case '{': node = ParseObject(depth); break;
                    case '[': node = ParseArray(depth); break;
                    case '"': node = JsonNode.From(ParseString()); break;
                    case 't': ExpectWord("true"); node = JsonNode.From(true); break;
                    case 'f': ExpectWord("false"); node = JsonNode.From(false); break;
                    case 'n': ExpectWord("null"); node = JsonNode.NewNull(); break;
                    default:
                        if (c == '-' || (c >= '0' && c <= '9'))
                        {
                            node = ParseNumber();
                            break;
                        }
                        throw Error($"expected a value, found {DescribeNext()}");
                }

                node.Line = line;
                node.Column = column;
                return node;
            }

            private JsonNode ParseObject(int depth)
            {
                var node = JsonNode.NewObject();
                _pos++; // {
                SkipWhitespace();
                if (TryConsume('}')) return node;

                while (true)
                {
                    SkipWhitespace();
                    if (AtEnd || _text[_pos] != '"')
                    {
                        bool afterComma = node.Properties.Count > 0;
                        throw Error($"expected a property name in double quotes, found {DescribeNext()}" +
                                    (afterComma && !AtEnd && _text[_pos] == '}' ? " (JSON doesn't allow a comma after the last property)" : ""));
                    }

                    int nameLine = _line;
                    int nameColumn = Column;
                    string name = ParseString();
                    SkipWhitespace();
                    if (!TryConsume(':')) throw Error($"expected ':' after \"{name}\", found {DescribeNext()}");

                    SkipWhitespace();
                    var value = ParseValue(depth + 1);
                    value.NameLine = nameLine;
                    value.NameColumn = nameColumn;
                    node.Add(name, value);

                    SkipWhitespace();
                    if (TryConsume(',')) continue;
                    if (TryConsume('}')) return node;
                    throw Error($"expected ',' or '}}' after the value of \"{name}\", found {DescribeNext()}");
                }
            }

            private JsonNode ParseArray(int depth)
            {
                var node = JsonNode.NewArray();
                _pos++; // [
                SkipWhitespace();
                if (TryConsume(']')) return node;

                while (true)
                {
                    SkipWhitespace();
                    if (!AtEnd && _text[_pos] == ']')
                    {
                        throw Error("expected a value, found ']' (JSON doesn't allow a comma after the last item)");
                    }

                    node.Add(ParseValue(depth + 1));

                    SkipWhitespace();
                    if (TryConsume(',')) continue;
                    if (TryConsume(']')) return node;
                    throw Error($"expected ',' or ']' after an array item, found {DescribeNext()}");
                }
            }

            private string ParseString()
            {
                int line = _line;
                int column = Column;
                _pos++; // opening quote
                var sb = new StringBuilder();

                while (true)
                {
                    if (AtEnd) throw new JsonFormatException("Invalid JSON: a string is missing its closing '\"'", line, column);

                    char c = _text[_pos++];
                    if (c == '"') return sb.ToString();

                    if (c == '\\')
                    {
                        if (AtEnd) continue; // reported as a missing closing quote
                        char e = _text[_pos++];
                        switch (e)
                        {
                            case '"': sb.Append('"'); break;
                            case '\\': sb.Append('\\'); break;
                            case '/': sb.Append('/'); break;
                            case 'b': sb.Append('\b'); break;
                            case 'f': sb.Append('\f'); break;
                            case 'n': sb.Append('\n'); break;
                            case 'r': sb.Append('\r'); break;
                            case 't': sb.Append('\t'); break;
                            case 'u': sb.Append(ParseHexChar()); break;
                            default:
                                _pos--;
                                throw Error($"'\\{e}' isn't a valid escape sequence (use \\\\ for a backslash)");
                        }
                        continue;
                    }

                    if (c < ' ')
                    {
                        _pos--;
                        throw Error(c == '\n' || c == '\r'
                            ? "a string can't continue on the next line (a missing closing '\"'?)"
                            : "a string can't contain control characters (escape them, e.g. \\t)");
                    }

                    sb.Append(c);
                }
            }

            private char ParseHexChar()
            {
                if (_pos + 4 > _text.Length) throw Error("expected 4 hex digits after \\u");
                int value = 0;
                for (int i = 0; i < 4; i++)
                {
                    char h = _text[_pos + i];
                    int digit = h >= '0' && h <= '9' ? h - '0'
                        : h >= 'a' && h <= 'f' ? h - 'a' + 10
                        : h >= 'A' && h <= 'F' ? h - 'A' + 10
                        : -1;
                    if (digit < 0) throw Error("expected 4 hex digits after \\u");
                    value = value * 16 + digit;
                }
                _pos += 4;
                return (char)value;
            }

            private JsonNode ParseNumber()
            {
                int start = _pos;
                TryConsume('-');

                if (TryConsume('0'))
                {
                    if (!AtEnd && char.IsDigit(_text[_pos])) throw Error("a number can't have leading zeros");
                }
                else if (!ConsumeDigits())
                {
                    throw Error("expected a digit");
                }

                if (TryConsume('.') && !ConsumeDigits()) throw Error("expected a digit after the decimal point");

                if (!AtEnd && (_text[_pos] == 'e' || _text[_pos] == 'E'))
                {
                    _pos++;
                    if (!TryConsume('+')) TryConsume('-');
                    if (!ConsumeDigits()) throw Error("expected a digit in the exponent");
                }

                string text = _text.Substring(start, _pos - start);
                if (double.IsInfinity(double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture)))
                {
                    _pos = start;
                    throw Error($"the number {text} is too large");
                }
                return JsonNode.Number(text);
            }

            private bool ConsumeDigits()
            {
                int start = _pos;
                while (!AtEnd && _text[_pos] >= '0' && _text[_pos] <= '9') _pos++;
                return _pos > start;
            }

            private void ExpectWord(string word)
            {
                if (string.CompareOrdinal(_text, _pos, word, 0, word.Length) != 0 ||
                    (_pos + word.Length < _text.Length && char.IsLetterOrDigit(_text[_pos + word.Length])))
                {
                    throw Error($"expected a value, found {DescribeNext()}");
                }
                _pos += word.Length;
            }

            private bool TryConsume(char c)
            {
                if (AtEnd || _text[_pos] != c) return false;
                _pos++;
                return true;
            }

            private void SkipWhitespace()
            {
                while (!AtEnd)
                {
                    char c = _text[_pos];
                    if (c == '\n')
                    {
                        _pos++;
                        _line++;
                        _lineStart = _pos;
                    }
                    else if (c == ' ' || c == '\t' || c == '\r')
                    {
                        _pos++;
                    }
                    else
                    {
                        return;
                    }
                }
            }

            private string DescribeNext()
            {
                if (AtEnd) return "the end of the text";

                char c = _text[_pos];
                if (c == '/') return "'/' (JSON doesn't allow comments)";
                if (c == '\'') return "a single quote (JSON strings use double quotes)";
                if (char.IsLetter(c) || c == '_')
                {
                    int end = _pos;
                    while (end < _text.Length && (char.IsLetterOrDigit(_text[end]) || _text[end] == '_')) end++;
                    string word = _text.Substring(_pos, end - _pos);
                    return $"'{word}' (text must be in double quotes)";
                }
                return $"'{c}'";
            }

            private JsonFormatException Error(string message) =>
                new JsonFormatException("Invalid JSON: " + message, _line, Column);
        }
    }
}
