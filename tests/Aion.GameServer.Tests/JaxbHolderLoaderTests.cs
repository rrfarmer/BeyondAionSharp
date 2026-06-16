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
