using Aion.GameServer.Dataholders;
using Aion.GameServer.Dataholders.LoadingUtils;

namespace Aion.GameServer.Tests;

/// <summary>
/// Pilot smoke test for the faithful per-holder XML load path (JaxbHolderLoader).
/// Loads the real game-server/data/static_data/bind_points/bind_points.xml into the faithful
/// BindPointData holder and asserts known entries, proving the JAXB-style holder can be populated
/// from its source XML via XmlSerializer + AfterUnmarshal.
/// </summary>
public sealed class JaxbHolderLoaderTests
{
    [Fact]
    public void LoadFromFile_PopulatesBindPointDataFromRealXml()
    {
        var path = ResolveStaticDataFile("bind_points", "bind_points.xml");

        var data = JaxbHolderLoader.LoadFromFile<BindPointData>(path);

        // AfterUnmarshal built the npcId->template index and nulled the raw list.
        Assert.True(data.Size() > 0);

        // Known entry from bind_points.xml: npcid="700013" name="Binding_Stone_akarios" price="47".
        var akarios = data.GetBindPointTemplate(700013);
        Assert.NotNull(akarios);
        Assert.Equal("Binding_Stone_akarios", akarios!.GetName());
        Assert.Equal(700013, akarios.GetNpcId());
        Assert.Equal(47, akarios.GetPrice());

        // Free bind stone (price="-1").
        var abyssLi = data.GetBindPointTemplate(250092);
        Assert.NotNull(abyssLi);
        Assert.Equal(-1, abyssLi!.GetPrice());

        Assert.Null(data.GetBindPointTemplate(-99999));
    }

    [Fact]
    public void LoadFromFile_PopulatesCuringObjectsDataFromRealXml()
    {
        var path = ResolveStaticDataFile("curing_objects", "curing_objects.xml");

        var data = JaxbHolderLoader.LoadFromFile<CuringObjectsData>(path);

        // AfterUnmarshal copied every curing_object row into the list.
        Assert.True(data.Size() > 0);

        var objects = data.GetCuringObject();
        Assert.NotNull(objects);

        // First row: <curing_object map_id="710010000" x="1765.6873" y="2601.5105" z="231.19335" range="10.3"/>
        var first = objects[0];
        Assert.Equal(710010000, first.GetMapId());
        Assert.Equal(10.3f, first.GetRange(), 3);
    }

    [Fact]
    public void LoadFromFile_PopulatesChestDataFromRealXml()
    {
        var path = ResolveStaticDataFile("chests", "chest_templates.xml");

        var data = JaxbHolderLoader.LoadFromFile<ChestData>(path);

        // AfterUnmarshal built the npcId->template index and nulled the raw list.
        Assert.True(data.Size() > 0);

        // Known entry: <chest npc_id="700472"><key_item item_ids="185000036" count="1"/></chest>
        var chest = data.GetChestTemplate(700472);
        Assert.NotNull(chest);
        Assert.Equal(700472, chest!.GetNpcId());
        var keyItems = chest.GetKeyItems();
        Assert.NotNull(keyItems);
        Assert.Single(keyItems!);
        Assert.Equal(1, keyItems![0].GetCount());
        Assert.Contains(185000036, keyItems[0].GetItemIds());

        Assert.Null(data.GetChestTemplate(-99999));
    }

    [Fact]
    public void LoadFromFile_PopulatesRoadDataFromRealXml()
    {
        var path = ResolveStaticDataFile("roads", "roads.xml");

        var data = JaxbHolderLoader.LoadFromFile<RoadData>(path);

        Assert.True(data.Size() > 0);

        var roads = data.GetRoadTemplates();
        Assert.NotNull(roads);

        // First row: <road radius="30.0" map="210030000" name="VERTERON_TO_ELTNEN"> with center/p1/p2/roadexit.
        var first = roads[0];
        Assert.Equal("VERTERON_TO_ELTNEN", first.GetName());
        Assert.Equal(210030000, first.GetMap());
        Assert.Equal(30.0f, first.GetRadius(), 3);
        Assert.NotNull(first.GetCenter());
        Assert.NotNull(first.GetP1());
        Assert.NotNull(first.GetP2());
        Assert.NotNull(first.GetRoadExit());
        // roadexit mapid="210020000" on first road.
        Assert.Equal(210020000, first.GetRoadExit().GetMap());
    }

    [Fact]
    public void LoadFromFile_PopulatesHotspotDataFromRealXml()
    {
        var path = ResolveStaticDataFile("hotspot_template.xml");

        var data = JaxbHolderLoader.LoadFromFile<HotspotData>(path);

        Assert.True(data.Size() > 0);

        // Known entry: <hotspot_location id="13" worldId="210010000" x="807.0" y="1242.0" z="119.0" race="ELYOS" price="44"/>
        var hotspot = data.GetHotspotTemplateById(13);
        Assert.NotNull(hotspot);
        Assert.Equal(13, hotspot!.GetId());
        Assert.Equal(210010000, hotspot.GetWorldId());
        Assert.Equal(Model.Race.ELYOS, hotspot.GetRace());
        Assert.Equal(44L, hotspot.GetPrice());
    }

