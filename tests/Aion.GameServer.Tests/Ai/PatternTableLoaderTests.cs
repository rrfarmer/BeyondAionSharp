using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aion.GameServer.Ai.Pattern;
using Xunit;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// <b>Every guard and every action the extractor can write, put through the loader.</b>
/// </summary>
/// <remarks>
/// The pattern tables are moving out of generated C# and into data. While they were C#, a token
/// nobody had taught the port to translate was a build error; as data it would be a runtime one, and
/// a runtime error inside a boss fight is the worst place to find out.
/// <para>
/// So this reads the extractor's own output — the same TSVs the emitters read — and pushes every
/// distinct token and every distinct action shape through <see cref="PatternTableLoader"/>. It is the
/// compiler's coverage check, kept.
/// </para>
/// </remarks>
public class PatternTableLoaderTests
{
    private static readonly string[] Tables =
        ["battle_cycles.tsv", "death_spawns.tsv", "wake_idle_patterns.tsv"];

    private static IEnumerable<(string[] Header, string[] Fields)> Rows(string table)
    {
        string path = Path.Combine(BossAiHarness.RepoRoot(), "tools", "client-extract", "out", table);
        if (!File.Exists(path))
        {
            yield break;
        }

        string[] lines = File.ReadAllLines(path);
        string[] header = lines[0].Split('\t');
        foreach (string line in lines.Skip(1))
        {
            string[] fields = line.Split('\t');
            if (fields.Length >= header.Length)
            {
                yield return (header, fields);
            }
        }
    }

    private static string Field(string[] header, string[] fields, string name)
    {
        int at = Array.IndexOf(header, name);
        return at < 0 || at >= fields.Length ? string.Empty : fields[at];
    }

    /// <summary>
    /// <b>Every guard token in every table translates.</b> Distinct tokens only — the corpus repeats
    /// itself heavily, and a token either translates or it does not.
    /// </summary>
    [Fact]
    public void EveryGuardTokenTranslates()
    {
        HashSet<string> tokens = new();
        foreach (string table in Tables)
        {
            foreach ((string[] header, string[] fields) in Rows(table))
            {
                foreach (string token in Field(header, fields, "guards").Split('|'))
                {
                    if (token.Length > 0)
                    {
                        tokens.Add(token);
                    }
                }
            }
        }

        Assert.NotEmpty(tokens);
        List<string> refused = [];
        foreach (string token in tokens)
        {
            try
            {
                Assert.NotNull(PatternTableLoader.Guard(token));
            }
            catch (PatternTableFormatException ex)
            {
                refused.Add($"{token}: {ex.Message}");
            }
        }

        Assert.True(refused.Count == 0,
            $"{refused.Count} of {tokens.Count} guard tokens have no translation:\n"
            + string.Join("\n", refused.Take(20)));
    }

    /// <summary>
    /// <b>Every action shape in every table translates.</b> Keyed by the fields that choose the
    /// overload — the kind and the place — rather than by the numbers, which only fill it in.
    /// </summary>
    [Fact]
    public void EveryActionShapeTranslates()
    {
        Dictionary<string, PatternTableLoader.ActionRow> shapes = new();
        foreach (string table in Tables)
        {
            foreach ((string[] header, string[] fields) in Rows(table))
            {
                string kind = Field(header, fields, "kind");
                if (kind.Length == 0)
                {
                    continue;
                }

                PatternTableLoader.ActionRow row = new(
                    kind,
                    Field(header, fields, "a1"), Field(header, fields, "a2"), Field(header, fields, "a3"),
                    Field(header, fields, "place"),
                    Field(header, fields, "x"), Field(header, fields, "y"), Field(header, fields, "z"),
                    Field(header, fields, "group"));
                shapes[kind + "|" + row.Place] = row;
            }
        }

        Assert.NotEmpty(shapes);
        List<string> refused = [];
        foreach ((string shape, PatternTableLoader.ActionRow row) in shapes)
        {
            try
            {
                Assert.NotNull(PatternTableLoader.Action(row));
            }
            catch (PatternTableFormatException ex)
            {
                refused.Add($"{shape}: {ex.Message}");
            }
        }

        Assert.True(refused.Count == 0,
            $"{refused.Count} of {shapes.Count} action shapes have no translation:\n"
            + string.Join("\n", refused.Take(20)));
    }

    /// <summary>
    /// <b>And a token nobody taught it is refused loudly.</b> The whole point of writing this by hand
    /// rather than by reflection is that an unknown name fails at load with its own name in the
    /// message, instead of failing later as a missing method inside a fight.
    /// </summary>
    [Fact]
    public void AnUnknownTokenIsRefusedByName()
    {
        PatternTableFormatException guard =
            Assert.Throws<PatternTableFormatException>(() => PatternTableLoader.Guard("wingspan:3"));
        Assert.Contains("wingspan:3", guard.Message);

        PatternTableFormatException action = Assert.Throws<PatternTableFormatException>(
            () => PatternTableLoader.Action(new PatternTableLoader.ActionRow(
                "levitate", "1", "0", "0", "", "0", "0", "0", "0")));
        Assert.Contains("levitate", action.Message);
    }
}
