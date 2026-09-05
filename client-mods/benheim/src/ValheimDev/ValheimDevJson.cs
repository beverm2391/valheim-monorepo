using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace BenheimQoL.ValheimDev;

internal static class ValheimDevJson
{
    internal static bool TryParseObject(
        string json,
        out Dictionary<string, object?> value,
        out string error)
    {
        try
        {
            Parser parser = new Parser(json);
            object? parsed = parser.ParseValue();
            parser.SkipWhitespace();
            if (!parser.AtEnd || parsed is not Dictionary<string, object?> dictionary)
            {
                throw new FormatException("root must be one JSON object");
            }
            value = dictionary;
            error = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            value = new Dictionary<string, object?>();
            error = exception.Message;
            return false;
        }
    }

    internal static void AppendProperty(StringBuilder builder, string name, string value)
    {
        AppendString(builder, name);
        builder.Append(':');
        AppendString(builder, value);
    }

    internal static void AppendProperty(StringBuilder builder, string name, int value)
    {
        AppendString(builder, name);
        builder.Append(':').Append(value.ToString(CultureInfo.InvariantCulture));
    }

    internal static void AppendProperty(StringBuilder builder, string name, bool value)
    {
        AppendString(builder, name);
        builder.Append(value ? ":true" : ":false");
    }

    internal static void AppendNullableProperty(StringBuilder builder, string name, string? value)
    {
        AppendString(builder, name);
        builder.Append(':');
        if (value == null) builder.Append("null");
        else AppendString(builder, value);
    }

    internal static void AppendStringArrayProperty(
        StringBuilder builder,
        string name,
        IEnumerable<string> values)
    {
        AppendString(builder, name);
        builder.Append(":[");
        bool first = true;
        foreach (string value in values)
        {
            if (!first) builder.Append(',');
            AppendString(builder, value);
            first = false;
        }
        builder.Append(']');
    }

