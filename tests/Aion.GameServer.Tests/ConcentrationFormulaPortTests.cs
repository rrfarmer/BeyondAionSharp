using Aion.GameServer.Dataholders;
using Aion.GameServer.Dataholders.LoadingUtils;

namespace Aion.GameServer.Tests;

public sealed class ConcentrationFormulaPortTests
{
    [Fact]
    public void CancelLevelBindsFromRealNpcTemplates()
    {
        var path = RepoFile("game-server", "data", "static_data", "npcs", "npc_templates.xml");
        var data = JaxbHolderLoader.LoadFromFile<NpcData>(path);

        // Explicit values from the retail import (5.8 with 4.6 fallback, carried in upstream's 4.8 data).
        Assert.Equal(0, data.GetNpcTemplate(201056)!.GetCancelLevel()); // quality siege weapon
        Assert.Equal(0, data.GetNpcTemplate(286251)!.GetCancelLevel()); // test_cancel_level_0
        Assert.Equal(90, data.GetNpcTemplate(201005)!.GetCancelLevel()); // earth spirit

        // No attribute -> retail NpcClassAttr default of 100.
        Assert.Equal(100, data.GetNpcTemplate(203700)!.GetCancelLevel()); // fasimedes
        Assert.Equal(100, data.GetNpcTemplate(200000)!.GetCancelLevel());
    }

    [Fact]
    public void PlayersAlwaysCancelAtLevel100()
    {
        // Creature's virtual GetCancelLevel is the player path in the interruption formula.
        var creature = (Model.GameObjects.Creature)System.Runtime.CompilerServices.RuntimeHelpers
            .GetUninitializedObject(typeof(Model.GameObjects.Players.Player));
        Assert.Equal(100, creature.GetCancelLevel());
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
