using System.Reflection;
using System.Runtime.CompilerServices;
using Aion.GameServer.Ai;
using Aion.GameServer.Controllers;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Model.Templates.Stats;
using Aion.GameServer.Utils.Stats;
using IDFactory = Aion.GameServer.Utils.IdFactory.IDFactory;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class NpcCustomHpExperiencePortTests : IDisposable
{
    private const string TestAiName = "xp_test_max_hp";
    private static bool aiRegistered;
    private readonly DataManager? previousDataManager = DataManager.GetRegisteredInstance();

    [Fact]
    public void BaseExperienceUsesAiModifiedMaximumHp()
    {
        Npc npc = BuildNpc();
        MethodInfo calculateBaseExp = typeof(StatFunctions).GetMethod(
            "CalculateBaseExp",
            BindingFlags.Static | BindingFlags.NonPublic)!;

        int baseExperience = (int)calculateBaseExp.Invoke(null, [npc])!;

        Assert.Equal(250, npc.GetGameStats().GetMaxHp().GetCurrent());
        Assert.Equal(600, baseExperience);
    }

    private static Npc BuildNpc()
    {
        EnsureInfrastructure();
        var spawn = new SpawnTemplate(new SpawnGroup(1, 920001, 0, null), new SpawnSpotTemplate());
        var template = new NpcTemplate
        {
            npcId = 920001,
            ai = TestAiName,
            level = 1,
            rank = NpcRank.DISCIPLINED,
            rating = NpcRating.NORMAL,
            statsTemplate = new StatsTemplate { MaxHp = 100 }
        };

        return new Npc(new NpcController(), spawn, template);
    }

    private static void EnsureInfrastructure()
    {
        try
        {
            _ = IDFactory.GetInstance();
        }
        catch (InvalidOperationException)
        {
            IDFactory.RegisterInstance(new IDFactory());
        }

        if (DataManager.GetRegisteredInstance()?.StaticData.NpcSkillDataDh is null)
        {
            var staticData = (StaticData)RuntimeHelpers.GetUninitializedObject(typeof(StaticData));
            typeof(StaticData).GetProperty(nameof(StaticData.NpcSkillDataDh))!
                .SetValue(staticData, new NpcSkillData());
            ConstructorInfo constructor = typeof(DataManager).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                [typeof(StaticData)],
                modifiers: null)!;
            DataManager.RegisterInstance((DataManager)constructor.Invoke([staticData]));
        }

        if (!aiRegistered)
        {
            AIEngine.GetInstance().RegisterAI(typeof(MaxHpTestAI));
            aiRegistered = true;
        }
    }

    public void Dispose()
    {
        DataManager.RestoreInstance(previousDataManager);
    }

    [AIName(TestAiName)]
    public sealed class MaxHpTestAI : AITemplate<Npc>
    {
        public MaxHpTestAI(Npc owner) : base(owner)
        {
        }

        public override void ModifyOwnerStat(Stat2 stat)
        {
            if (stat.GetStat() == StatEnum.MAXHP)
                stat.SetBase(250);
        }
    }
}
