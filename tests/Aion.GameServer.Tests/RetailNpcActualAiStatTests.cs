using Aion.GameServer.Controllers;
using Aion.GameServer.Controllers.Effects;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Retail;
using Aion.GameServer.Tests.Ai;
using Aion.GameServer.World.Knownlist;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class RetailNpcActualAiStatTests
{
    private sealed class Owner : IStatOwner;

    [SkippableTheory]
    [InlineData(216520)]
    [InlineData(216580)]
    public void SelectedWorldBossUsesRetailHpThroughItsActualAiAndRetainsLegacySkillAttack(int id)
    {
        var (bytes, pin, directory) = RetailNpcRuntimeGetterTests.ProfileFixture();
        using var installed = RetailNpcStatCatalog.InstallForTests(bytes, pin, directory);
        var member = RetailNpcStatCatalog.Members.First(m => m.MapId != 0 && m.Profile.NpcId == id);
        using var h = BossAiHarness.For(member.MapId).WithAi(typeof(SematariuxAI), typeof(GelkmarosPadmarashkaAI)).Build();
        var npc = Create(member.MapId, id);
        if (id == 216520) Assert.IsType<SematariuxAI>(npc.GetAi());
        else Assert.IsType<GelkmarosPadmarashkaAI>(npc.GetAi());
        Assert.Equal(86954218, member.Profile.MaxHp);
        Assert.Equal(86954218, npc.GetLifeStats().GetMaxHp());
        Assert.Equal(86954218, npc.GetLifeStats().GetCurrentHp());
        var owner = new Owner();
        npc.GetGameStats().AddEffectOnly(owner, [new StatAddFunction(StatEnum.MAXHP, 2000, true)]);
        Assert.Equal(86956218, npc.GetLifeStats().GetMaxHp());
        npc.GetGameStats().EndEffect(owner);
        Assert.Equal(86954218, npc.GetLifeStats().GetMaxHp());
        // This is the retained 4.8 skill operand; normal attacks use source min/max.
        Assert.Equal(2200, npc.GetGameStats().GetMainHandPAttack().GetCurrent());
        Assert.Empty(h.Clock.Faults);
    }

    [Theory]
    [InlineData(216520)]
    [InlineData(216580)]
    public void UnselectedWorldBossRetainsItsExistingJavaTweak(int id)
    {
        Assert.False(RetailNpcStatCatalog.Enabled);
        using var h = BossAiHarness.For(120010000).WithAi(typeof(SematariuxAI), typeof(GelkmarosPadmarashkaAI)).Build();
        var npc = Create(120010000, id);
        Assert.Null(npc.GetGameStats().StatisticsProfile);
        Assert.Equal(20880000, npc.GetLifeStats().GetMaxHp());
        Assert.Equal(2200, npc.GetGameStats().GetMainHandPAttack().GetCurrent());
        Assert.Empty(h.Clock.Faults);
    }

    [SkippableTheory]
    [InlineData(217161)]
    [InlineData(217733)]
    [InlineData(700099)]
    public void AltgardOneDamageAiBaseDefensesAlreadyAgreeWithTheSelectedSource(int id)
    {
        var (bytes, pin, directory) = RetailNpcRuntimeGetterTests.ProfileFixture();
        using var installed = RetailNpcStatCatalog.InstallForTests(bytes, pin, directory);
        using var h = BossAiHarness.For(220030000).WithAi(typeof(OneDmgAI), typeof(OneDmgNoActionAI)).Build();
        var npc = Create(220030000, id); var profile = npc.GetGameStats().StatisticsProfile!;
        Assert.True(npc.GetAi() is OneDmgAI or OneDmgNoActionAI);
        Assert.Equal(0, profile.Dodge); Assert.Equal(0, profile.MagicalResist);
        Assert.Equal(0, npc.GetGameStats().GetEvasion().GetCurrent());
        Assert.Equal(0, npc.GetGameStats().GetMResist().GetCurrent());
        Assert.Equal(profile.MaxHp, npc.GetLifeStats().GetMaxHp());
        Assert.Empty(h.Clock.Faults);
    }

    private static Npc Create(int map, int id)
    {
        var spawn = Aion.GameServer.SpawnEngine.SpawnEngine.NewSingleTimeSpawn(map, id, 100, 100, 200, 0);
        var npc = new Npc(new NpcController(), spawn, DataManager.NPC_DATA.GetNpcTemplate(id));
        npc.SetKnownlist(new KnownList(npc));
        npc.SetEffectController(new EffectController(npc));
        return npc;
    }
}
