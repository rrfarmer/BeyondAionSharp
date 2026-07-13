using System.Xml;

namespace Aion.GameServer.Tests;

public sealed class ShieldMasteryDataTests
{
    private static readonly Dictionary<int, (int Value, int? Delta)> ExpectedChanges = new()
    {
        [43] = (0, null),
        [50] = (5, null),
        [62] = (5, null),
        [93] = (5, null),
        [99] = (5, null),
        [11479] = (18, 2)
    };

    [Fact]
    public void ShieldMasterySkillsReduceDamageInsteadOfIncreasingBlock()
    {
        var actualChanges = new Dictionary<int, (string Stat, string Func, int Value, int? Delta)>();
        int? currentSkillId = null;
        bool insideShieldMastery = false;
        using XmlReader reader = XmlReader.Create(RepoFile("game-server", "data", "static_data", "skills", "skill_templates.xml"));

        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element && reader.Name == "skill_template")
            {
                int skillId = int.Parse(reader.GetAttribute("skill_id")!);
                currentSkillId = ExpectedChanges.ContainsKey(skillId) ? skillId : null;
            }
            else if (currentSkillId != null && reader.NodeType == XmlNodeType.Element && reader.Name == "shieldmastery")
            {
                insideShieldMastery = true;
            }
            else if (currentSkillId != null && insideShieldMastery && reader.NodeType == XmlNodeType.Element && reader.Name == "change")
            {
                string? delta = reader.GetAttribute("delta");
                actualChanges.Add(currentSkillId.Value, (
                    reader.GetAttribute("stat")!,
                    reader.GetAttribute("func")!,
                    int.Parse(reader.GetAttribute("value")!),
                    delta is null ? null : int.Parse(delta)));
            }
            else if (reader.NodeType == XmlNodeType.EndElement && reader.Name == "shieldmastery")
            {
                insideShieldMastery = false;
            }
            else if (reader.NodeType == XmlNodeType.EndElement && reader.Name == "skill_template")
            {
                currentSkillId = null;
                insideShieldMastery = false;
            }
        }

        Assert.Equal(ExpectedChanges.Count, actualChanges.Count);
        foreach ((int skillId, (int value, int? delta)) in ExpectedChanges)
        {
            Assert.True(actualChanges.TryGetValue(skillId, out var change), $"Missing shield mastery change for skill {skillId}");
            Assert.Equal("DAMAGE_REDUCE", change.Stat);
            Assert.Equal("PERCENT", change.Func);
            Assert.Equal(value, change.Value);
            Assert.Equal(delta, change.Delta);
        }
    }

    private static string RepoFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
                return candidate;
            directory = directory.Parent;
        }
        throw new FileNotFoundException("Could not find repository file", Path.Combine(parts));
    }
}
