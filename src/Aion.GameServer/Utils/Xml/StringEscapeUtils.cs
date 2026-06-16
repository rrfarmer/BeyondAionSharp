using System.Globalization;
using System.Text;

namespace Aion.GameServer.Utils.Xml;

/// <summary>
/// Faithful port of Apache commons-lang3 org.apache.commons.lang3.StringEscapeUtils.unescapeJava
/// (and its UNESCAPE_JAVA aggregate translator: OctalUnescaper, UnicodeUnescaper, JAVA_CTRL_CHARS
/// lookup, and the \\, \", \', \ lookup). Mirrors the commons-lang3 source exactly.
/// </summary>
public static class StringEscapeUtils
{
    /// <summary>Apache commons-lang3 StringEscapeUtils.unescapeJava(String).</summary>
    public static string UnescapeJava(string input)
    {
        if (input == null)
            return null;
        StringBuilder writer = new StringBuilder(input.Length);
        int pos = 0;
        int len = input.Length;
        while (pos < len)
        {
            int consumed = Translate(input, pos, writer);
            if (consumed == 0)
            {
                // inlined CharSequenceTranslator: emit code point at pos, advance over it
                char c1 = input[pos];
                writer.Append(c1);
                pos++;
                if (char.IsHighSurrogate(c1) && pos < len)
                {
                    char c2 = input[pos];
                    if (char.IsLowSurrogate(c2))
                    {
                        writer.Append(c2);
                        pos++;
                    }
                }
                continue;
            }
            // all translators here report consumed input chars directly
            pos += consumed;
        }
        return writer.ToString();
    }

    // AggregateTranslator: returns the number of consumed chars from the first translator that consumes input.
    private static int Translate(string input, int index, StringBuilder outBuf)
    {
        int consumed = TranslateOctal(input, index, outBuf);
        if (consumed != 0)
            return consumed;
        consumed = TranslateUnicode(input, index, outBuf);
        if (consumed != 0)
            return consumed;
        consumed = TranslateLookup(input, index, outBuf);
        return consumed;
    }

    // org.apache.commons.lang3.text.translate.OctalUnescaper (longest possible valid octal escape)
    private static int TranslateOctal(string input, int index, StringBuilder outBuf)
    {
        int remaining = input.Length - index - 1; // chars after the \
        StringBuilder builder = new StringBuilder();
        if (input[index] == '\\' && remaining > 0 && IsOctalDigit(input[index + 1]))
        {
            int next = index + 1;
            int next1 = next + 1;
            int next2 = next + 2;
            builder.Append(input[next]);
            if (remaining > 1 && IsOctalDigit(input[next1]))
            {
                builder.Append(input[next1]);
                if (remaining > 2 && IsZeroToThree(input[next]) && IsOctalDigit(input[next2]))
                    builder.Append(input[next2]);
            }
            int value = System.Convert.ToInt32(builder.ToString(), 8);
            outBuf.Append((char)value);
            return 1 + builder.Length;
        }
        return 0;
    }

    // org.apache.commons.lang3.text.translate.UnicodeUnescaper
    private static int TranslateUnicode(string input, int index, StringBuilder outBuf)
    {
        if (input[index] == '\\' && index + 1 < input.Length && input[index + 1] == 'u')
        {
            // consume optional extra u chars
            int i = 2;
            while (index + i < input.Length && input[index + i] == 'u')
                i++;
            if (index + i < input.Length && input[index + i] == '+')
                i++;
            if (index + i + 4 <= input.Length)
            {
                string unicode = input.Substring(index + i, 4);
                int value = int.Parse(unicode, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                outBuf.Append((char)value);
                return i + 4;
            }
            throw new System.ArgumentException("Less than 4 hex digits in unicode value: '" + input.Substring(index)
                + "' due to end of CharSequence");
        }
        return 0;
    }

    // The two LookupTranslators: JAVA_CTRL_CHARS_UNESCAPE then {\\,\",\',\}
    private static int TranslateLookup(string input, int index, StringBuilder outBuf)
    {
        if (input[index] != '\\' || index + 1 >= input.Length)
        {
            // last lookup has a {"\\", ""} entry: a lone trailing backslash maps to ""
            if (input[index] == '\\')
                return 1;
            return 0;
        }
        char c = input[index + 1];
        switch (c)
        {
            // EntityArrays.JAVA_CTRL_CHARS_UNESCAPE
            case 'b':
                outBuf.Append('\b');
                return 2;
            case 'n':
                outBuf.Append('\n');
                return 2;
            case 't':
                outBuf.Append('\t');
                return 2;
            case 'f':
                outBuf.Append('\f');
                return 2;
            case 'r':
                outBuf.Append('\r');
                return 2;
            // {"\\\\","\\"},{"\\\"","\""},{"\\'","'"}
            case '\\':
                outBuf.Append('\\');
                return 2;
            case '"':
                outBuf.Append('"');
                return 2;
            case '\'':
                outBuf.Append('\'');
                return 2;
            default:
                // {"\\", ""}: a backslash not followed by a known escape is dropped
                outBuf.Append("");
                return 1;
        }
    }

    private static bool IsOctalDigit(char ch) => ch >= '0' && ch <= '7';

    private static bool IsZeroToThree(char ch) => ch >= '0' && ch <= '3';
}
