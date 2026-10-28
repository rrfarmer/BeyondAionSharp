using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Controllers.Observer;
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
public sealed class RetailNpcAttackScalarTests
{
    private sealed class Owner : IStatOwner;

    private static (string Path,string Hash) Fixture()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        Skip.If(string.IsNullOrWhiteSpace(path)||string.IsNullOrWhiteSpace(hash),"Requires pinned retail statistics package");
        return (path!,hash!);
    }

    private static Stat2 Current(Aion.GameServer.Model.GameObjects.Npc npc,StatEnum stat)=>stat switch
    {
        StatEnum.PHYSICAL_ACCURACY=>npc.GetGameStats().GetMainHandPAccuracy(),
        StatEnum.MAGICAL_ACCURACY=>npc.GetGameStats().GetMAccuracy(),
        StatEnum.PHYSICAL_CRITICAL=>npc.GetGameStats().GetMainHandPCritical(),
        StatEnum.BOOST_MAGICAL_SKILL=>npc.GetGameStats().GetMBoost(),
        _=>throw new ArgumentOutOfRangeException(nameof(stat))
    };

    [SkippableTheory]
    [InlineData(StatEnum.PHYSICAL_ACCURACY,"hit_accuracy")]
    [InlineData(StatEnum.MAGICAL_ACCURACY,"magical_hit_accuracy")]
    [InlineData(StatEnum.PHYSICAL_CRITICAL,"critical")]
    [InlineData(StatEnum.BOOST_MAGICAL_SKILL,"magical_skill_boost")]
    public void SelectedAttackGetterAppliesRetailModifierOrderAndBounds(StatEnum stat,string field)
    {
        var (path,hash)=Fixture();
        using var installed=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(path),hash,Path.GetDirectoryName(path)!);
        using var h=BossAiHarness.For(220010000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var npc=h.SpawnWithAi(210407,"retail_stat_base_fixture");
        long raw=npc.GetGameStats().StatisticsProfile!.ScalarInputs.Fields[field].Value;
        Assert.Equal(field,RetailNpcStatDefenses.Field(stat));
        var owner=new Owner();npc.GetGameStats().AddEffectOnly(owner,
            [new StatRateFunction(stat,50,true),new StatAddFunction(stat,200,false)]);
        Assert.Equal((int)Math.Clamp(raw+raw*50/100+200,0,9999),Current(npc,stat).GetCurrent());
        npc.GetGameStats().EndEffect(owner);
        var cap=new Owner();npc.GetGameStats().AddEffectOnly(cap,[new StatAddFunction(stat,20000,true)]);
        Assert.Equal(9999,Current(npc,stat).GetCurrent());
        if(stat==StatEnum.MAGICAL_ACCURACY)
        {
            npc.SetCreatorId(42);
            Assert.Null(npc.GetGameStats().StatisticsProfile);
            Assert.True(Current(npc,stat).GetCurrent()>9999);
        }
        npc.GetGameStats().EndEffect(cap);
        Assert.Empty(h.Clock.Faults);
    }

    [SkippableFact]
    public void AltgardMobUsesItsBoundRetailAccuracyBuffWithAnotherModifier()
    {
        var (path,hash)=Fixture();
        using var installed=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(path),hash,Path.GetDirectoryName(path)!);
        using var h=BossAiHarness.For(220020000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var npc=h.SpawnWithAi(212880,"retail_stat_base_fixture");
        var profile=npc.GetGameStats().StatisticsProfile!;
        var entry=Enumerable.Range(0,npc.GetSkillList().Size()).Select(i=>npc.GetSkillList().GetSkillOnPosition(i))
            .First(x=>x.GetSkillId()==16873);
        var skill=DataManager.SKILL_DATA.GetSkillTemplate(16873);
        var buff=Assert.IsType<StatupEffect>(skill.GetEffectTemplate(2));
        var other=new Owner();npc.GetGameStats().AddEffectOnly(other,[new StatAddFunction(StatEnum.PHYSICAL_ACCURACY,200,false)]);
        var effect=new Effect(npc,npc,skill,entry.GetSkillLevel());effect.AddSuccessEffect(buff);effect.ApplyEffect();
        Assert.True(RetailNpcStatBuffs.HasSelectedProgram(effect));
        long raw=profile.ScalarInputs.Fields["hit_accuracy"].Value;
        Assert.Equal(1013,raw);
        var accuracy=npc.GetGameStats().GetMainHandPAccuracy();
        Assert.Equal(1719,accuracy.GetCurrent());
        // Native one-time accuracy modifiers consume the current actor word.
        var oneTime=new OneTimeBoostSkillAttack(null!,SkillType.ALL,0,0,10,20,1);
        Assert.Equal(191,oneTime.CalculatePhysicalAccuracyBonus(accuracy));
        var legacy=new AdditionStat(StatEnum.PHYSICAL_ACCURACY,1013,null!);var types=new HashSet<CalculationType>();
        new StatAddFunction(StatEnum.PHYSICAL_ACCURACY,200,false).Apply(legacy,types);
        new StatRateFunction(StatEnum.PHYSICAL_ACCURACY,50,true).Apply(legacy,types);
        Assert.Equal(1819,legacy.GetCurrent());
        effect.EndEffect();npc.GetGameStats().EndEffect(other);
        Assert.Empty(h.Clock.Faults);
    }
}
