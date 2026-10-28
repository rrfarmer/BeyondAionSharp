using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Retail;
using Aion.GameServer.Tests.Ai;
using Aion.GameServer.Utils.Stats;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class RetailNpcStatusScalarTests
{
    private sealed class Owner : IStatOwner;

    [Fact]
    public void StatusMappingsUseTheNativeSlotsAndDoNotTreatElementsAsStatus()
    {
        Assert.Equal("abnormal_resist[10]", SkyTempleNpcProfile.AbnormalField(StatEnum.CURSE_RESISTANCE));
        Assert.Equal("abnormal_penetration[10]", RetailNpcStatDefenses.Field(StatEnum.CURSE_RESISTANCE_PENETRATION));
        Assert.Equal("abnormal_resist[12]", RetailNpcStatDefenses.Field(StatEnum.STUN_RESISTANCE));
        Assert.Equal("abnormal_penetration[13]", RetailNpcStatDefenses.Field(StatEnum.PERIFICATION_RESISTANCE_PENETRATION));
        Assert.Equal("elemental_defend_water", RetailNpcStatDefenses.Field(StatEnum.WATER_RESISTANCE));
        Assert.Null(SkyTempleNpcProfile.AbnormalField(StatEnum.WATER_RESISTANCE));
        Assert.Null(RetailNpcStatDefenses.Field(StatEnum.ABNORMAL_RESISTANCE_ALL));
    }

    [SkippableFact]
    public void SelectedNpcUsesRetailStatusModifierOrderAndCapsWithoutChangingRetiredHost()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        Skip.If(string.IsNullOrWhiteSpace(path)||string.IsNullOrWhiteSpace(hash),"Requires pinned retail statistics package");
        using var installed=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(path!),hash!,Path.GetDirectoryName(path!)!);
        using var h=BossAiHarness.For(220010000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var npc=h.SpawnWithAi(210407,"retail_stat_base_fixture");
        var profile=npc.GetGameStats().StatisticsProfile!;
        const StatEnum stat=StatEnum.CURSE_RESISTANCE_PENETRATION;
        long raw=profile.ScalarInputs.Fields["abnormal_penetration[10]"].Value;
        var first=new Owner();
        npc.GetGameStats().AddEffectOnly(first,[new StatRateFunction(stat,50,true),new StatAddFunction(stat,200,false)]);
        // Native first-level stage: raw + trunc(raw * 50 / 100) + 200;
        // a Java-style rate applied to the flat addition would give a different result.
        int expected=(int)Math.Clamp(raw+raw*50/100+200,0,9999);
        Assert.Equal(expected,npc.GetGameStats().GetStat(stat,0).GetCurrent());
        npc.GetGameStats().EndEffect(first);
        var high=new Owner();npc.GetGameStats().AddEffectOnly(high,[new StatAddFunction(stat,20000,true)]);
        Assert.Equal(9999,npc.GetGameStats().GetStat(stat,0).GetCurrent());
        npc.SetCreatorId(42); // Retires this retail profile and restores host ownership.
        Assert.Null(npc.GetGameStats().StatisticsProfile);
        Assert.True(npc.GetGameStats().GetStat(stat,0).GetCurrent()>9999);
        npc.GetGameStats().EndEffect(high);
        Assert.Empty(h.Clock.Faults);
    }

    [SkippableFact]
    public void RetailMobWithNonzeroCursePenetrationKeepsTheFlatModifierOutsideTheRate()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        Skip.If(string.IsNullOrWhiteSpace(path)||string.IsNullOrWhiteSpace(hash),"Requires pinned retail statistics package");
        using var installed=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(path!),hash!,Path.GetDirectoryName(path!)!);
        using var h=BossAiHarness.For(320080000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var npc=h.SpawnWithAi(213780,"retail_stat_base_fixture");
        const StatEnum stat=StatEnum.CURSE_RESISTANCE_PENETRATION;
        Assert.Equal(700,npc.GetGameStats().StatisticsProfile!.ScalarInputs.Fields["abnormal_penetration[10]"].Value);
        Assert.Equal(700,npc.GetGameStats().GetStat(stat,0).GetCurrent());
        var boost=new Owner();npc.GetGameStats().AddEffectOnly(boost,
            [new StatRateFunction(stat,50,true),new StatAddFunction(stat,200,false)]);
        Assert.Equal(1250,npc.GetGameStats().GetStat(stat,0).GetCurrent());
        var legacy=new AdditionStat(stat,700,null!);var types=new HashSet<CalculationType>();
        new StatAddFunction(stat,200,false).Apply(legacy,types);
        new StatRateFunction(stat,50,true).Apply(legacy,types);
        Assert.Equal(1350,legacy.GetCurrent());
        npc.GetGameStats().EndEffect(boost);
        var reduction=new Owner();npc.GetGameStats().AddEffectOnly(reduction,[new StatAddFunction(stat,-2000,true)]);
        Assert.Equal(0,npc.GetGameStats().GetStat(stat,0).GetCurrent());
        npc.GetGameStats().EndEffect(reduction);
        Assert.Equal(700,npc.GetGameStats().GetStat(stat,0).GetCurrent());
        Assert.Empty(h.Clock.Faults);
    }
}