    [Fact]
    public void LoadFromFile_PopulatesMapWeatherDataFromRealXml()
    {
        var path = ResolveStaticDataFile("weather_table.xml");

        var data = JaxbHolderLoader.LoadFromFile<MapWeatherData>(path);

        // AfterUnmarshal built the mapId->table index and nulled the raw list.
        Assert.True(data.Size() > 0);

        // First map row: <map id="210010000" zone_count="2" weather_count="7">.
        var table = data.GetWeather(210010000);
        Assert.NotNull(table);
        Assert.Equal(210010000, table!.GetMapId());
        Assert.Equal(2, table.GetZoneCount());
        Assert.Equal(7, table.GetWeatherCount());

        // First table entry: <table zone_id="1" rank="2" code="1" name="RAIN"/>.
        var entries = table.GetZoneData();
        Assert.NotNull(entries);
        var first = entries[0];
        Assert.Equal(1, first.GetZoneId());
        Assert.Equal(1, first.GetCode());
        Assert.Equal("RAIN", first.GetWeatherName());
        Assert.False(first.IsBefore());
        Assert.False(first.IsAfter());

        // <table zone_id="1" rank="1" code="2" name="RAIN" before="true"/>.
        Assert.True(entries[1].IsBefore());

        Assert.Null(data.GetWeather(-99999));
    }

    [Fact]
    public void LoadFromFile_PopulatesKillBountyDataFromRealXml()
    {
        var path = ResolveStaticDataFile("bounties", "kill_bounties.xml");

        var data = JaxbHolderLoader.LoadFromFile<KillBountyData>(path);

        Assert.True(data.Size() > 0);

        var bounties = data.GetKillBounties();
        Assert.NotNull(bounties);

        // First row: <kill_bounty type="PER_X_KILLS" kill_count="1000" is_random_reward="true">
        //   <bounty item_id="168310018" count="1" />
        var first = bounties[0];
        Assert.Equal(Model.Templates.Bounty.BountyType.PER_X_KILLS, first.GetBountyType());
        Assert.Equal(1000, first.GetKillCount());
        Assert.True(first.IsRandomReward());
        // race omitted on this row -> defaults to PC_ALL.
        Assert.Equal(Model.Race.PC_ALL, first.GetRaceCondition());

        var firstBounties = first.GetBounties();
        Assert.NotNull(firstBounties);
        Assert.Single(firstBounties!);
        Assert.Equal(168310018, firstBounties![0].GetItemId());
        Assert.Equal(1, firstBounties[0].GetCount());
    }

    [Fact]
    public void LoadFromFile_PopulatesBaseDataFromRealXml()
    {
        var path = ResolveStaticDataFile("base", "base_locations.xml");

        var data = JaxbHolderLoader.LoadFromFile<BaseData>(path);

        Assert.True(data.Size() > 0);

        var templates = data.GetAllBaseTemplates();
        Assert.NotNull(templates);

        // First row: <base_location id="2120" type="CASUAL" world="210020000" />
        var first = templates[0];
        Assert.Equal(2120, first.GetId());
        Assert.Equal(210020000, first.GetWorldId());
        Assert.Equal(Model.Base.BaseType.CASUAL, first.GetType_());
        // default_occupier omitted -> defaults to BALAUR.
        Assert.Equal(Model.Base.BaseOccupier.BALAUR, first.GetDefaultOccupier());

        // A PANESTERRA_FACTION_CAMP row carries an explicit default_occupier enum.
        var ivy = templates.First(t => t.GetId() == 4211);
        Assert.Equal(Model.Base.BaseType.PANESTERRA_FACTION_CAMP, ivy.GetType_());
        Assert.Equal(Model.Base.BaseOccupier.IVY_TEMPLE, ivy.GetDefaultOccupier());
    }

    [Fact]
    public void LoadFromFile_PopulatesLegionDominionDataFromRealXml()
    {
        var path = ResolveStaticDataFile("legion_dominion_template.xml");

        var data = JaxbHolderLoader.LoadFromFile<LegionDominionData>(path);

        Assert.True(data.Size() > 0);

        var locations = data.GetLocationTemplates();
        Assert.NotNull(locations);

        // First row: <legion_dominion_location id="1" world_id="220080000"
        //   zone="LegionDominionArea_01" race="ASMODIANS" name_id="404623">
        var first = locations[0];
        Assert.Equal(1, first.GetId());
        Assert.Equal(220080000, first.GetWorldId());
        Assert.Equal("LegionDominionArea_01", first.GetZone());
        Assert.Equal(Model.Race.ASMODIANS, first.GetRace());
        Assert.Equal(404623, first.GetL10nId());

        var rewards = first.GetRewards();
        Assert.NotNull(rewards);
        Assert.True(rewards!.Count > 0);
        // First reward: <reward rank="1" item_id="188053896" count="1" />
        Assert.Equal(1, rewards[0].GetRank());
        Assert.Equal(188053896, rewards[0].GetItemId());
        Assert.Equal(1, rewards[0].GetCount());

        // <invasion_rift key_item_id="185000233" rift_id="2289" />
        var rift = first.GetInvasionRift();
        Assert.NotNull(rift);
        Assert.Equal(185000233, rift!.GetKeyItemId());
        Assert.Equal(2289, rift.GetRiftId());
    }

    private static string ResolveStaticDataFile(params string[] relativeUnderStaticData)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(
                new[] { directory.FullName, "game-server", "data", "static_data" }
                    .Concat(relativeUnderStaticData).ToArray());
            if (File.Exists(candidate))
                return candidate;
            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate game-server/data/static_data/{string.Join('/', relativeUnderStaticData)} from {AppContext.BaseDirectory}");
    }
}
