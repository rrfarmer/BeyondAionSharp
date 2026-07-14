using Aion.GameServer.Dataholders;
using Aion.GameServer.Dataholders.LoadingUtils;
using Aion.GameServer.Model.Siege;

namespace Aion.GameServer.Tests;

public sealed class SiegeLocationXmlParityTests
{
    [Fact]
    public void RealSiegeXml_IndexesFortressGateRepairStone()
    {
        SiegeLocationData data = LoadRealSiegeLocations();

        var fortress = data.GetFortress()[1131];
        var repairData = fortress.GetTemplate().GetDoorRepairData();

        Assert.NotNull(repairData);
        Assert.Equal(188030000, repairData!.GetItemId());
        Assert.Equal(5, repairData.GetCount());
        Assert.Equal(30_000, repairData.GetCd());

        // GateRepairAI resolves the repair NPC's static id through this index before looking up the door.
        var repairStone = repairData.GetRepairStone(199);
        Assert.NotNull(repairStone);
        Assert.Equal(199, repairStone!.GetStaticId());
        Assert.Equal(53, repairStone.GetDoorId());
        Assert.Equal(new[] { 199, 200 }, repairData.GetRepairStones().Select(stone => stone.GetStaticId()).Order());
    }

    [Fact]
    public void RealSiegeXml_BuildsAutomaticFortressAssaultInputs()
    {
        SiegeLocationData data = LoadRealSiegeLocations();

        var assaultData = data.GetFortress()[1131].GetTemplate().GetAssaultData();
        Assert.NotNull(assaultData);
        Assert.Equal(15, assaultData!.GetDredgionId());
        Assert.Equal(3, assaultData.GetBaseBudget());
        Assert.Equal(180, assaultData.GetBaseDelay());

        var assaulters = assaultData.GetProcessedAssaulters();

        // FortressAssault consumes TELEPORT for waves 1/10, COMMANDER for the commander budget, and each
        // remaining combat category when computing normal waves. All categories declared by fortress 1131
        // must therefore survive the nested JAXB callback.
        Assert.NotEmpty(assaulters[AssaulterType.TELEPORT]);
        Assert.NotEmpty(assaulters[AssaulterType.COMMANDER]);
        Assert.NotEmpty(assaulters[AssaulterType.FIGHTER]);
        Assert.NotEmpty(assaulters[AssaulterType.ASSASSIN]);
        Assert.NotEmpty(assaulters[AssaulterType.PRIEST]);
        Assert.NotEmpty(assaulters[AssaulterType.WITCH]);
        Assert.NotEmpty(assaulters[AssaulterType.RANGER]);

        var teleporter = Assert.Single(assaulters[AssaulterType.TELEPORT]);
        Assert.Equal(276793, teleporter.GetNpcId());
        Assert.Equal(0, teleporter.GetSpawnCost());
        Assert.Equal(60, teleporter.GetHeadingOffset());
        Assert.Equal(2, teleporter.GetDistanceOffset());

        Assert.Equal(5, assaulters[AssaulterType.COMMANDER].Count);
        Assert.Equal(new[] { 1f, 1.25f, 1.5f, 1.75f, 2f },
            assaulters[AssaulterType.COMMANDER].Select(assaulter => assaulter.GetSpawnCost()));

        // Java keeps only NPC ids that have a corresponding spawn-cost entry. The XML has five fighters,
        // while AssaulterType.FIGHTER defines four costs, so the fifth id (276721) is intentionally dropped.
        Assert.Equal(new[] { 276717, 276718, 276719, 276720 },
            assaulters[AssaulterType.FIGHTER].Select(assaulter => assaulter.GetNpcId()));
    }

    [Fact]
    public void RealSiegeXml_AllNestedSiegeIndexesAreUsable()
    {
        SiegeLocationData data = LoadRealSiegeLocations();
        var fortressTemplates = data.GetFortress().Values.Select(fortress => fortress.GetTemplate()).ToList();

        var repairSets = fortressTemplates
            .Select(template => template.GetDoorRepairData())
            .Where(repairData => repairData != null)
            .ToList();
        Assert.NotEmpty(repairSets);
        Assert.All(repairSets, repairData =>
        {
            var stones = repairData!.GetRepairStones().ToList();
            Assert.NotEmpty(stones);
            Assert.All(stones, stone =>
            {
                Assert.True(stone.GetStaticId() > 0);
                Assert.True(stone.GetDoorId() > 0);
                Assert.Same(stone, repairData.GetRepairStone(stone.GetStaticId()));
            });
        });

        var assaultSets = fortressTemplates
            .Select(template => template.GetAssaultData())
            .Where(assaultData => assaultData != null)
            .ToList();
        Assert.NotEmpty(assaultSets);
        Assert.All(assaultSets, assaultData =>
        {
            var assaulters = assaultData!.GetProcessedAssaulters();
            Assert.NotEmpty(assaulters[AssaulterType.TELEPORT]);
            Assert.NotEmpty(assaulters[AssaulterType.COMMANDER]);
            Assert.Contains(assaulters,
                entry => entry.Key is not AssaulterType.TELEPORT and not AssaulterType.COMMANDER
                    && entry.Value.Count > 0);
        });
    }

    private static SiegeLocationData LoadRealSiegeLocations()
    {
        return JaxbHolderLoader.LoadFromFile<SiegeLocationData>(
            ResolveStaticDataFile("siege", "siege_locations.xml"));
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
