using Aion.GameServer.Utils.Xml;

namespace Aion.GameServer.Tests;

/// <summary>
/// Parity tests for StringEscapeUtils.UnescapeJava vs Apache commons-lang3
/// org.apache.commons.lang3.StringEscapeUtils.unescapeJava (used by the //announcements admin command).
/// </summary>
public sealed class StringEscapeUtilsTests
{
    [Theory]
    [InlineData(@"Hello\nWorld", "Hello\nWorld")]
    [InlineData(@"Tab\there", "Tab\there")]
    [InlineData(@"a\rb", "a\rb")]
    [InlineData(@"a\fb", "a\fb")]
    [InlineData(@"a\bb", "a\bb")]
    [InlineData("quote\\\"end", "quote\"end")] // \" -> "
    [InlineData(@"back\\slash", @"back\slash")] // \\ -> \
    [InlineData(@"\101bc", "Abc")]           // octal 101 = 'A'
    [InlineData(@"\47", "'")]                // octal 47 = apostrophe
    [InlineData("plain", "plain")]
    [InlineData(@"\x", "x")]                  // unknown escape: backslash dropped
    [InlineData(@"end\", "end")]             // trailing lone backslash dropped
    public void UnescapeJava_MatchesCommonsLang(string input, string expected)
    {
        Assert.Equal(expected, StringEscapeUtils.UnescapeJava(input));
    }

    [Fact]
    public void UnescapeJava_UnicodeEscape()
    {
        // input is backslash-u-0-0-4-1 + "BC"  ->  "ABC"
        string input = "\\u0041" + "BC";
        Assert.Equal("ABC", StringEscapeUtils.UnescapeJava(input));
        // backslash-u-0-0-e-9 -> é (U+00E9)
        Assert.Equal("é", StringEscapeUtils.UnescapeJava("\\u00e9"));
    }

    [Fact]
    public void UnescapeJava_Null_ReturnsNull()
    {
        Assert.Null(StringEscapeUtils.UnescapeJava(null));
    }
}
