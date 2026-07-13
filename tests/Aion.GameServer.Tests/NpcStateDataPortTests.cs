using System.Xml;
using System.Xml.Linq;

namespace Aion.GameServer.Tests;

public sealed class NpcStateDataPortTests
{
    [Fact]
    public void NpcTemplatesUseCorrectedInitialStates()
    {
        var expected = new Dictionary<int, string?>
        {
            [201019] = null,
            [201035] = null,
            [201062] = null,
            [203103] = "5",
            [203149] = "6",
            [203505] = null
        };
        var actual = new Dictionary<int, string?>();

        using XmlReader reader = XmlReader.Create(RepoFile("game-server", "data", "static_data", "npcs", "npc_templates.xml"));
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element || reader.Name != "npc_template")
                continue;

            int npcId = int.Parse(reader.GetAttribute("npc_id")!);
            if (expected.ContainsKey(npcId))
                actual[npcId] = reader.GetAttribute("state");
        }

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("220040000_Beluslan.xml", 213569)]
    [InlineData("400010000_Reshanta.xml", 700304)]
    [InlineData("400010000_Reshanta.xml", 253043)]
    public void AerialSpawnFlagReplacesLegacyState(string fileName, int npcId)
    {
        XElement spawn = FindSpawn(fileName, npcId);
        XElement[] spots = spawn.Elements("spot").ToArray();

        Assert.NotEmpty(spots);
        Assert.All(spots, spot =>
        {
            Assert.Null(spot.Attribute("state"));
            Assert.Equal("true", (string?)spot.Attribute("aerial_spawn"));
        });
    }

    [Fact]
    public void RemovedSpawnStateDoesNotOverrideNpcTemplateState()
    {
        XElement spawn = FindSpawn("700010000_Oriel.xml", 830774);

        Assert.Null(Assert.Single(spawn.Elements("spot")).Attribute("state"));
    }

    [Fact]
    public void ObsoleteAggroRunnerAiHandlerIsRemoved()
    {
        string handler = RepoPath("src", "Aion.GameServer", "Handlers", "AI", "WalkAggroRunnerAI.cs");

        Assert.False(File.Exists(handler));
    }

    private static XElement FindSpawn(string fileName, int npcId)
    {
        XDocument document = XDocument.Load(RepoFile("game-server", "data", "static_data", "spawns", "Npcs", fileName));
        return Assert.Single(document.Descendants("spawn"), spawn => (int?)spawn.Attribute("npc_id") == npcId);
    }

    private static string RepoFile(params string[] parts)
    {
        string path = RepoPath(parts);
        if (File.Exists(path))
            return path;
        throw new FileNotFoundException("Could not find repository file", Path.Combine(parts));
    }

    private static string RepoPath(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate) || Directory.Exists(Path.GetDirectoryName(candidate)))
                return candidate;
            directory = directory.Parent;
        }
        return Path.Combine(parts);
    }
}
