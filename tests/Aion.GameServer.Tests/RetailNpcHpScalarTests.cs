using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Tests.Ai;
using Aion.GameServer.Utils.Stats;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class RetailNpcHpScalarTests
{
    private sealed class Owner : IStatOwner;
    [Aion.GameServer.Ai.AIName("retail_stat_hp_half_fixture")]
    public sealed class HalfHpAI(Aion.GameServer.Model.GameObjects.Npc owner):Aion.GameServer.Ai.NpcAI(owner)
    {
        public override bool CanThink()=>false;
        protected override void HandleSpawned()=>SetStateIfNot(Aion.GameServer.Ai.AIState.IDLE);
        public override void ModifyOwnerStat(Stat2 stat)
        {
            if(stat.GetStat()==StatEnum.MAXHP)stat.SetBaseRate(0.5f);
        }
    }

    private static (string Path,string Hash) Fixture()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        Skip.If(string.IsNullOrWhiteSpace(path)||string.IsNullOrWhiteSpace(hash),"Requires pinned retail statistics package");
        return (path!,hash!);
    }

    [SkippableFact]
    public void SourceBoundHpBuffUsesRetailOrderAlongsideOtherModifiers()
    {
        var (path,hash)=Fixture();
        using var installed=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(path),hash,Path.GetDirectoryName(path)!);
        using var h=BossAiHarness.For(301120000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var npc=h.SpawnWithAi(232847,"retail_stat_base_fixture");
        var source=npc.GetGameStats().StatisticsProfile!.ScalarInputs.Fields["max_hp"];
        Assert.Equal(233088,source.Value);
        var entry=Enumerable.Range(0,npc.GetSkillList().Size()).Select(i=>npc.GetSkillList().GetSkillOnPosition(i))
            .First(x=>x.GetSkillId()==20701);
        var skill=DataManager.SKILL_DATA.GetSkillTemplate(20701);
        var buff=Assert.IsType<StatupEffect>(skill.GetEffectTemplate(1));
        var other=new Owner();npc.GetGameStats().AddEffectOnly(other,
            [new StatRateFunction(StatEnum.MAXHP,50,true),new StatAddFunction(StatEnum.MAXHP,200,false)]);
        var effect=new Effect(npc,npc,skill,entry.GetSkillLevel());effect.AddSuccessEffect(buff);effect.ApplyEffect();
        Assert.True(RetailNpcStatBuffs.HasSelectedProgram(effect));
        var maximum=npc.GetGameStats().GetMaxHp();
        Assert.Equal(351832,maximum.GetCurrent());
        Assert.Equal(233288,maximum.GetBase()); // Existing base-HP consumers keep their host input.
        Assert.Equal(351832,npc.GetLifeStats().GetMaxHp());
        var legacy=new AdditionStat(StatEnum.MAXHP,233088,null!);var types=new HashSet<CalculationType>();
        new StatAddFunction(StatEnum.MAXHP,200,false).Apply(legacy,types);
        new StatRateFunction(StatEnum.MAXHP,50,true).Apply(legacy,types);
        new StatAddFunction(StatEnum.MAXHP,2000,true).Apply(legacy,types);
        Assert.Equal(351932,legacy.GetCurrent());
        effect.EndEffect();npc.GetGameStats().EndEffect(other);
        Assert.Equal(233088,npc.GetLifeStats().GetMaxHp());
        Assert.Empty(h.Clock.Faults);
    }

    [SkippableFact]
    public void SelectedHpRetainsIntegerPrecisionAndNativeBounds()
    {
        var (path,hash)=Fixture();
        using var installed=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(path),hash,Path.GetDirectoryName(path)!);
        using var h=BossAiHarness.For(301120000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var npc=h.SpawnWithAi(232847,"retail_stat_base_fixture");
        var large=new Owner();npc.GetGameStats().AddEffectOnly(large,[new StatAddFunction(StatEnum.MAXHP,16777217,true)]);
        Assert.Equal(17010305,npc.GetLifeStats().GetMaxHp());
        npc.GetGameStats().EndEffect(large);
        var cap=new Owner();npc.GetGameStats().AddEffectOnly(cap,[new StatAddFunction(StatEnum.MAXHP,2000000000,true)]);
        Assert.Equal(2000000000,npc.GetLifeStats().GetMaxHp());
        npc.GetGameStats().EndEffect(cap);
        var floor=new Owner();npc.GetGameStats().AddEffectOnly(floor,[new StatAddFunction(StatEnum.MAXHP,-1000000,true)]);
        Assert.Equal(1,npc.GetLifeStats().GetMaxHp());
        npc.GetGameStats().EndEffect(floor);
        Assert.Empty(h.Clock.Faults);
    }

    [Fact]
    public void ExactIntegerResultExpiresWhenAStatHookChangesItsInputs()
    {
        var stat=new AdditionStat(StatEnum.MAXHP,16777216,null!);
        stat.SetExactIntegerCurrent(16777217);
        Assert.Equal(16777217,stat.GetCurrent());
        stat.SetBaseRate(0.5f);
        Assert.False(stat.HasUnchangedExactIntegerCurrent());
        Assert.Equal(8388608,stat.GetCurrent());
        stat.SetBaseRate(1);
        stat.SetExactIntegerCurrent(16777217);
        stat.AddToBase(1); // A float addition can round back to the same base.
        Assert.False(stat.HasUnchangedExactIntegerCurrent());
        Assert.Equal(16777216,stat.GetCurrent());
    }

    [SkippableFact]
    public void SelectedLifeStealMobKeepsItsExactLargeRetailHp()
    {
        var (path,hash)=Fixture();
        using var installed=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(path),hash,Path.GetDirectoryName(path)!);
        using var h=BossAiHarness.For(120010000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var npc=h.SpawnWithAi(216520,"retail_stat_base_fixture");
        Assert.Equal(86954218,npc.GetGameStats().StatisticsProfile!.MaxHp);
        Assert.Equal(86954218,npc.GetLifeStats().GetMaxHp());
        Assert.Empty(h.Clock.Faults);
    }

    [SkippableFact]
    public void AiHpOverrideInvalidatesExactResultAndKeepsItsExistingBehavior()
    {
        var (path,hash)=Fixture();
        using var installed=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(path),hash,Path.GetDirectoryName(path)!);
        using var h=BossAiHarness.For(301120000).WithAi(typeof(HalfHpAI)).Build();
        var npc=h.SpawnWithAi(232847,"retail_stat_hp_half_fixture");
        var stat=npc.GetGameStats().GetMaxHp();
        Assert.False(stat.HasUnchangedExactIntegerCurrent());
        Assert.Equal(116544,stat.GetCurrent());
        Assert.Empty(h.Clock.Faults);
    }
}
