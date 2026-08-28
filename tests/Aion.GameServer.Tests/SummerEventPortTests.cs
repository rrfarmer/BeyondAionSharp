using System.Xml.Linq;

namespace Aion.GameServer.Tests;

public sealed class SummerEventPortTests
{
    [Fact]
    public void CustomEventScheduleMatchesUpstreamSummerWindows()
    {
        var events = LoadEventsByName("custom_events.xml");

        Assert.Equal("2026-08-03T00:00:00", (string?)events["Increased XP Rates"].Attribute("start"));
        Assert.Equal("2026-08-09T23:59:59", (string?)events["Increased XP Rates"].Attribute("end"));
        Assert.Equal("2026-08-03T00:00:00", (string?)events["Increased AP Rates"].Attribute("start"));
        Assert.Equal("2026-08-09T23:59:59", (string?)events["Increased AP Rates"].Attribute("end"));
        Assert.Equal("2026-08-10T00:00:00", (string?)events["Increased Drop Rates"].Attribute("start"));
        Assert.Equal("2026-08-16T23:59:59", (string?)events["Increased Drop Rates"].Attribute("end"));
        Assert.Equal("2026-08-17T00:00:00", (string?)events["Increased Gather Count"].Attribute("start"));
        Assert.Equal("2026-08-23T23:59:59", (string?)events["Increased Gather Count"].Attribute("end"));
        Assert.Equal("2026-08-17T00:00:00", (string?)events["Increased Crafting Crit Rate"].Attribute("start"));
        Assert.Equal("2026-08-23T23:59:59", (string?)events["Increased Crafting Crit Rate"].Attribute("end"));
    }

    [Fact]
    public void NewEnchantmentRateEventOverridesBothStoneChanceKeys()
    {
        var events = LoadEventsByName("custom_events.xml");
        var enchantmentEvent = events["Increased Enchantment Rate"];

        Assert.Equal("2026-08-16T00:00:00", (string?)enchantmentEvent.Attribute("start"));
        Assert.Equal("2026-08-16T23:59:59", (string?)enchantmentEvent.Attribute("end"));

        var properties = enchantmentEvent.Element("config_properties")!.Elements("property")
            .Select(p => p.Value.Trim()).ToList();
        Assert.Equal(
            new[]
            {
                "gameserver.rates.enchantment_stone.base_chances = 73",
                "gameserver.rates.enchantment_stone.amplified_chances = 55"
            },
            properties);
    }

    [Fact]
    public void RetailSummerBlockPartyScheduleMatchesUpstreamWindows()
    {
        var events = LoadEventsByName("retail_events.xml");

        Assert.Equal("2026-08-03T00:00:00", (string?)events["Summer Block Party"].Attribute("start"));
        Assert.Equal("2026-08-23T23:59:59", (string?)events["Summer Block Party"].Attribute("end"));
        Assert.Equal("2026-08-03T00:00:00", (string?)events["Summer Block Party Part 1"].Attribute("start"));
        Assert.Equal("2026-08-09T23:59:59", (string?)events["Summer Block Party Part 1"].Attribute("end"));
        Assert.Equal("2026-08-10T00:00:00", (string?)events["Summer Block Party Part 2"].Attribute("start"));
        Assert.Equal("2026-08-16T23:59:59", (string?)events["Summer Block Party Part 2"].Attribute("end"));
        Assert.Equal("2026-08-17T00:00:00", (string?)events["Summer Block Party Part 3"].Attribute("start"));
        Assert.Equal("2026-08-23T23:59:59", (string?)events["Summer Block Party Part 3"].Attribute("end"));
        Assert.Equal("2026-08-24T00:00:00", (string?)events["Summer Block Party Part 4"].Attribute("start"));
        Assert.Equal("2026-08-27T23:59:59", (string?)events["Summer Block Party Part 4"].Attribute("end"));
        Assert.Equal("2026-07-13T00:00:00", (string?)events["Alchemy Event"].Attribute("start"));
        Assert.Equal("2026-07-19T23:59:59", (string?)events["Alchemy Event"].Attribute("end"));
    }

    [Fact]
    public void DecomposeRewardChestsSwappedToNonEventBoxes()
    {
        var document = XDocument.Load(RepoFile(
            "game-server", "data", "static_data", "decomposable_items", "decomposable_items.xml"));
        var rewardItemIds = document.Descendants("items")
            .Where(g => (string?)g.Attribute("chance") == "2")
            .SelectMany(g => g.Elements("item"))
            .Select(i => (int)i.Attribute("id")!)
            .ToHashSet();

        Assert.Contains(188054238, rewardItemIds); // Iron Wall Armor Box
        Assert.Contains(188053698, rewardItemIds); // Honorable Weapon Box of Conquest
        Assert.DoesNotContain(188053006, rewardItemIds); // [Event] Hyperion's Mythic Armor Chest
        Assert.DoesNotContain(188053007, rewardItemIds); // [Event] Hyperion's Mythic Weapon Chest
    }

    private static Dictionary<string, XElement> LoadEventsByName(string fileName)
    {
        var document = XDocument.Load(RepoFile(
            "game-server", "data", "static_data", "events", "timed_events", fileName));
        return document.Descendants("event").ToDictionary(e => (string)e.Attribute("name")!);
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
