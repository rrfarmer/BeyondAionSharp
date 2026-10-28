using System.Text.Json;
using System.Reflection;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Model.Templates.Items.Enums;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Condition;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Tests.Ai;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class RetailNpcCurseResourcePlanBuilderTests
{
    private sealed class PrimaryModifierOwner:IStatOwner;

    [SkippableTheory]
    [InlineData(340,PlayerClass.CHANTER,false,true)]
    [InlineData(344,PlayerClass.RIDER,false,true)]
    [InlineData(347,PlayerClass.GLADIATOR,true,false)]
    [InlineData(352,PlayerClass.SORCERER,false,true)]
    [InlineData(361,PlayerClass.BARD,false,true)]
    [InlineData(362,PlayerClass.PRIEST,true,true)]
    [InlineData(370,PlayerClass.TEMPLAR,true,false)]
    [InlineData(374,PlayerClass.SPIRIT_MASTER,false,true)]
    public void SelectedFlightPassiveFamilyFeedsPreparedCurse(int passiveId,PlayerClass playerClass,bool boostsHealth,bool boostsWill)
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        string? root=Environment.GetEnvironmentVariable("AION_RETAIL_PRIMARY_FLIGHT_FAMILY_ROOT");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(hash)||string.IsNullOrEmpty(root),"Requires pinned retail package and flight sources");
        byte[] flightSource=File.ReadAllBytes(Path.Combine(root!, $"skill-{passiveId}.utf16le.xml"));
        byte[] manifestBytes=File.ReadAllBytes(path!);using var manifest=JsonDocument.Parse(manifestBytes);
        var source=manifest.RootElement.GetProperty("skill_sources").EnumerateArray().Single(x=>x.GetProperty("id").GetInt32()==16596);
        var candidate=RetailNpcVampirePrimaryProjection.ReadForOfflineFixture(16596,File.ReadAllBytes(source.GetProperty("path").GetString()!));
        using var active=RetailNpcStatCatalog.InstallForTests(manifestBytes,hash!,Path.GetDirectoryName(path!)!);
        using var h=BossAiHarness.For(220050000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var npc=h.SpawnWithAi(214435,"retail_stat_base_fixture");var player=RetailNpcStatPeriodicTests.Player(h,220050000);
        player.GetCommonData().SetPlayerClass(playerClass);player.GetCommonData().SetLevel(65);
        player.GetGameStats().UpdateStatsTemplate();PlayerStatFunctions.AddPredefinedStatFunctions(player);player.GetLifeStats().OnStatsChange(null!);
        player.GetLifeStats().SetCurrentHp(player.GetLifeStats().GetMaxHp());player.GetLifeStats().SetCurrentMp(player.GetLifeStats().GetMaxMp());
        npc.GetLifeStats().SetCurrentHp(100);npc.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.MAGICAL_ACCURACY,10000)]);
        player.SetFlyState(FlyState.FLYING);player.SetFlyState(FlyState.GLIDING);
        var passiveTemplate=DataManager.SKILL_DATA.GetSkillTemplate(passiveId);var passive=new Effect(player,player,passiveTemplate,1);
        for(int position=1;position<=3;position++)passive.AddSuccessEffect(Assert.IsType<StatboostEffect>(passiveTemplate.GetEffectTemplate(position)));
        passive.ApplyEffect();player.GetLifeStats().OnStatsChange(null!);
        player.GetLifeStats().SetCurrentHp(player.GetLifeStats().GetMaxHp());player.GetLifeStats().SetCurrentMp(player.GetLifeStats().GetMaxMp());
        const uint nativeState=0x203;
        bool captured=RetailNpcPlayerPrimaryFlightCapture.TryCaptureForOfflineFixture(player,passive,flightSource,_=>nativeState,out var flight);
        var firstTemplate=passiveTemplate.GetEffectTemplate(1);var secondTemplate=passiveTemplate.GetEffectTemplate(2);var thirdTemplate=passiveTemplate.GetEffectTemplate(3);
        Assert.NotNull(firstTemplate);Assert.NotNull(secondTemplate);Assert.NotNull(thirdTemplate);
        string Details(StatEnum stat)=>string.Join(";",player.GetGameStats().GetStatsSorted(stat).Select(f=>$"{f.GetType().Name}/{f.GetValue()}/owner={ReferenceEquals(f.GetOwner(),passive)}/conditional={f.HasConditions()}"));
        Assert.True(captured,$"id={passiveId} active={player.GetEffectController().GetAllEffects().Contains(passive)} success={passive.IsInSuccessEffects(1)},{passive.IsInSuccessEffects(2)},{passive.IsInSuccessEffects(3)} noresist={firstTemplate.NoResist},{secondTemplate.NoResist},{thirdTemplate.NoResist} basic={firstTemplate.BasicLvl},{secondTemplate.BasicLvl},{thirdTemplate.BasicLvl} health=[{Details(StatEnum.HEALTH)}] will=[{Details(StatEnum.WILL)}] knowledge=[{Details(StatEnum.KNOWLEDGE)}]");Assert.NotNull(flight);
        var otherSource=File.ReadAllBytes(Path.Combine(root!, $"skill-{(passiveId==362?340:362)}.utf16le.xml"));
        Assert.False(RetailNpcPlayerPrimaryFlightCapture.TryCaptureForOfflineFixture(player,passive,otherSource,_=>nativeState,out _));
        Assert.True(RetailNpcCursePlayerPrimarySnapshot.TryCaptureRetailWithFoodForOfflineFixture(player,flight,out var primary));Assert.NotNull(primary);
        var initial=RetailNpcCurseStatMath.InitialClassPrimary(playerClass.GetClassId());
        Assert.Equal(RetailNpcCurseStatMath.InitialBase(RetailNpcCurseStatMath.Resource.Hp,65,(ushort)(initial.Health+(boostsHealth?10:0)),playerClass.GetClassId()),primary.HpBasis);
        Assert.Equal(RetailNpcCurseStatMath.InitialBase(RetailNpcCurseStatMath.Resource.Mp,65,(ushort)(initial.Will+(boostsWill?10:0)),playerClass.GetClassId()),primary.MpBasis);
        var effect=new Effect(new Skill(DataManager.SKILL_DATA.GetSkillTemplate(16596),npc,38,player,null!),player);
        RetailNpcCurseResourceOwner.Plan? selected=null;
        RetailNpcVampirePrimaryProjection.AttachOfflineInitialization(effect,candidate,(_,_)=>selected,(_,hi)=>hi);
        effect.Initialize();Assert.True(effect.IsInSuccessEffects(2));
        var mods=new RetailNpcCurseResourcePlanBuilder.ModifierInputs(0,0,p=>ReferenceEquals(p,player));
        Assert.True(RetailNpcCurseResourcePlanBuilder.TryBuild(player,effect,mods,null,_=>null,out selected,out _,out _,out _,fixtureFood:flight));Assert.NotNull(selected);
        Assert.Equal(primary.HpBasis,selected.HpBasis);Assert.Equal(primary.MpBasis,selected.MpBasis);
        int hostHp=player.GetLifeStats().GetMaxHp(),hostMp=player.GetLifeStats().GetMaxMp();
        int curseHp=RetailNpcCurseStatMath.Maximum(RetailNpcCurseStatMath.Resource.Hp,selected.HpBasis,selected.HpUnscaled,selected.HpOtherPercent-35);
        int curseMp=RetailNpcCurseStatMath.Maximum(RetailNpcCurseStatMath.Resource.Mp,selected.MpBasis,selected.MpUnscaled,selected.MpOtherPercent-35);
        effect.ApplyEffect();Assert.Equal(curseHp,player.GetLifeStats().GetMaxHp());Assert.Equal(curseMp,player.GetLifeStats().GetMaxMp());
        effect.EndEffect();Assert.Equal(hostHp,player.GetLifeStats().GetMaxHp());Assert.Equal(hostMp,player.GetLifeStats().GetMaxMp());
        passive.EndEffect();Assert.False(flight.IsCurrentForOfflineFixture(player));
        player.UnsetFlyState(FlyState.GLIDING);player.UnsetFlyState(FlyState.FLYING);
        h.Clock.Advance(TimeSpan.FromMilliseconds(1000));Assert.Empty(h.Clock.Faults);
    }

    [Fact]
    public void RetailFlightBonusUsesOnlyTheReviewedLowNibbleStates()
    {
        for(uint state=0;state<16;state++)
        {
            bool expected=state is 3 or 13 or 14;
            Assert.Equal(expected,RetailNpcPlayerPrimaryFlightCapture.NativeFlightBonusApplies(state));
            Assert.Equal(expected,RetailNpcPlayerPrimaryFlightCapture.NativeFlightBonusApplies(state|0x200));
        }
    }

    [SkippableTheory]
    [InlineData(false,0x201u,95,110)]
    [InlineData(true,0x203u,105,120)]
    [InlineData(false,0x203u,105,120)] // Deliberately synthetic mixed-state boundary.
    public void ReviewedFlightPassiveUsesExplicitNativeStateWhileHostIsGliding(bool wasFlying,uint selectedNativeState,int retailHealth,int retailWill)
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        string? sourcePath=Environment.GetEnvironmentVariable("AION_RETAIL_PRIMARY_FLIGHT_SOURCE");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(hash)||string.IsNullOrEmpty(sourcePath),"Requires pinned retail package and flight passive source");
        byte[] flightSource=File.ReadAllBytes(sourcePath!);var altered=(byte[])flightSource.Clone();altered[0]^=1;
        Assert.Throws<InvalidDataException>(()=>RetailNpcPlayerPrimaryFlightCapture.VerifySourceForOfflineFixture(altered));
        byte[] manifestBytes=File.ReadAllBytes(path!);using var manifest=JsonDocument.Parse(manifestBytes);
        var source=manifest.RootElement.GetProperty("skill_sources").EnumerateArray().Single(x=>x.GetProperty("id").GetInt32()==16596);
        var candidate=RetailNpcVampirePrimaryProjection.ReadForOfflineFixture(16596,File.ReadAllBytes(source.GetProperty("path").GetString()!));
        using var active=RetailNpcStatCatalog.InstallForTests(manifestBytes,hash!,Path.GetDirectoryName(path!)!);
        using var h=BossAiHarness.For(220050000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var npc=h.SpawnWithAi(214435,"retail_stat_base_fixture");var player=RetailNpcStatPeriodicTests.Player(h,220050000);
        player.GetCommonData().SetPlayerClass(PlayerClass.PRIEST);player.GetCommonData().SetLevel(65);
        player.GetGameStats().UpdateStatsTemplate();PlayerStatFunctions.AddPredefinedStatFunctions(player);player.GetLifeStats().OnStatsChange(null!);
        player.GetLifeStats().SetCurrentHp(player.GetLifeStats().GetMaxHp());player.GetLifeStats().SetCurrentMp(player.GetLifeStats().GetMaxMp());
        npc.GetLifeStats().SetCurrentHp(100);npc.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.MAGICAL_ACCURACY,10000)]);
        if(wasFlying)player.SetFlyState(FlyState.FLYING);
        player.SetFlyState(FlyState.GLIDING);Assert.True(player.IsFlying());Assert.Equal(wasFlying,player.IsInFlyingState());
        var passiveTemplate=DataManager.SKILL_DATA.GetSkillTemplate(362);var passive=new Effect(player,player,passiveTemplate,1);
        for(int position=1;position<=3;position++)passive.AddSuccessEffect(Assert.IsType<StatboostEffect>(passiveTemplate.GetEffectTemplate(position)));
        uint nativeState=selectedNativeState;
        Assert.False(RetailNpcPlayerPrimaryFlightCapture.TryCaptureForOfflineFixture(player,passive,flightSource,_=>nativeState,out _));
        passive.ApplyEffect();
        Assert.Equal(95+(wasFlying?10:0),player.GetGameStats().GetHealth().GetCurrent());
        Assert.Equal(100+(wasFlying?10:0),player.GetGameStats().GetWill().GetCurrent());
        player.GetLifeStats().OnStatsChange(null!);
        player.GetLifeStats().SetCurrentHp(player.GetLifeStats().GetMaxHp());player.GetLifeStats().SetCurrentMp(player.GetLifeStats().GetMaxMp());
        Assert.True(RetailNpcPlayerPrimaryFlightCapture.TryCaptureForOfflineFixture(player,passive,flightSource,_=>nativeState,out var flight));Assert.NotNull(flight);
        var unknownOwner=new PrimaryModifierOwner();
        var unknown=new StatAddFunction(StatEnum.WILL,1,true).WithConditions(new Conditions {conditions=[new OnFlyCondition()]});
        player.GetGameStats().AddEffectOnly(unknownOwner,[unknown]);Assert.False(flight.IsCurrentForOfflineFixture(player));
        player.GetGameStats().EndEffect(unknownOwner);Assert.True(flight.IsCurrentForOfflineFixture(player));
        Assert.False(RetailNpcCursePlayerPrimarySnapshot.TryCaptureRetailInitialForUnmodifiedHost(player,out _));
        Assert.True(RetailNpcCursePlayerPrimarySnapshot.TryCaptureRetailWithFoodForOfflineFixture(player,flight,out var primary));Assert.NotNull(primary);
        Assert.Equal(RetailNpcCurseStatMath.InitialBase(RetailNpcCurseStatMath.Resource.Hp,65,(ushort)retailHealth,player.GetPlayerClass().GetClassId()),primary.HpBasis);
        Assert.Equal(RetailNpcCurseStatMath.InitialBase(RetailNpcCurseStatMath.Resource.Mp,65,(ushort)retailWill,player.GetPlayerClass().GetClassId()),primary.MpBasis);
        var effect=new Effect(new Skill(DataManager.SKILL_DATA.GetSkillTemplate(16596),npc,38,player,null!),player);
        Assert.False(RetailNpcCurseStatWitness.Create(player,effect,_=>true,0,0).Before(player));
        RetailNpcCurseResourceOwner.Plan? selected=null;
        RetailNpcVampirePrimaryProjection.AttachOfflineInitialization(effect,candidate,(_,_)=>selected,(_,hi)=>hi);
        effect.Initialize();Assert.True(effect.IsInSuccessEffects(2));
        var mods=new RetailNpcCurseResourcePlanBuilder.ModifierInputs(0,0,p=>ReferenceEquals(p,player));
        Assert.True(RetailNpcCurseResourcePlanBuilder.TryBuild(player,effect,mods,null,_=>null,out selected,out _,out _,out _,fixtureFood:flight));Assert.NotNull(selected);
        Assert.Equal(primary.HpBasis,selected.HpBasis);Assert.Equal(primary.MpBasis,selected.MpBasis);
        Assert.True(selected.SnapshotCurrent(player));nativeState=selectedNativeState==0x201?0x203u:0x201u;
        Assert.False(selected.SnapshotCurrent(player));nativeState=selectedNativeState;
        Assert.True(selected.SnapshotCurrent(player));int hostHp=player.GetLifeStats().GetMaxHp(),hostMp=player.GetLifeStats().GetMaxMp();
        int curseHp=RetailNpcCurseStatMath.Maximum(RetailNpcCurseStatMath.Resource.Hp,selected.HpBasis,selected.HpUnscaled,selected.HpOtherPercent-35);
        int curseMp=RetailNpcCurseStatMath.Maximum(RetailNpcCurseStatMath.Resource.Mp,selected.MpBasis,selected.MpUnscaled,selected.MpOtherPercent-35);
        effect.ApplyEffect();Assert.Equal(curseHp,player.GetLifeStats().GetMaxHp());Assert.Equal(curseMp,player.GetLifeStats().GetMaxMp());
        effect.EndEffect();Assert.Equal(hostHp,player.GetLifeStats().GetMaxHp());Assert.Equal(hostMp,player.GetLifeStats().GetMaxMp());
        passive.EndEffect();Assert.False(flight.IsCurrentForOfflineFixture(player));player.UnsetFlyState(FlyState.GLIDING);player.UnsetFlyState(FlyState.FLYING);
        h.Clock.Advance(TimeSpan.FromMilliseconds(1000));Assert.Empty(h.Clock.Faults);
    }

    [SkippableFact]
    public void ReviewedAllPrimaryReductionFeedsBothRetailBasesAndRetiresWithItsEffect()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        string? foodPath=Environment.GetEnvironmentVariable("AION_RETAIL_PRIMARY_ALL_STATDOWN_SOURCE");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(hash)||string.IsNullOrEmpty(foodPath),"Requires pinned retail package and all-primary source");
        byte[] foodSource=File.ReadAllBytes(foodPath!);var altered=(byte[])foodSource.Clone();altered[0]^=1;
        Assert.Throws<InvalidDataException>(()=>RetailNpcPlayerPrimaryAllStatdownCapture.VerifySourceForOfflineFixture(altered));
        byte[] manifestBytes=File.ReadAllBytes(path!);using var manifest=JsonDocument.Parse(manifestBytes);
        var source=manifest.RootElement.GetProperty("skill_sources").EnumerateArray().Single(x=>x.GetProperty("id").GetInt32()==16596);
        var candidate=RetailNpcVampirePrimaryProjection.ReadForOfflineFixture(16596,File.ReadAllBytes(source.GetProperty("path").GetString()!));
        using var active=RetailNpcStatCatalog.InstallForTests(manifestBytes,hash!,Path.GetDirectoryName(path!)!);
        using var h=BossAiHarness.For(220050000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var npc=h.SpawnWithAi(214435,"retail_stat_base_fixture");var player=RetailNpcStatPeriodicTests.Player(h,220050000);
        player.GetCommonData().SetPlayerClass(PlayerClass.PRIEST);player.GetCommonData().SetLevel(65);
        player.GetGameStats().UpdateStatsTemplate();PlayerStatFunctions.AddPredefinedStatFunctions(player);player.GetLifeStats().OnStatsChange(null!);
        player.GetLifeStats().SetCurrentHp(player.GetLifeStats().GetMaxHp());player.GetLifeStats().SetCurrentMp(player.GetLifeStats().GetMaxMp());
        npc.GetLifeStats().SetCurrentHp(100);npc.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.MAGICAL_ACCURACY,10000)]);
        var foodTemplate=DataManager.SKILL_DATA.GetSkillTemplate(9942);var foodEffect=new Effect(player,player,foodTemplate,1);
        foodEffect.AddSuccessEffect(Assert.IsType<StatupEffect>(foodTemplate.GetEffectTemplate(1)));
        foodEffect.AddSuccessEffect(Assert.IsType<StatupEffect>(foodTemplate.GetEffectTemplate(2)));
        foodEffect.AddSuccessEffect(Assert.IsType<StatdownEffect>(foodTemplate.GetEffectTemplate(3)));
        Assert.False(RetailNpcPlayerPrimaryAllStatdownCapture.TryCaptureForOfflineFixture(player,foodEffect,foodSource,out _));
        foodEffect.ApplyEffect();
        Assert.Equal(95,player.GetGameStats().GetHealth().GetCurrent());Assert.Equal(100,player.GetGameStats().GetWill().GetCurrent());
        Assert.True(RetailNpcPlayerPrimaryAllStatdownCapture.TryCaptureForOfflineFixture(player,foodEffect,foodSource,out var food));Assert.NotNull(food);
        Assert.False(RetailNpcCursePlayerPrimarySnapshot.TryCaptureRetailInitialForUnmodifiedHost(player,out _));
        Assert.True(RetailNpcCursePlayerPrimarySnapshot.TryCaptureRetailWithFoodForOfflineFixture(player,food,out var primary));Assert.NotNull(primary);
        Assert.Equal(RetailNpcCurseStatMath.InitialBase(RetailNpcCurseStatMath.Resource.Hp,65,92,player.GetPlayerClass().GetClassId()),primary.HpBasis);
        Assert.Equal(RetailNpcCurseStatMath.InitialBase(RetailNpcCurseStatMath.Resource.Mp,65,107,player.GetPlayerClass().GetClassId()),primary.MpBasis);
        var effect=new Effect(new Skill(DataManager.SKILL_DATA.GetSkillTemplate(16596),npc,38,player,null!),player);
        RetailNpcCurseResourceOwner.Plan? selected=null;
        RetailNpcVampirePrimaryProjection.AttachOfflineInitialization(effect,candidate,(_,_)=>selected,(_,hi)=>hi);
        effect.Initialize();Assert.True(effect.IsInSuccessEffects(2));
        var mods=new RetailNpcCurseResourcePlanBuilder.ModifierInputs(0,0,p=>ReferenceEquals(p,player));
        Assert.False(RetailNpcCurseResourcePlanBuilder.TryBuild(player,effect,mods,null,_=>null,out _,out var missing,out _,out _));
        Assert.Equal(RetailNpcCurseResourcePlanBuilder.Failure.PrimaryInputUnavailable,missing);
        Assert.True(RetailNpcCurseResourcePlanBuilder.TryBuild(player,effect,mods,null,_=>null,out selected,out _,out _,out _,fixtureFood:food));Assert.NotNull(selected);
        Assert.Equal(primary.HpBasis,selected.HpBasis);Assert.Equal(primary.MpBasis,selected.MpBasis);
        int hostHp=player.GetLifeStats().GetMaxHp(),hostMp=player.GetLifeStats().GetMaxMp();
        int curseHp=RetailNpcCurseStatMath.Maximum(RetailNpcCurseStatMath.Resource.Hp,selected.HpBasis,selected.HpUnscaled,selected.HpOtherPercent-35);
        int curseMp=RetailNpcCurseStatMath.Maximum(RetailNpcCurseStatMath.Resource.Mp,selected.MpBasis,selected.MpUnscaled,selected.MpOtherPercent-35);
        effect.ApplyEffect();Assert.Equal(curseHp,player.GetLifeStats().GetMaxHp());Assert.Equal(curseMp,player.GetLifeStats().GetMaxMp());
        effect.EndEffect();Assert.Equal(hostHp,player.GetLifeStats().GetMaxHp());Assert.Equal(hostMp,player.GetLifeStats().GetMaxMp());
        foodEffect.EndEffect();Assert.False(food.IsCurrentForOfflineFixture(player));
        Assert.False(RetailNpcCursePlayerPrimarySnapshot.TryCaptureRetailWithFoodForOfflineFixture(player,food,out _));
        h.Clock.Advance(TimeSpan.FromMilliseconds(1000));Assert.Empty(h.Clock.Faults);
    }

    [SkippableFact]
    public void ReviewedWillReductionFeedsRetailPrimaryAndRetiresWithItsEffect()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        string? foodPath=Environment.GetEnvironmentVariable("AION_RETAIL_PRIMARY_STATDOWN_SOURCE");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(hash)||string.IsNullOrEmpty(foodPath),"Requires pinned retail package and statdown source");
        byte[] foodSource=File.ReadAllBytes(foodPath!);var altered=(byte[])foodSource.Clone();altered[0]^=1;
        Assert.Throws<InvalidDataException>(()=>RetailNpcPlayerPrimaryStatdownCapture.VerifySourceForOfflineFixture(altered));
        byte[] manifestBytes=File.ReadAllBytes(path!);using var manifest=JsonDocument.Parse(manifestBytes);
        var source=manifest.RootElement.GetProperty("skill_sources").EnumerateArray().Single(x=>x.GetProperty("id").GetInt32()==16596);
        var candidate=RetailNpcVampirePrimaryProjection.ReadForOfflineFixture(16596,File.ReadAllBytes(source.GetProperty("path").GetString()!));
        using var active=RetailNpcStatCatalog.InstallForTests(manifestBytes,hash!,Path.GetDirectoryName(path!)!);
        using var h=BossAiHarness.For(220050000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var npc=h.SpawnWithAi(214435,"retail_stat_base_fixture");var player=RetailNpcStatPeriodicTests.Player(h,220050000);
        player.GetCommonData().SetPlayerClass(PlayerClass.PRIEST);player.GetCommonData().SetLevel(65);
        player.GetGameStats().UpdateStatsTemplate();PlayerStatFunctions.AddPredefinedStatFunctions(player);player.GetLifeStats().OnStatsChange(null!);
        player.GetLifeStats().SetCurrentHp(player.GetLifeStats().GetMaxHp());player.GetLifeStats().SetCurrentMp(player.GetLifeStats().GetMaxMp());
        npc.GetLifeStats().SetCurrentHp(100);npc.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.MAGICAL_ACCURACY,10000)]);
        var foodTemplate=DataManager.SKILL_DATA.GetSkillTemplate(9840);var foodEffect=new Effect(player,player,foodTemplate,1);
        foodEffect.AddSuccessEffect(Assert.IsType<StatupEffect>(foodTemplate.GetEffectTemplate(1)));
        foodEffect.AddSuccessEffect(Assert.IsType<StatdownEffect>(foodTemplate.GetEffectTemplate(2)));
        Assert.False(RetailNpcPlayerPrimaryStatdownCapture.TryCaptureForOfflineFixture(player,foodEffect,foodSource,out _));
        foodEffect.ApplyEffect();
        Assert.Equal(95,player.GetGameStats().GetWill().GetCurrent());
        Assert.True(RetailNpcPlayerPrimaryStatdownCapture.TryCaptureForOfflineFixture(player,foodEffect,foodSource,out var food));Assert.NotNull(food);
        Assert.False(RetailNpcCursePlayerPrimarySnapshot.TryCaptureRetailInitialForUnmodifiedHost(player,out _));
        Assert.True(RetailNpcCursePlayerPrimarySnapshot.TryCaptureRetailWithFoodForOfflineFixture(player,food,out var primary));Assert.NotNull(primary);
        Assert.Equal(RetailNpcCurseStatMath.InitialBase(RetailNpcCurseStatMath.Resource.Mp,65,105,player.GetPlayerClass().GetClassId()),primary.MpBasis);
        var effect=new Effect(new Skill(DataManager.SKILL_DATA.GetSkillTemplate(16596),npc,38,player,null!),player);
        RetailNpcCurseResourceOwner.Plan? selected=null;
        RetailNpcVampirePrimaryProjection.AttachOfflineInitialization(effect,candidate,(_,_)=>selected,(_,hi)=>hi);
        effect.Initialize();Assert.True(effect.IsInSuccessEffects(2));
        var mods=new RetailNpcCurseResourcePlanBuilder.ModifierInputs(0,0,p=>ReferenceEquals(p,player));
        Assert.False(RetailNpcCurseResourcePlanBuilder.TryBuild(player,effect,mods,null,_=>null,out _,out var missing,out _,out _));
        Assert.Equal(RetailNpcCurseResourcePlanBuilder.Failure.PrimaryInputUnavailable,missing);
        Assert.True(RetailNpcCurseResourcePlanBuilder.TryBuild(player,effect,mods,null,_=>null,out selected,out _,out _,out _,fixtureFood:food));Assert.NotNull(selected);
        Assert.Equal(primary.MpBasis,selected.MpBasis);int hostHp=player.GetLifeStats().GetMaxHp(),hostMp=player.GetLifeStats().GetMaxMp();
        int curseMp=RetailNpcCurseStatMath.Maximum(RetailNpcCurseStatMath.Resource.Mp,selected.MpBasis,selected.MpUnscaled,selected.MpOtherPercent-35);
        effect.ApplyEffect();Assert.Equal(curseMp,player.GetLifeStats().GetMaxMp());
        effect.EndEffect();Assert.Equal(hostHp,player.GetLifeStats().GetMaxHp());Assert.Equal(hostMp,player.GetLifeStats().GetMaxMp());
        foodEffect.EndEffect();Assert.False(food.IsCurrentForOfflineFixture(player));
        Assert.False(RetailNpcCursePlayerPrimarySnapshot.TryCaptureRetailWithFoodForOfflineFixture(player,food,out _));
        h.Clock.Advance(TimeSpan.FromMilliseconds(1000));Assert.Empty(h.Clock.Faults);
    }

    [SkippableFact]
    public void ReviewedFoodFeedsRetailWillIntoPreparedCurseAndRetiresWithItsOwner()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        string? foodPath=Environment.GetEnvironmentVariable("AION_RETAIL_PRIMARY_FOOD_SOURCE");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(hash)||string.IsNullOrEmpty(foodPath),"Requires pinned retail package and food source");
        byte[] manifestBytes=File.ReadAllBytes(path!);using var manifest=JsonDocument.Parse(manifestBytes);
        var source=manifest.RootElement.GetProperty("skill_sources").EnumerateArray().Single(x=>x.GetProperty("id").GetInt32()==16596);
        var candidate=RetailNpcVampirePrimaryProjection.ReadForOfflineFixture(16596,File.ReadAllBytes(source.GetProperty("path").GetString()!));
        using var active=RetailNpcStatCatalog.InstallForTests(manifestBytes,hash!,Path.GetDirectoryName(path!)!);
        using var h=BossAiHarness.For(220050000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var npc=h.SpawnWithAi(214435,"retail_stat_base_fixture");var player=RetailNpcStatPeriodicTests.Player(h,220050000);
        player.GetCommonData().SetPlayerClass(PlayerClass.PRIEST);player.GetCommonData().SetLevel(65);
        player.GetGameStats().UpdateStatsTemplate();PlayerStatFunctions.AddPredefinedStatFunctions(player);player.GetLifeStats().OnStatsChange(null!);
        player.GetLifeStats().SetCurrentHp(player.GetLifeStats().GetMaxHp());player.GetLifeStats().SetCurrentMp(player.GetLifeStats().GetMaxMp());
        npc.GetLifeStats().SetCurrentHp(100);npc.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.MAGICAL_ACCURACY,10000)]);
        var foodTemplate=DataManager.SKILL_DATA.GetSkillTemplate(9839);var foodEffect=new Effect(player,player,foodTemplate,1);
        foodEffect.AddSuccessEffect(Assert.IsType<StatupEffect>(foodTemplate.GetEffectTemplate(1)));foodEffect.ApplyEffect();
        Assert.True(RetailNpcPlayerPrimaryFoodCapture.TryCaptureForOfflineFixture(player,foodEffect,File.ReadAllBytes(foodPath!),out var food));Assert.NotNull(food);
        Assert.False(RetailNpcCursePlayerPrimarySnapshot.TryCaptureRetailInitialForUnmodifiedHost(player,out _));
        Assert.True(RetailNpcCursePlayerPrimarySnapshot.TryCaptureRetailWithFoodForOfflineFixture(player,food,out var primary));Assert.NotNull(primary);
        Assert.Equal(RetailNpcCurseStatMath.InitialBase(RetailNpcCurseStatMath.Resource.Mp,65,115,player.GetPlayerClass().GetClassId()),primary.MpBasis);
        var skill=new Skill(DataManager.SKILL_DATA.GetSkillTemplate(16596),npc,38,player,null!);var effect=new Effect(skill,player);
        RetailNpcCurseResourceOwner.Plan? selected=null;
        RetailNpcVampirePrimaryProjection.AttachOfflineInitialization(effect,candidate,(_,_)=>selected,(_,hi)=>hi);
        effect.Initialize();Assert.True(effect.IsInSuccessEffects(2));
        var mods=new RetailNpcCurseResourcePlanBuilder.ModifierInputs(0,0,p=>ReferenceEquals(p,player));
        Assert.False(RetailNpcCurseResourcePlanBuilder.TryBuild(player,effect,mods,null,_=>null,out _,out var missing,out _,out _));
        Assert.Equal(RetailNpcCurseResourcePlanBuilder.Failure.PrimaryInputUnavailable,missing);
        Assert.True(RetailNpcCurseResourcePlanBuilder.TryBuild(player,effect,mods,null,_=>null,out selected,out _,out _,out _,fixtureFood:food));Assert.NotNull(selected);
        Assert.Equal(primary.MpBasis,selected.MpBasis);int oldHp=player.GetLifeStats().GetMaxHp(),oldMp=player.GetLifeStats().GetMaxMp();
        int curseMp=RetailNpcCurseStatMath.Maximum(RetailNpcCurseStatMath.Resource.Mp,selected.MpBasis,selected.MpUnscaled,selected.MpOtherPercent-35);
        effect.ApplyEffect();Assert.Equal(curseMp,player.GetLifeStats().GetMaxMp());
        effect.EndEffect();Assert.Equal(oldHp,player.GetLifeStats().GetMaxHp());Assert.Equal(oldMp,player.GetLifeStats().GetMaxMp());
        foodEffect.EndEffect();Assert.False(food.IsCurrentForOfflineFixture(player));
        Assert.False(RetailNpcCursePlayerPrimarySnapshot.TryCaptureRetailWithFoodForOfflineFixture(player,food,out _));
        h.Clock.Advance(TimeSpan.FromMilliseconds(1000));Assert.Empty(h.Clock.Faults);
    }
    [SkippableTheory]
    [InlineData(PlayerClass.MAGE,20,1,false)]
    [InlineData(PlayerClass.PRIEST,65,0,true)]
    public void ActualPlayerMaximumIsPreservedWhileRetailBasisControlsCurse(PlayerClass playerClass,int level,int expectedHpDifference,bool apply)
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(hash),"Requires pinned retail source package");
        byte[] manifestBytes=File.ReadAllBytes(path!);using var manifest=JsonDocument.Parse(manifestBytes);
        var source=manifest.RootElement.GetProperty("skill_sources").EnumerateArray().Single(x=>x.GetProperty("id").GetInt32()==16596);
        var candidate=RetailNpcVampirePrimaryProjection.ReadForOfflineFixture(16596,File.ReadAllBytes(source.GetProperty("path").GetString()!));
        using var active=RetailNpcStatCatalog.InstallForTests(manifestBytes,hash!,Path.GetDirectoryName(path!)!);
        using var h=BossAiHarness.For(220050000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var npc=h.SpawnWithAi(214435,"retail_stat_base_fixture");var player=RetailNpcStatPeriodicTests.Player(h,220050000);
        player.GetCommonData().SetPlayerClass(playerClass);player.GetCommonData().SetLevel(level);
        player.GetGameStats().UpdateStatsTemplate();PlayerStatFunctions.AddPredefinedStatFunctions(player);player.GetLifeStats().OnStatsChange(null!);
        player.GetLifeStats().SetCurrentHp(player.GetLifeStats().GetMaxHp());player.GetLifeStats().SetCurrentMp(player.GetLifeStats().GetMaxMp());
        npc.GetLifeStats().SetCurrentHp(100);npc.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.MAGICAL_ACCURACY,10000)]);
        int hostHp=player.GetLifeStats().GetMaxHp(),hostMp=player.GetLifeStats().GetMaxMp();
        Assert.True(RetailNpcCursePlayerPrimarySnapshot.TryCaptureRetailInitialForUnmodifiedHost(player,out var initial));Assert.NotNull(initial);
        Assert.Equal(expectedHpDifference,hostHp-initial.HpBasis);
        Assert.Empty(RetailNpcCurseEquipmentObservation.Capture(player).Items);
        var template=DataManager.SKILL_DATA.GetSkillTemplate(16596);var skill=new Skill(template,npc,38,player,null!);var effect=new Effect(skill,player);
        RetailNpcCurseResourceOwner.Plan? selected=null;
        RetailNpcVampirePrimaryProjection.AttachOfflineInitialization(effect,candidate,(_,_)=>selected,(_,hi)=>hi);
        effect.Initialize();Assert.True(effect.IsInSuccessEffects(2));
        bool modifiersCurrent=true;
        var mods=new RetailNpcCurseResourcePlanBuilder.ModifierInputs(0,0,p=>ReferenceEquals(p,player)&&modifiersCurrent);
        var primaryOwner=new PrimaryModifierOwner();
        player.GetGameStats().AddEffectOnly(primaryOwner,[new StatAddFunction(StatEnum.WILL,10,true)]);
        Assert.False(RetailNpcCurseResourcePlanBuilder.TryBuild(player,effect,mods,null,_=>null,out _,out var primaryFailure,out _,out _));
        Assert.Equal(RetailNpcCurseResourcePlanBuilder.Failure.PrimaryInputUnavailable,primaryFailure);
        player.GetGameStats().EndEffect(primaryOwner);
        Assert.True(RetailNpcCurseResourcePlanBuilder.TryBuild(player,effect,mods,null,_=>null,out selected,out var failure,out var equipmentFailure,out var failedItem));
        Assert.Equal(RetailNpcCurseResourcePlanBuilder.Failure.None,failure);
        Assert.Equal(RetailNpcCurseEquipmentObservation.ProjectionFailure.None,equipmentFailure);Assert.NotNull(selected);
        Assert.Equal(0,failedItem);
        Assert.Equal(hostHp,RetailNpcCurseStatMath.Maximum(RetailNpcCurseStatMath.Resource.Hp,selected.HpBasis,selected.HpUnscaled,selected.HpOtherPercent));
        Assert.Equal(hostMp,RetailNpcCurseStatMath.Maximum(RetailNpcCurseStatMath.Resource.Mp,selected.MpBasis,selected.MpUnscaled,selected.MpOtherPercent));
        Assert.Equal(initial.HpBasis,selected.HpBasis);Assert.Equal(initial.MpBasis,selected.MpBasis);
        Assert.True(selected.SnapshotCurrent(player));modifiersCurrent=false;Assert.False(selected.SnapshotCurrent(player));
        Assert.False(RetailNpcCurseResourcePlanBuilder.TryBuild(player,effect,mods,null,_=>null,out _,out var rejected,out _,out _));
        Assert.Equal(RetailNpcCurseResourcePlanBuilder.Failure.ModifierInputsUnavailable,rejected);modifiersCurrent=true;
        Assert.True(selected.SnapshotCurrent(player));
        if(apply)
        {
            // A constructed unreviewed item must not be treated as zero gear.
            var slots=Assert.IsType<SortedDictionary<long,Item>>(typeof(Equipment)
                .GetField("equipment",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(player.GetEquipment()));
            long main=ItemSlot.MAIN_HAND.GetSlotIdMask();
            var unknown=new Item(900999,new ItemTemplate {itemId=999999,itemGroup=ItemGroup.SWORD},1,true,main);
            slots.Add(main,unknown);
            Assert.False(selected.SnapshotCurrent(player));
            Assert.False(RetailNpcCurseResourcePlanBuilder.TryBuild(player,effect,mods,null,_=>null,out _,out var unsupported,out _,out var badItem));
            Assert.Equal(RetailNpcCurseResourcePlanBuilder.Failure.EquipmentUnsupported,unsupported);
            Assert.Equal(999999,badItem);
            slots.Remove(main);
            // Rebuild after the synthetic change; the point-in-time witness is not an ABA lock.
            Assert.True(RetailNpcCurseResourcePlanBuilder.TryBuild(player,effect,mods,null,_=>null,out selected,out _,out _,out _));
            Assert.NotNull(selected);
            int damage=effect.GetReserveds(1).GetValue();Assert.InRange(damage,1,player.GetLifeStats().GetCurrentHp()-1);
            int curseHp=RetailNpcCurseStatMath.Maximum(RetailNpcCurseStatMath.Resource.Hp,selected.HpBasis,selected.HpUnscaled,selected.HpOtherPercent-35);
            int curseMp=RetailNpcCurseStatMath.Maximum(RetailNpcCurseStatMath.Resource.Mp,selected.MpBasis,selected.MpUnscaled,selected.MpOtherPercent-35);
            effect.ApplyEffect();Assert.Equal(curseHp,player.GetLifeStats().GetMaxHp());Assert.Equal(curseMp,player.GetLifeStats().GetMaxMp());
            effect.EndEffect();Assert.Equal(hostHp,player.GetLifeStats().GetMaxHp());Assert.Equal(hostMp,player.GetLifeStats().GetMaxMp());
            h.Clock.Advance(TimeSpan.FromMilliseconds(1000));Assert.Empty(h.Clock.Faults);
        }
    }
}
