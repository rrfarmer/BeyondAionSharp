using System.Text.Json;
using System.Reflection;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Tests.Ai;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class RetailNpcCursePlayerPrimarySnapshotTests
{
    private sealed class Owner:IStatOwner;
    private sealed class UnreviewedReceipt:IRetailNpcPlayerPrimaryReceipt
    {
        public RetailNpcCurseStatMath.PrimaryModifier Modifier=>new(RetailNpcCurseStatMath.PrimaryAttribute.Will,0,1);
        public bool IsCurrentForOfflineFixture(Aion.GameServer.Model.GameObjects.Players.Player player)=>true;
        public bool MatchesHostPrimaries(Aion.GameServer.Model.GameObjects.Players.Player player,int health,int will)=>true;
    }

    [Fact]
    public void ArbitraryPrimaryReceiptIsRejected()
    {
        using var h=BossAiHarness.For(220050000).Build();
        var player=RetailNpcStatPeriodicTests.Player(h,220050000);
        player.GetLifeStats().SetCurrentHp(100);
        Assert.False(RetailNpcCursePlayerPrimarySnapshot.TryCaptureRetailWithFoodForOfflineFixture(player,new UnreviewedReceipt(),out _));
    }

    [Theory]
    [InlineData(StatEnum.ALLPARA)]
    [InlineData(StatEnum.STRVIT)]
    [InlineData(StatEnum.KNOWIL)]
    public void UnmodifiedHostRejectsCombinedPrimaryFunctionsEvenWhenHostValuesDoNotChange(StatEnum combined)
    {
        using var h=BossAiHarness.For(220050000).Build();
        var player=RetailNpcStatPeriodicTests.Player(h,220050000);
        player.GetCommonData().SetPlayerClass(PlayerClass.PRIEST);player.GetCommonData().SetLevel(38);
        player.GetGameStats().UpdateStatsTemplate();PlayerStatFunctions.AddPredefinedStatFunctions(player);
        player.GetLifeStats().SetCurrentHp(100);
        Assert.True(RetailNpcCursePlayerPrimarySnapshot.TryCaptureRetailInitialForUnmodifiedHost(player,out var initial));
        var owner=new Owner();player.GetGameStats().AddEffectOnly(owner,[new StatAddFunction(combined,5,true)]);
        Assert.Equal(player.GetPlayerClass().GetHealth(),player.GetGameStats().GetHealth().GetCurrent());
        Assert.Equal(player.GetPlayerClass().GetWill(),player.GetGameStats().GetWill().GetCurrent());
        Assert.False(initial!.HasEquivalentInputs(player));
        Assert.False(RetailNpcCursePlayerPrimarySnapshot.TryCaptureRetailInitialForUnmodifiedHost(player,out _));
        player.GetGameStats().EndEffect(owner);
    }

    [Theory]
    [InlineData(PlayerClass.PRIEST,100)]
    [InlineData(PlayerClass.ARTIST,105)]
    public void UnmodifiedHostUsesRetailInitialWillAndRejectsPrimaryModifiers(PlayerClass playerClass,int hostWill)
    {
        using var h=BossAiHarness.For(220050000).Build();
        var player=RetailNpcStatPeriodicTests.Player(h,220050000);
        player.GetCommonData().SetPlayerClass(playerClass);player.GetCommonData().SetLevel(38);
        player.GetGameStats().UpdateStatsTemplate();PlayerStatFunctions.AddPredefinedStatFunctions(player);
        player.GetLifeStats().SetCurrentHp(100);
        Assert.Equal(hostWill,player.GetGameStats().GetWill().GetCurrent());
        Assert.True(RetailNpcCursePlayerPrimarySnapshot.TryCapture(player,out var observed));Assert.NotNull(observed);
        Assert.True(RetailNpcCursePlayerPrimarySnapshot.TryCaptureRetailInitialForUnmodifiedHost(player,out var retail));Assert.NotNull(retail);
        Assert.Equal(RetailNpcCurseStatMath.InitialBase(RetailNpcCurseStatMath.Resource.Mp,38,110,playerClass.GetClassId()),retail.MpBasis);
        Assert.NotEqual(observed.MpBasis,retail.MpBasis);
        var owner=new Owner();player.GetGameStats().AddEffectOnly(owner,[new StatAddFunction(StatEnum.WILL,10,true)]);
        Assert.False(retail.HasEquivalentInputs(player));
        Assert.False(RetailNpcCursePlayerPrimarySnapshot.TryCaptureRetailInitialForUnmodifiedHost(player,out _));
        player.GetGameStats().EndEffect(owner);
        Assert.True(RetailNpcCursePlayerPrimarySnapshot.TryCaptureRetailInitialForUnmodifiedHost(player,out _));
    }

    [Fact]
    public void CurrentPlayerInputsExpireWhenPrimaryOrIdentityChanges()
    {
        using var h=BossAiHarness.For(220050000).Build();
        var p=RetailNpcStatPeriodicTests.Player(h,220050000);
        p.GetCommonData().SetPlayerClass(PlayerClass.PRIEST);p.GetCommonData().SetLevel(38);
        p.GetGameStats().UpdateStatsTemplate();PlayerStatFunctions.AddPredefinedStatFunctions(p);
        p.GetLifeStats().SetCurrentHp(100);
        Assert.True(RetailNpcCursePlayerPrimarySnapshot.TryCapture(p,out var captured));Assert.NotNull(captured);
        Assert.Equal((3127,4210),(captured.HpBasis,captured.MpBasis));Assert.True(captured.IsCurrent(p));
        var owner=new Owner();p.GetGameStats().AddEffectOnly(owner,[new StatAddFunction(StatEnum.WILL,10,true)]);
        Assert.False(captured.IsCurrent(p));Assert.False(captured.HasEquivalentInputs(p));
        Assert.True(RetailNpcCursePlayerPrimarySnapshot.TryCapture(p,out var updated));Assert.NotNull(updated);
        Assert.Equal(4270,updated.MpBasis);p.GetGameStats().EndEffect(owner);
        Assert.False(updated.IsCurrent(p));Assert.False(captured.IsCurrent(p));
        Assert.True(RetailNpcCursePlayerPrimarySnapshot.TryCapture(p,out var restored));Assert.NotNull(restored);
        p.GetCommonData().SetLevel(39);Assert.False(restored.IsCurrent(p));p.GetCommonData().SetLevel(38);
        p.GetCommonData().SetRace(Race.ELYOS);Assert.False(restored.IsCurrent(p));
        p.GetCommonData().SetRace(Race.ASMODIANS);
        var hp=typeof(CreatureLifeStats).GetField("currentHp",BindingFlags.Instance|BindingFlags.NonPublic)!;
        hp.SetValue(p.GetLifeStats(),0);Assert.False(RetailNpcCursePlayerPrimarySnapshot.TryCapture(p,out _));
        hp.SetValue(p.GetLifeStats(),100);
        p.GetCommonData().SetPlayerClass((PlayerClass)999);
        Assert.False(RetailNpcCursePlayerPrimarySnapshot.TryCapture(p,out _));p.GetCommonData().SetPlayerClass(PlayerClass.PRIEST);
    }

    [SkippableTheory]
    [InlineData(16596,214435,38,100)]
    [InlineData(18459,214583,41,130)]
    [InlineData(18460,214583,41,100)]
    public void ActualUnequippedPlayerInputsFeedSelectedVampireCurseFixture(int skillId,int npcId,int skillLevel,int healPercent)
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(hash),"Requires pinned retail source package");
        byte[] bytes=File.ReadAllBytes(path!);using var manifest=JsonDocument.Parse(bytes);
        var asset=manifest.RootElement.GetProperty("skill_sources").EnumerateArray().Single(x=>x.GetProperty("id").GetInt32()==skillId);
        var candidate=RetailNpcVampirePrimaryProjection.ReadForOfflineFixture(skillId,File.ReadAllBytes(asset.GetProperty("path").GetString()!));
        using var active=RetailNpcStatCatalog.InstallForTests(bytes,hash!,Path.GetDirectoryName(path!)!);
        using var h=BossAiHarness.For(220050000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var npc=h.SpawnWithAi(npcId,"retail_stat_base_fixture");
        var player=RetailNpcStatPeriodicTests.Player(h,220050000);
        player.GetCommonData().SetPlayerClass(PlayerClass.PRIEST);player.GetCommonData().SetLevel(65);
        player.GetGameStats().UpdateStatsTemplate();PlayerStatFunctions.AddPredefinedStatFunctions(player);
        player.GetLifeStats().OnStatsChange(null!);
        player.GetLifeStats().SetCurrentHp(player.GetLifeStats().GetMaxHp());
        player.GetLifeStats().SetCurrentMp(player.GetLifeStats().GetMaxMp());
        npc.GetLifeStats().SetCurrentHp(100);
        npc.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.MAGICAL_ACCURACY,10000)]);
        Assert.True(RetailNpcCursePlayerPrimarySnapshot.TryCapture(player,out var primary));Assert.NotNull(primary);
        Assert.Equal((player.GetLifeStats().GetMaxHp(),player.GetLifeStats().GetMaxMp()),(primary.HpBasis,primary.MpBasis));
        var equipment=RetailNpcCurseEquipmentObservation.Capture(player);Assert.Empty(equipment.Items);
        var equipmentWitness=RetailNpcCurseEquipmentObservation.EquipmentWitness(player,equipment,RetailNpcCurseEquipmentObservation.Capture);
        int newHp=RetailNpcCurseStatMath.Maximum(RetailNpcCurseStatMath.Resource.Hp,primary.HpBasis,primary.HpBasis,-35);
        int newMp=RetailNpcCurseStatMath.Maximum(RetailNpcCurseStatMath.Resource.Mp,primary.MpBasis,primary.MpBasis,-35);
        var template=DataManager.SKILL_DATA.GetSkillTemplate(skillId);
        var skill=new Skill(template,npc,skillLevel,player,null!);var effect=new Effect(skill,player);
        var witness=RetailNpcCurseStatWitness.Create(player,effect,p=>equipmentWitness(p)&&primary.HasEquivalentInputs(p),
            newHp-primary.HpBasis,newMp-primary.MpBasis);
        Assert.True(witness.Before(player));
        var plan=new RetailNpcCurseResourceOwner.Plan(primary.HpBasis,primary.HpBasis,0,primary.MpBasis,primary.MpBasis,0,
            witness.Before,witness.After,witness.CommitStamp);
        RetailNpcVampirePrimaryProjection.AttachOfflineInitialization(effect,candidate,plan,(_,hi)=>hi);
        effect.Initialize();Assert.True(effect.IsInSuccessEffects(2));
        int damage=effect.GetReserveds(1).GetValue();Assert.InRange(damage,1,primary.HpBasis-1);
        effect.ApplyEffect();Assert.Equal(newHp,player.GetLifeStats().GetMaxHp());Assert.Equal(newMp,player.GetLifeStats().GetMaxMp());
        Assert.False(primary.IsCurrent(player));Assert.True(primary.HasEquivalentInputs(player));
        h.Clock.Advance(TimeSpan.FromMilliseconds(1000));
        Assert.Equal(Math.Min(npc.GetLifeStats().GetMaxHp(),100+RetailNpcStatSpellDrain.HealAmount(damage,healPercent)),npc.GetLifeStats().GetCurrentHp());
        effect.EndEffect();Assert.Equal(primary.HpBasis,player.GetLifeStats().GetMaxHp());Assert.Equal(primary.MpBasis,player.GetLifeStats().GetMaxMp());
        Assert.Empty(h.Clock.Faults);
    }
}
