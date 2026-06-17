using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Aion.Commons.Configuration;

/// <summary>
/// Java parity: java.util.Properties (the subset the config framework relies on) plus PropertiesUtils
/// (commons/utils/PropertiesUtils, SoulKeeper) load/loadFromDirectory cascading.
/// <para>
/// Faithful to java.util.Properties.load line semantics: '#'/'!' line comments; '=', ':' or whitespace as the
/// first key/value separator; backslash escapes (\t \n \r \f \\ \uXXXX and escaped separators); line continuation
/// via a trailing odd number of backslashes; leading whitespace stripped on continuation lines. A chained
/// <see cref="_defaults"/> Properties is consulted on miss, exactly like Java's defaults constructor.
/// </para>
/// </summary>
public sealed class JavaProperties
{
    private readonly Dictionary<string, string> _map = new();
    private readonly JavaProperties? _defaults;

    public JavaProperties() { }

    public JavaProperties(JavaProperties? defaults) => _defaults = defaults;

    /// <summary>Java parity: Properties.setProperty.</summary>
    public void SetProperty(string key, string value) => _map[key] = value;

    /// <summary>
    /// Java parity: Properties.putAll(Map) — copies every entry of <paramref name="source"/> into this instance's
    /// own map (NOT the chained defaults), overwriting existing keys. Mirrors Hashtable.putAll semantics used by
    /// Config.load() to merge the active-event config overrides over the loaded base properties.
    /// </summary>
    public void PutAll(IEnumerable<KeyValuePair<string, string>> source)
    {
        foreach (var kv in source)
            _map[kv.Key] = kv.Value;
    }

    /// <summary>Java parity: Properties.getProperty(key) — consults defaults on miss; null if absent everywhere.</summary>
    public string? GetProperty(string key)
    {
        if (_map.TryGetValue(key, out var v))
            return v;
        return _defaults?.GetProperty(key);
    }

    /// <summary>Java parity: Properties.getProperty(key, defaultValue).</summary>
    public string GetProperty(string key, string defaultValue) => GetProperty(key) ?? defaultValue;

    /// <summary>Java parity: Properties.stringPropertyNames() — keys visible across this + defaults.</summary>
    public ISet<string> StringPropertyNames()
    {
        var names = new HashSet<string>();
        if (_defaults != null)
            names.UnionWith(_defaults.StringPropertyNames());
        names.UnionWith(_map.Keys);
        return names;
    }

    public int Count => StringPropertyNames().Count;

    /// <summary>Java parity: Properties.load(Reader) line/escape semantics.</summary>
    public void Load(TextReader reader)
    {
        string? rawLine;
        while ((rawLine = ReadLogicalLine(reader)) != null)
        {
            ParseLine(rawLine);
        }
    }

    /// <summary>Java parity: PropertiesUtils.loadProperties(properties, file).</summary>
    public void LoadFromFile(string path)
    {
        using var sr = new StreamReader(path, Encoding.GetEncoding("ISO-8859-1"));
        Load(sr);
    }

    /// <summary>
    /// Java parity: PropertiesUtils.load(file, defaults) — empty result if the file is missing/not a regular file.
    /// </summary>
    public static JavaProperties Load(string path, JavaProperties? defaults)
    {
        var p = new JavaProperties(defaults);
        if (File.Exists(path))
            p.LoadFromFile(path);
        return p;
    }

    /// <summary>
    /// Java parity: PropertiesUtils.loadFromDirectory(properties, dir, recursive). Loads every *.properties file
    /// (sorted for determinism) into this instance; later files override earlier keys.
    /// </summary>
    public void LoadFromDirectory(string dir, bool recursive)
    {
        if (!Directory.Exists(dir))
            return;
        var opt = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        foreach (var file in Directory.GetFiles(dir, "*.properties", opt).OrderBy(f => f, StringComparer.Ordinal))
            LoadFromFile(file);
    }

    /// <summary>
    /// Reads one logical line, honoring line continuation (trailing odd backslash count) and skipping blank/comment
    /// lines. Returns null at end of input.
    /// </summary>
    private static string? ReadLogicalLine(TextReader reader)
    {
        var sb = new StringBuilder();
        bool haveContent = false;
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (!haveContent)
            {
                int s = SkipLeadingWhitespace(line, 0);
                if (s >= line.Length)
                    continue; // blank line
                char c = line[s];
                if (c == '#' || c == '!')
                    continue; // comment line
                line = line.Substring(s);
            }
            else
            {
                // continuation: strip leading whitespace of the continued physical line
                line = line.Substring(SkipLeadingWhitespace(line, 0));
            }

            if (EndsWithOddBackslashes(line))
            {
                sb.Append(line, 0, line.Length - 1); // drop the trailing continuation backslash
                haveContent = true;
                continue;
            }

            sb.Append(line);
            return sb.ToString();
        }
        return haveContent ? sb.ToString() : null;
    }

    private static int SkipLeadingWhitespace(string s, int i)
    {
        while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\f'))
            i++;
        return i;
    }

    private static bool EndsWithOddBackslashes(string s)
    {
        int count = 0;
        for (int i = s.Length - 1; i >= 0 && s[i] == '\\'; i--)
            count++;
        return (count & 1) == 1;
    }

    private void ParseLine(string line)
    {
        int i = 0;
        var key = new StringBuilder();
        // key terminates at first unescaped '=', ':' or whitespace
        bool sawSeparator = false;
        while (i < line.Length)
        {
            char c = line[i];
            if (c == '\\')
            {
                i = AppendEscaped(line, i, key);
                continue;
            }
            if (c == '=' || c == ':' || c == ' ' || c == '\t' || c == '\f')
            {
                sawSeparator = true;
                break;
            }
            key.Append(c);
            i++;
        }

        // skip whitespace after key
        while (i < line.Length && (line[i] == ' ' || line[i] == '\t' || line[i] == '\f'))
            i++;
        // skip a single explicit '=' or ':' separator (plus following whitespace)
        if (i < line.Length && (line[i] == '=' || line[i] == ':'))
        {
            i++;
            while (i < line.Length && (line[i] == ' ' || line[i] == '\t' || line[i] == '\f'))
                i++;
        }

        _ = sawSeparator;

        var value = new StringBuilder();
        while (i < line.Length)
        {
            char c = line[i];
            if (c == '\\')
            {
                i = AppendEscaped(line, i, value);
                continue;
            }
            value.Append(c);
            i++;
        }

        _map[key.ToString()] = value.ToString();
    }

    /// <summary>Consumes one backslash escape starting at index i; appends decoded char(s) to sb; returns next index.</summary>
    private static int AppendEscaped(string s, int i, StringBuilder sb)
    {
        i++; // consume backslash
        if (i >= s.Length)
            return i;
        char c = s[i];
        switch (c)
        {
            case 't': sb.Append('\t'); return i + 1;
            case 'n': sb.Append('\n'); return i + 1;
            case 'r': sb.Append('\r'); return i + 1;
            case 'f': sb.Append('\f'); return i + 1;
            case 'u':
                if (i + 4 < s.Length + 1 && i + 5 <= s.Length)
                {
                    string hex = s.Substring(i + 1, 4);
                    sb.Append((char)Convert.ToInt32(hex, 16));
                    return i + 5;
                }
                sb.Append(c);
                return i + 1;
            default:
                sb.Append(c); // \\ \= \: \space etc. -> literal char
                return i + 1;
        }
    }
}