    internal static void AppendString(StringBuilder builder, string value)
    {
        builder.Append('"');
        foreach (char character in value)
        {
            switch (character)
            {
                case '"': builder.Append("\\\""); break;
                case '\\': builder.Append("\\\\"); break;
                case '\b': builder.Append("\\b"); break;
                case '\f': builder.Append("\\f"); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;
                default:
                    if (character < 0x20)
                    {
                        builder.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else builder.Append(character);
                    break;
            }
        }
        builder.Append('"');
    }

    internal static int EncodedStringUtf8ByteCount(string value)
    {
        StringBuilder builder = new StringBuilder(value.Length + 2);
        AppendString(builder, value);
        return Encoding.UTF8.GetByteCount(builder.ToString());
    }

    private sealed class Parser
    {
        private readonly string source;
        private int index;

        internal Parser(string source) => this.source = source;
        internal bool AtEnd => index == source.Length;

        internal object? ParseValue(int containerDepth = 0)
        {
            SkipWhitespace();
            if (AtEnd) throw new FormatException("unexpected end of JSON");
            return source[index] switch
            {
                '{' => ParseObject(containerDepth + 1),
                '[' => ParseArray(containerDepth + 1),
                '"' => ParseString(),
                't' => ParseLiteral("true", true),
                'f' => ParseLiteral("false", false),
                'n' => ParseLiteral("null", null),
                _ => ParseNumber()
            };
        }

        internal void SkipWhitespace()
        {
            while (!AtEnd
                && (source[index] == ' '
                    || source[index] == '\t'
                    || source[index] == '\r'
                    || source[index] == '\n'))
            {
                index++;
            }
        }

        private Dictionary<string, object?> ParseObject(int containerDepth)
        {
            RequireDepth(containerDepth);
            Expect('{');
            Dictionary<string, object?> result = new Dictionary<string, object?>(StringComparer.Ordinal);
            SkipWhitespace();
            if (TryConsume('}')) return result;
            while (true)
            {
                SkipWhitespace();
                string key = ParseString();
                SkipWhitespace();
                Expect(':');
                if (!result.TryAdd(key, ParseValue(containerDepth)))
                {
                    throw new FormatException("duplicate property: " + key);
                }
                SkipWhitespace();
                if (TryConsume('}')) return result;
                Expect(',');
            }
        }

        private List<object?> ParseArray(int containerDepth)
        {
            RequireDepth(containerDepth);
            Expect('[');
            List<object?> result = new List<object?>();
            SkipWhitespace();
            if (TryConsume(']')) return result;
            while (true)
            {
                result.Add(ParseValue(containerDepth));
                SkipWhitespace();
                if (TryConsume(']')) return result;
                Expect(',');
            }
        }

        private string ParseString()
        {
            Expect('"');
            StringBuilder builder = new StringBuilder();
            while (!AtEnd)
            {
                char character = source[index++];
                if (character == '"') return builder.ToString();
                if (character < 0x20) throw new FormatException("control character in string");
                if (character != '\\')
                {
                    builder.Append(character);
                    continue;
                }
                if (AtEnd) throw new FormatException("unfinished escape");
                char escape = source[index++];
                switch (escape)
                {
                    case '"': builder.Append('"'); break;
                    case '\\': builder.Append('\\'); break;
                    case '/': builder.Append('/'); break;
                    case 'b': builder.Append('\b'); break;
                    case 'f': builder.Append('\f'); break;
                    case 'n': builder.Append('\n'); break;
                    case 'r': builder.Append('\r'); break;
                    case 't': builder.Append('\t'); break;
                    case 'u': builder.Append(ParseUnicode()); break;
                    default: throw new FormatException("invalid escape");
                }
            }
            throw new FormatException("unfinished string");
        }

        private char ParseUnicode()
        {
            if (index + 4 > source.Length) throw new FormatException("unfinished unicode escape");
            string digits = source.Substring(index, 4);
            index += 4;
            if (!ushort.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ushort value))
            {
                throw new FormatException("invalid unicode escape");
            }
            return (char)value;
        }

        private object? ParseLiteral(string literal, object? value)
        {
            if (index + literal.Length > source.Length
                || string.CompareOrdinal(source, index, literal, 0, literal.Length) != 0)
            {
                throw new FormatException("invalid literal");
            }
            index += literal.Length;
            return value;
        }

        private double ParseNumber()
        {
            int start = index;
            if (!AtEnd && source[index] == '-') index++;
            if (AtEnd) throw new FormatException("invalid number");
            if (source[index] == '0')
            {
                index++;
                if (!AtEnd && IsAsciiDigit(source[index]))
                {
                    throw new FormatException("leading zero in number");
                }
            }
            else if (source[index] >= '1' && source[index] <= '9')
            {
                while (!AtEnd && IsAsciiDigit(source[index])) index++;
            }
            else
            {
                throw new FormatException("invalid number");
            }
            if (!AtEnd && source[index] == '.')
            {
                index++;
                int fractionalStart = index;
                while (!AtEnd && IsAsciiDigit(source[index])) index++;
                if (index == fractionalStart) throw new FormatException("invalid fraction");
            }
            if (!AtEnd && (source[index] == 'e' || source[index] == 'E'))
            {
                index++;
                if (!AtEnd && (source[index] == '+' || source[index] == '-')) index++;
                int exponentStart = index;
                while (!AtEnd && IsAsciiDigit(source[index])) index++;
                if (index == exponentStart) throw new FormatException("invalid exponent");
            }
            string number = source.Substring(start, index - start);
            if (!double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out double result)
                || double.IsNaN(result)
                || double.IsInfinity(result))
            {
                throw new FormatException("invalid number");
            }
            return result;
        }

        private static bool IsAsciiDigit(char value) => value >= '0' && value <= '9';

        private static void RequireDepth(int containerDepth)
        {
            if (containerDepth > ValheimDevProtocol.MaximumJsonDepth)
            {
                throw new FormatException("JSON nesting exceeds maximum depth");
            }
        }

        private bool TryConsume(char expected)
        {
            if (!AtEnd && source[index] == expected)
            {
                index++;
                return true;
            }
            return false;
        }

        private void Expect(char expected)
        {
            SkipWhitespace();
            if (AtEnd || source[index++] != expected)
            {
                throw new FormatException("expected '" + expected + "'");
            }
        }
    }
}
