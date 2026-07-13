using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;
using Aion.GameServer.Controllers;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Model.Templates.Stats;
using IDFactory = Aion.GameServer.Utils.IdFactory.IDFactory;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class AerialSpawnPortTests
{
    [Fact]
    public void AerialSpawnAttributeDeserializesAndPropagatesToRuntimeTemplate()
    {
        var serializer = new XmlSerializer(typeof(SpawnSpotTemplate));
        using var reader = new StringReader("<SpawnSpotTemplate x=\"1\" y=\"2\" z=\"3\" aerial_spawn=\"true\" />");
        var spot = Assert.IsType<SpawnSpotTemplate>(serializer.Deserialize(reader));

        var runtime = new SpawnTemplate(new SpawnGroup(1, 2, 0, null), spot);

        Assert.True(spot.IsAerialSpawn());
        Assert.True(runtime.IsAerialSpawn());
    }

    [Theory]
    [InlineData(6, 5, true, 6, true)]
    [InlineData(0, 5, true, 5, false)]
    [InlineData(0, 0, true, 0, true)]
    [InlineData(0, 0, false, 0, false)]
    public void InitialStateUsesRetailPrecedence(
        int spawnState,
        int templateState,
        bool aerialSpawn,
        int expectedExactState,
        bool expectedFlying)
    {
        Npc npc = BuildNpc(spawnState, templateState, aerialSpawn);

        npc.GetController().OnBeforeSpawn();

        if (expectedExactState > 0)
            Assert.Equal(expectedExactState, npc.GetState());
        else
            Assert.True(npc.IsInState(CreatureState.WALK_MODE));
        Assert.Equal(expectedFlying, npc.IsInState(CreatureState.FLYING));
    }

    private static Npc BuildNpc(int spawnState, int templateState, bool aerialSpawn)
    {
        EnsureIdFactory();
        EnsureDataManager();
        var spot = new SpawnSpotTemplate
        {
            X = 1,
            Y = 2,
            Z = 3,
            State = spawnState,
            AerialSpawn = aerialSpawn
        };
        var spawn = new SpawnTemplate(new SpawnGroup(1, 2, 0, null), spot);
        var template = new NpcTemplate
        {
            npcId = 2,
            level = 1,
            state = templateState,
            statsTemplate = new StatsTemplate { MaxHp = 100 }
        };
        var controller = new NpcController();

        return new Npc(controller, spawn, template);
    }

    private static void EnsureIdFactory()
    {
        try
        {
            _ = IDFactory.GetInstance();
        }
        catch (InvalidOperationException)
        {
            IDFactory.RegisterInstance(new IDFactory());
        }
    }

    private static void EnsureDataManager()
    {
        if (DataManager.GetRegisteredInstance() is not null)
            return;

        var staticData = (StaticData)RuntimeHelpers.GetUninitializedObject(typeof(StaticData));
        PropertyInfo property = typeof(StaticData).GetProperty(nameof(StaticData.NpcSkillDataDh))!;
        property.SetValue(staticData, new NpcSkillData());
        ConstructorInfo constructor = typeof(DataManager).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            [typeof(StaticData)],
            modifiers: null)!;
        DataManager.RegisterInstance((DataManager)constructor.Invoke([staticData]));
    }
}
