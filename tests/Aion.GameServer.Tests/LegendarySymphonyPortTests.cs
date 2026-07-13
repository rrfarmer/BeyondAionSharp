using System.Reflection;
using System.Xml.Linq;
using Aion.GameServer.Handlers.PlayerCommands;

namespace Aion.GameServer.Tests;

public sealed class LegendarySymphonyPortTests
{
    private static readonly int[][] ExpectedRewards =
    {
        new[] { 3, 186000236, 10 },
        new[] { 5, 186000399, 10 },
        new[] { 15, 166000195, 5 },
        new[] { 40, 188052388, 1 },
        new[] { 50, 188053695, 2 },
        new[] { 50, 188053610, 3 },
        new[] { 60, 188053321, 1 },
        new[] { 65, 188053903, 1 },
        new[] { 70, 166020003, 10 },
        new[] { 70, 166500005, 10 },
        new[] { 70, 166030007, 10 },
        new[] { 100, 188950015, 2 },
        new[] { 150, 188053099, 1 },
        new[] { 200, 188054238, 1 },
        new[] { 250, 187000090, 1 }
    };

    [Fact]
    public void RewardTableMatchesLegendarySymphonyEvent()
    {
        var field = typeof(Symphony).GetField("REWARDS", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(Symphony).FullName, "REWARDS");
        var actual = Assert.IsType<int[][]>(field.GetValue(null));

        Assert.Equal(ExpectedRewards.Length, actual.Length);
        for (int i = 0; i < ExpectedRewards.Length; i++)
            Assert.Equal(ExpectedRewards[i], actual[i]);
    }

    [Fact]
    public void SyntaxInfoIncludesEveryGeneratedReward()
    {
        string syntax = new Symphony().GetSyntaxInfo();

        Assert.Contains("[4] - (40 copies): 1x [item:188052388]", syntax);
        Assert.Contains("[15] - (250 copies): 1x [item:187000090]", syntax);
    }

    [Fact]
    public void RetailEventScheduleMatchesUpstreamWindow()
    {
        var document = XDocument.Load(RepoFile("game-server", "data", "static_data", "events", "timed_events", "retail_events.xml"));
        var events = document.Descendants("event").ToDictionary(e => (string)e.Attribute("name")!);

        Assert.Equal("2026-05-23T00:00:00", (string?)events["Legendary Symphony"].Attribute("start"));
        Assert.Equal("2026-06-09T23:59:59", (string?)events["Legendary Symphony"].Attribute("end"));
        Assert.Equal("2026-05-23T00:00:00", (string?)events["Legendary Symphony Drop"].Attribute("start"));
        Assert.Equal("2026-06-07T23:59:59", (string?)events["Legendary Symphony Drop"].Attribute("end"));
    }

    [Fact]
    public void DatabaseCleanupTargetsSymphonyAssemblyItems()
    {
        string sql = File.ReadAllText(RepoFile("game-server", "sql", "update.sql"));

        Assert.Contains("DB changes since f2f77fe (15.05.2026)", sql);
        Assert.Contains("DELETE FROM inventory WHERE item_id IN (182007170, 188100252, 188100253, 188100254, 188100255, 188100256);", sql);
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
