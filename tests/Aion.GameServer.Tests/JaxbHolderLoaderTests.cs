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
