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
                "gameserver.rates.enchantment_stone.amplified_chances = 65"
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

    /// <summary>
    /// Upstream d254ea172 reorganised this data and carries the swap itself, in the override file its folder import
    /// merges last: box 188052625 offers the non-event chests, where this port had put them on 188052621.
    /// </summary>
    /// <remarks>
    /// The maintainer chose to follow upstream here (2026-11-02), which leaves one gap recorded rather than closed:
    /// upstream's regenerated retail file newly has box 188052358 (Power Lockbox Cache) offering the two [Event]
    /// Hyperion chests at 4 %, and no override row covers that box, so they stay reachable outside their event. This
    /// test therefore asserts what the override rows establish and names that boundary instead of claiming it is gone.
    /// </remarks>
    [Fact]
    public void DecomposeRewardChestsSwappedToNonEventBoxesByTheOverrideRows()
    {
        var overrides = XDocument.Load(RepoFile(
            "game-server", "data", "static_data", "decomposable_items", "custom_decomposable_items.xml"));
        var overriddenRewards = overrides.Descendants("decomposable")
            .Where(entry => (string?)entry.Attribute("override") == "true")
            .SelectMany(entry => entry.Descendants("item"))
            .Select(item => (int)item.Attribute("id")!)
            .ToHashSet();

        Assert.Contains(188054238, overriddenRewards); // Iron Wall Armor Box
        Assert.Contains(188053698, overriddenRewards); // Honorable Weapon Box of Conquest
        Assert.DoesNotContain(188053006, overriddenRewards); // [Event] Hyperion's Mythic Armor Chest
        Assert.DoesNotContain(188053007, overriddenRewards); // [Event] Hyperion's Mythic Weapon Chest

        // The recorded boundary: the event chests survive in upstream's retail data, from a box nothing overrides.
        var retail = XDocument.Load(RepoFile(
            "game-server", "data", "static_data", "decomposable_items", "decomposable_items.xml"));
        var stillOffering = retail.Descendants("decomposable")
            .Where(entry => entry.Descendants("item").Any(item => (int)item.Attribute("id")! is 188053006 or 188053007))
            .Select(entry => (int)entry.Attribute("item_id")!)
            .Where(itemId => !overrides.Descendants("decomposable").Any(o => (int)o.Attribute("item_id")! == itemId))
            .ToHashSet();
        Assert.Equal([188052358], stillOffering);
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
