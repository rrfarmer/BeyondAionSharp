using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Tests.Ai;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class RetailNpcCurrentHpTests
{
    private sealed class Owner : IStatOwner;
    private static (string Path,string Hash) Fixture()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        Skip.If(string.IsNullOrWhiteSpace(path)||string.IsNullOrWhiteSpace(hash),"Requires pinned retail statistics package");
        return (path!,hash!);
    }

    [Theory]
    [InlineData(1001,651,100,66)]
    [InlineData(1001,2001,100,199)]
    [InlineData(1001,651,0,0)]
    [InlineData(0,1001,10,10)]
    [InlineData(1807131033,1588920085,1807131033,1588920089)]
    public void NativeNpcHpChangeUsesTruncatedFloatDelta(int before,int after,int current,int expected)
        => Assert.Equal(expected,RetailNpcCurrentHpMath.Adjust(before,after,current));

    [SkippableTheory]
    [InlineData(false,100)]
    [InlineData(true,102)]
    public void SourceBoundHpBuffRescalesCurrentHpWithTheSelectedOwner(bool aiOverride,int expected)
    {
        var (path,hash)=Fixture();
        using var installed=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(path),hash,Path.GetDirectoryName(path)!);
        var ai=aiOverride?typeof(RetailNpcHpScalarTests.HalfHpAI):typeof(RetailNpcStatCatalogTests.BaseFixtureAI);
        using var h=BossAiHarness.For(301120000).WithAi(ai).Build();
        var npc=h.SpawnWithAi(232847,aiOverride?"retail_stat_hp_half_fixture":"retail_stat_base_fixture");
        int previous=npc.GetLifeStats().GetMaxHp();
        Assert.Equal(aiOverride?116544:233088,previous);
        npc.GetLifeStats().SetCurrentHp(100);
        var entry=Enumerable.Range(0,npc.GetSkillList().Size()).Select(i=>npc.GetSkillList().GetSkillOnPosition(i))
            .First(x=>x.GetSkillId()==20701);
        var skill=DataManager.SKILL_DATA.GetSkillTemplate(20701);
        var buff=Assert.IsType<StatupEffect>(skill.GetEffectTemplate(1));
        var effect=new Effect(npc,npc,skill,entry.GetSkillLevel());effect.AddSuccessEffect(buff);effect.ApplyEffect();
        Assert.True(RetailNpcStatBuffs.HasSelectedProgram(effect));
        Assert.Equal(previous+2000,npc.GetLifeStats().GetMaxHp());
        Assert.Equal(expected,npc.GetLifeStats().GetCurrentHp());
        effect.EndEffect();
        Assert.Equal(previous,npc.GetLifeStats().GetMaxHp());
        Assert.Equal(100,npc.GetLifeStats().GetCurrentHp());
        Assert.Empty(h.Clock.Faults);
    }

    [SkippableFact]
    public void SelectedNpcKeepsNativeRoundingWhenCurrentHpBrieflyExceedsNewMaximum()
    {
        var (path,hash)=Fixture();
        using var installed=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(path),hash,Path.GetDirectoryName(path)!);
        using var h=BossAiHarness.For(301120000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var npc=h.SpawnWithAi(232847,"retail_stat_base_fixture");
        var high=new Owner();
        npc.GetGameStats().AddEffect(high,[new StatAddFunction(StatEnum.MAXHP,1807131033-233088,true)]);
        Assert.Equal(1807131033,npc.GetLifeStats().GetMaxHp());
        npc.GetLifeStats().SetCurrentHp(1807131033);
        var reduction=new Owner();
        npc.GetGameStats().AddEffect(reduction,[new StatAddFunction(StatEnum.MAXHP,1588920085-1807131033,true)]);
        Assert.Equal(1588920085,npc.GetLifeStats().GetMaxHp());
        Assert.Equal(1588920089,npc.GetLifeStats().GetCurrentHp());
        npc.GetGameStats().EndEffect(reduction);npc.GetGameStats().EndEffect(high);
        Assert.Empty(h.Clock.Faults);
    }
}
