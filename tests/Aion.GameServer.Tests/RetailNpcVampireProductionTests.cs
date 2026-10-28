using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Tests.Ai;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class RetailNpcVampireProductionTests
{
    [SkippableFact]
    public void NormalVampireCastUsesEachSelectedStoneSkinRank()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_VAMPIRE_FAMILY_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_VAMPIRE_FAMILY_SHA256");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(hash),"Requires the selected vampire and shield-family package");
        using var active=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(path!),hash!,Path.GetDirectoryName(path!)!);
        using var h=BossAiHarness.For(220050000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var npc=h.SpawnWithAi(214435,"retail_stat_base_fixture");var player=RetailNpcStatPeriodicTests.Player(h,220050000);
        player.GetCommonData().SetPlayerClass(PlayerClass.PRIEST);player.GetCommonData().SetLevel(npc.GetLevel());
        player.GetGameStats().UpdateStatsTemplate();PlayerStatFunctions.AddPredefinedStatFunctions(player);
        player.GetLifeStats().OnStatsChange(null!);player.GetLifeStats().SetCurrentHp(player.GetLifeStats().GetMaxHp());
        player.GetLifeStats().SetCurrentMp(player.GetLifeStats().GetMaxMp());
        player.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.REGEN_HP,0)]);
        npc.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.MAGICAL_ACCURACY,10000),
            new StatSetFunction(StatEnum.CURSE_RESISTANCE_PENETRATION,1000),new StatSetFunction(StatEnum.MAGICAL_CRITICAL,0),
            new StatSetFunction(StatEnum.REGEN_HP,0)]);
        BossAiHarness.MakeMutuallyKnown(npc,player);
        var catalog=Assert.IsType<RetailNpcPlayerShieldCatalog>(RetailNpcStatCatalog.PlayerShieldSources);
        var probeTemplate=DataManager.SKILL_DATA.GetSkillTemplate(17900);
        for(int id=1155;id<=1166;id++)
        {
            player.GetLifeStats().SetCurrentHp(player.GetLifeStats().GetMaxHp());
            player.GetLifeStats().SetCurrentMp(player.GetLifeStats().GetMaxMp());npc.GetLifeStats().SetCurrentHp(100);
            int maxHp=player.GetLifeStats().GetMaxHp();var source=Assert.IsType<RetailNpcPlayerShieldSource>(catalog.Find(id));
            var template=DataManager.SKILL_DATA.GetSkillTemplate(id);
            var shield=new Effect(new Skill(template,player,1,player,null!),player);
            shield.AddAllEffectToSucess();shield.AddToEffectedController();
            var probe=new Effect(new Skill(probeTemplate,npc,37,player,null!),player);
            Assert.True(player.GetObserveController().TryCalculatePlayerRetailNpcDrain(probe,player,source.InitialPool-1,AttackStatus.NORMALHIT,out _));
            Assert.Equal(0,probe.GetReserveds(1).GetValue());Assert.False(shield.RetailHasEnded);
            var skill=Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(npc,16596,38,player);
            Assert.NotNull(skill);skill.GetEffectedList().Add(player);Assert.True(skill.UseWithoutPropSkill());
            Assert.True(RetailNpcVampireCastFixture.IsSelectedForOfflineFixture(skill));
            int castMs=(int)MathF.Floor(skill.GetSkillTemplate().GetDuration()*(npc.GetGameStats().GetCastSpeed()/1000f)+0.5f);
            h.Clock.Advance(TimeSpan.FromMilliseconds(castMs));
            var effect=RetailNpcVampireCastFixture.ObservedEffectForOfflineFixture(skill);Assert.NotNull(effect);
            Assert.True(effect.IsInSuccessEffects(1));Assert.True(effect.IsInSuccessEffects(2));
            Assert.Equal(ShieldType.NORMAL.GetId(),effect.GetShieldDefense());Assert.True(shield.RetailHasEnded);
            int damage=effect.GetReserveds(1).GetValue(),impact=Math.Max(skill.GetHitTime()-50,100);
            h.Clock.Advance(TimeSpan.FromMilliseconds(impact-1));int before=player.GetLifeStats().GetCurrentHp();
            Assert.InRange(damage,1,before-1);h.Clock.Advance(TimeSpan.FromMilliseconds(1));
            Assert.Equal(before-damage,player.GetLifeStats().GetCurrentHp());
            h.Clock.Advance(TimeSpan.FromMilliseconds(skill.GetHitTime()-impact));
            Assert.True(player.GetEffectController().IsInAnyAbnormalState(AbnormalState.CURSE));
            Assert.Equal(RetailNpcCurseStatMath.CurrentMode2(RetailNpcCurseStatMath.Resource.Hp,
                maxHp,player.GetLifeStats().GetMaxHp(),before-damage),player.GetLifeStats().GetCurrentHp());
            h.Clock.Advance(TimeSpan.FromMilliseconds(1000));
            Assert.Equal(Math.Min(npc.GetLifeStats().GetMaxHp(),100+RetailNpcStatSpellDrain.HealAmount(damage,100)),npc.GetLifeStats().GetCurrentHp());
            effect.EndEffect();Assert.Equal(maxHp,player.GetLifeStats().GetMaxHp());
        }
        Assert.Empty(h.Clock.Faults);
    }

    [SkippableFact]
    public void ResourcePlanRetiringDuringCurseRegistrationDoesNotFaultTheTimer()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_VAMPIRE_SHIELD_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_VAMPIRE_SHIELD_SHA256");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(hash),"Requires selected vampire package");
        using var active=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(path!),hash!,Path.GetDirectoryName(path!)!);
        using var h=BossAiHarness.For(220050000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var npc=h.SpawnWithAi(214435,"retail_stat_base_fixture");var player=RetailNpcStatPeriodicTests.Player(h,220050000);
        player.GetCommonData().SetPlayerClass(PlayerClass.PRIEST);player.GetCommonData().SetLevel(npc.GetLevel());
        player.GetGameStats().UpdateStatsTemplate();PlayerStatFunctions.AddPredefinedStatFunctions(player);
        player.GetLifeStats().OnStatsChange(null!);player.GetLifeStats().SetCurrentHp(player.GetLifeStats().GetMaxHp());
        player.GetLifeStats().SetCurrentMp(player.GetLifeStats().GetMaxMp());npc.GetLifeStats().SetCurrentHp(100);
        npc.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.MAGICAL_ACCURACY,10000),
            new StatSetFunction(StatEnum.CURSE_RESISTANCE_PENETRATION,1000),new StatSetFunction(StatEnum.MAGICAL_CRITICAL,0)]);
        BossAiHarness.MakeMutuallyKnown(npc,player);
        var skill=Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(npc,16596,38,player);
        Assert.NotNull(skill);skill.GetEffectedList().Add(player);
        Assert.True(RetailNpcStatCatalog.TryGetReviewedVampireProjection(npc,16596,38,out var candidate));
        RetailNpcCurseResourceOwner.Plan? Retired(Player ignoredPlayer,Effect ignoredEffect) => null;
        RetailNpcVampireCastFixture.AttachFromCatalogForOfflineFixture(skill,candidate,Retired,(_,hi)=>hi,
            preflight:(_,_)=>true);
        Assert.True(skill.UseWithoutPropSkill());
        int castMs=(int)MathF.Floor(skill.GetSkillTemplate().GetDuration()*(npc.GetGameStats().GetCastSpeed()/1000f)+0.5f);
        h.Clock.Advance(TimeSpan.FromMilliseconds(castMs+skill.GetHitTime()));
        var effect=RetailNpcVampireCastFixture.ObservedEffectForOfflineFixture(skill);Assert.NotNull(effect);
        int damage=effect.GetReserveds(1).GetValue();Assert.True(damage>0);
        Assert.True(effect.RetailHasEnded);Assert.Null(player.GetEffectController().FindBySkillId(16596));
        Assert.False(player.GetEffectController().IsInAnyAbnormalState(AbnormalState.CURSE));
        h.Clock.Advance(TimeSpan.FromMilliseconds(1000));
        Assert.Equal(Math.Min(npc.GetLifeStats().GetMaxHp(),100+RetailNpcStatSpellDrain.HealAmount(damage,100)),npc.GetLifeStats().GetCurrentHp());
        Assert.Empty(h.Clock.Faults);
    }

    [SkippableFact]
    public void LateUnsupportedPlayerInputPreservesExistingCurseAndDoesNotLoseTheHit()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_VAMPIRE_SHIELD_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_VAMPIRE_SHIELD_SHA256");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(hash),"Requires selected vampire package");
        using var active=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(path!),hash!,Path.GetDirectoryName(path!)!);
        using var h=BossAiHarness.For(220050000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var npc=h.SpawnWithAi(214435,"retail_stat_base_fixture");var player=RetailNpcStatPeriodicTests.Player(h,220050000);
        player.GetCommonData().SetPlayerClass(PlayerClass.PRIEST);player.GetCommonData().SetLevel(npc.GetLevel());
        player.GetGameStats().UpdateStatsTemplate();PlayerStatFunctions.AddPredefinedStatFunctions(player);
        player.GetLifeStats().OnStatsChange(null!);player.GetLifeStats().SetCurrentHp(player.GetLifeStats().GetMaxHp());
        player.GetLifeStats().SetCurrentMp(player.GetLifeStats().GetMaxMp());npc.GetLifeStats().SetCurrentHp(100);
        npc.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.MAGICAL_ACCURACY,10000),
            new StatSetFunction(StatEnum.CURSE_RESISTANCE_PENETRATION,1000),new StatSetFunction(StatEnum.MAGICAL_CRITICAL,0)]);
        BossAiHarness.MakeMutuallyKnown(npc,player);
        Skill Begin(out int castMs)
        {
            var skill=Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(npc,16596,38,player);
            Assert.NotNull(skill);skill.GetEffectedList().Add(player);Assert.True(skill.UseWithoutPropSkill());
            castMs=(int)MathF.Floor(skill.GetSkillTemplate().GetDuration()*(npc.GetGameStats().GetCastSpeed()/1000f)+0.5f);
            return skill;
        }
        var first=Begin(out int castFirst);h.Clock.Advance(TimeSpan.FromMilliseconds(castFirst+first.GetHitTime()));
        var old=RetailNpcVampireCastFixture.ObservedEffectForOfflineFixture(first);Assert.NotNull(old);
        Assert.True(player.GetEffectController().IsInAnyAbnormalState(AbnormalState.CURSE));
        int firstDamage=old.GetReserveds(1).GetValue();
        var second=Begin(out int castSecond);h.Clock.Advance(TimeSpan.FromMilliseconds(castSecond));
        var replacement=RetailNpcVampireCastFixture.ObservedEffectForOfflineFixture(second);Assert.NotNull(replacement);
        Assert.True(replacement.IsInSuccessEffects(1));Assert.True(replacement.IsInSuccessEffects(2));
        int damage=replacement.GetReserveds(1).GetValue(),impact=Math.Max(second.GetHitTime()-50,100);
        h.Clock.Advance(TimeSpan.FromMilliseconds(impact-1));int before=player.GetLifeStats().GetCurrentHp();
        Assert.InRange(damage,1,before-1);h.Clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(before-damage,player.GetLifeStats().GetCurrentHp());
        player.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.ABNORMAL_RESISTANCE_ALL,100)]);
        h.Clock.Advance(TimeSpan.FromMilliseconds(second.GetHitTime()-impact));
        Assert.False(old.RetailHasEnded);Assert.Same(old,player.GetEffectController().FindBySkillId(16596));
        Assert.True(player.GetEffectController().IsInAnyAbnormalState(AbnormalState.CURSE));
        h.Clock.Advance(TimeSpan.FromMilliseconds(1000));
        Assert.Equal(Math.Min(npc.GetLifeStats().GetMaxHp(),100+RetailNpcStatSpellDrain.HealAmount(firstDamage,100)
            +RetailNpcStatSpellDrain.HealAmount(damage,100)),npc.GetLifeStats().GetCurrentHp());
        Assert.Empty(h.Clock.Faults);old.EndEffect();
    }

    [SkippableFact]
    public void PriorPackageDoesNotSelectUnconfiguredVampireInputs()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(hash),"Requires prior retail package");
        using var active=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(path!),hash!,Path.GetDirectoryName(path!)!);
        using var h=BossAiHarness.For(220050000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var npc=h.SpawnWithAi(214435,"retail_stat_base_fixture");var player=RetailNpcStatPeriodicTests.Player(h,220050000);
        var skill=Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(npc,16596,38,player);
        Assert.NotNull(skill);skill.GetEffectedList().Add(player);BossAiHarness.MakeMutuallyKnown(npc,player);
        Assert.True(skill.UseWithoutPropSkill());Assert.False(RetailNpcVampireCastFixture.IsSelectedForOfflineFixture(skill));
    }

    [SkippableFact]
    public void CandidateDoesNotAdmitTheAreaSkill()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_VAMPIRE_SHIELD_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_VAMPIRE_SHIELD_SHA256");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(hash),"Requires selected vampire package");
        using var active=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(path!),hash!,Path.GetDirectoryName(path!)!);
        using var h=BossAiHarness.For(220050000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var npc=h.SpawnWithAi(214583,"retail_stat_base_fixture");var player=RetailNpcStatPeriodicTests.Player(h,220050000);
        var skill=Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(npc,18460,41,player);
        Assert.NotNull(skill);skill.GetEffectedList().Add(player);BossAiHarness.MakeMutuallyKnown(npc,player);
        Assert.True(skill.UseWithoutPropSkill());Assert.False(RetailNpcVampireCastFixture.IsSelectedForOfflineFixture(skill));
    }

    [SkippableFact]
    public void RetiredTargetIsDeclinedWithoutInitializingThePreparedCast()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_VAMPIRE_SHIELD_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_VAMPIRE_SHIELD_SHA256");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(hash),"Requires selected vampire package");
        using var active=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(path!),hash!,Path.GetDirectoryName(path!)!);
        using var h=BossAiHarness.For(220050000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var npc=h.SpawnWithAi(214435,"retail_stat_base_fixture");var player=RetailNpcStatPeriodicTests.Player(h,220050000);
        player.GetLifeStats().SetCurrentHp(player.GetLifeStats().GetMaxHp());
        var skill=Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(npc,16596,38,player);
        Assert.NotNull(skill);skill.GetEffectedList().Add(player);BossAiHarness.MakeMutuallyKnown(npc,player);
        Assert.True(skill.UseWithoutPropSkill());Assert.True(RetailNpcVampireCastFixture.IsSelectedForOfflineFixture(skill));
        var conflict=new Effect(skill,player);
        RetailNpcVampireCastFixture.BeforeInitializationForOfflineFixture(skill,conflict);
        conflict.SetEffectResult(EffectResult.CONFLICT);conflict.Initialize();
        RetailNpcVampireCastFixture.AfterInitializationForOfflineFixture(skill,conflict);
        Assert.False(conflict.IsInSuccessEffects(1));
        h.World.Despawn(player);
        var effect=new Effect(skill,player);
        RetailNpcVampireCastFixture.BeforeInitializationForOfflineFixture(skill,effect);
        effect.Initialize();RetailNpcVampireCastFixture.AfterInitializationForOfflineFixture(skill,effect);
        Assert.False(effect.IsInSuccessEffects(1));Assert.Empty(h.Clock.Faults);
    }

    [SkippableTheory]
    [InlineData(16596,214435,38,100)]
    [InlineData(18459,214583,41,130)]
    public void SelectedSingleTargetCastUsesRetailDamageCurseAndHealing(int skillId,int npcId,int level,int healPercent)
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_VAMPIRE_SHIELD_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_VAMPIRE_SHIELD_SHA256");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(hash),"Requires selected vampire package");
        using var active=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(path!),hash!,Path.GetDirectoryName(path!)!);
        using var h=BossAiHarness.For(220050000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var npc=h.SpawnWithAi(npcId,"retail_stat_base_fixture");var player=RetailNpcStatPeriodicTests.Player(h,220050000);
        player.GetCommonData().SetPlayerClass(PlayerClass.PRIEST);player.GetCommonData().SetLevel(npc.GetLevel());
        player.GetGameStats().UpdateStatsTemplate();PlayerStatFunctions.AddPredefinedStatFunctions(player);
        player.GetLifeStats().OnStatsChange(null!);player.GetLifeStats().SetCurrentHp(player.GetLifeStats().GetMaxHp());
        player.GetLifeStats().SetCurrentMp(player.GetLifeStats().GetMaxMp());
        npc.GetLifeStats().SetCurrentHp(100);npc.GetGameStats().AddEffectOnly(null!,
            [new StatSetFunction(StatEnum.MAGICAL_ACCURACY,10000),new StatSetFunction(StatEnum.CURSE_RESISTANCE_PENETRATION,1000)]);
        BossAiHarness.MakeMutuallyKnown(npc,player);int maxHp=player.GetLifeStats().GetMaxHp();
        var skill=Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(npc,skillId,level,player);
        Assert.NotNull(skill);skill.GetEffectedList().Add(player);
        Assert.Null(RetailNpcVampireCastFixture.ObservedEffectForOfflineFixture(skill));
        Assert.True(skill.UseWithoutPropSkill());Assert.True(RetailNpcVampireCastFixture.IsSelectedForOfflineFixture(skill));
        int castMs=(int)MathF.Floor(skill.GetSkillTemplate().GetDuration()*(npc.GetGameStats().GetCastSpeed()/1000f)+0.5f);
        h.Clock.Advance(TimeSpan.FromMilliseconds(castMs));
        var effect=RetailNpcVampireCastFixture.ObservedEffectForOfflineFixture(skill);Assert.NotNull(effect);
        Assert.True(RetailNpcVampirePrimaryProjection.TryGetOfflineInitialization(effect,out bool handled,out bool success));
        Assert.True(handled,$"admission declined: result={effect.GetEffectResult()} status={effect.GetAttackStatus()}");
        Assert.True(success,$"primary failed: result={effect.GetEffectResult()} status={effect.GetAttackStatus()}");
        Assert.True(effect.IsInSuccessEffects(1),"primary was not accepted");
        Assert.True(effect.IsInSuccessEffects(2),$"curse was not accepted: result={effect.GetEffectResult()} status={effect.GetAttackStatus()}");
        int damage=effect.GetReserveds(1).GetValue(),impact=Math.Max(skill.GetHitTime()-50,100);
        h.Clock.Advance(TimeSpan.FromMilliseconds(impact-1));int before=player.GetLifeStats().GetCurrentHp();
        Assert.InRange(damage,1,before-1);h.Clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(before-damage,player.GetLifeStats().GetCurrentHp());
        h.Clock.Advance(TimeSpan.FromMilliseconds(skill.GetHitTime()-impact));
        Assert.True(player.GetEffectController().IsInAnyAbnormalState(AbnormalState.CURSE));
        Assert.Equal(RetailNpcCurseStatMath.CurrentMode2(RetailNpcCurseStatMath.Resource.Hp,
            maxHp,player.GetLifeStats().GetMaxHp(),before-damage),player.GetLifeStats().GetCurrentHp());
        h.Clock.Advance(TimeSpan.FromMilliseconds(1000));
        Assert.Equal(Math.Min(npc.GetLifeStats().GetMaxHp(),100+RetailNpcStatSpellDrain.HealAmount(damage,healPercent)),npc.GetLifeStats().GetCurrentHp());
        Assert.Empty(h.Clock.Faults);effect.EndEffect();
    }

    [SkippableFact]
    public void SelectedCastDeclinesAnUnsupportedPlayerBeforeDamage()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_VAMPIRE_SHIELD_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_VAMPIRE_SHIELD_SHA256");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(hash),"Requires selected vampire package");
        using var active=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(path!),hash!,Path.GetDirectoryName(path!)!);
        using var h=BossAiHarness.For(220050000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var npc=h.SpawnWithAi(214435,"retail_stat_base_fixture");var player=RetailNpcStatPeriodicTests.Player(h,220050000);
        player.GetCommonData().SetPlayerClass(PlayerClass.PRIEST);player.GetCommonData().SetLevel(65);
        player.GetGameStats().UpdateStatsTemplate();PlayerStatFunctions.AddPredefinedStatFunctions(player);
        player.GetLifeStats().OnStatsChange(null!);player.GetLifeStats().SetCurrentHp(player.GetLifeStats().GetMaxHp());
        player.GetLifeStats().SetCurrentMp(player.GetLifeStats().GetMaxMp());npc.GetLifeStats().SetCurrentHp(100);
        player.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.ABNORMAL_RESISTANCE_ALL,100)]);
        BossAiHarness.MakeMutuallyKnown(npc,player);int hp=player.GetLifeStats().GetCurrentHp();
        var skill=Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(npc,16596,38,player);
        Assert.NotNull(skill);skill.GetEffectedList().Add(player);Assert.True(skill.UseWithoutPropSkill());
        int castMs=(int)MathF.Floor(skill.GetSkillTemplate().GetDuration()*(npc.GetGameStats().GetCastSpeed()/1000f)+0.5f);
        h.Clock.Advance(TimeSpan.FromMilliseconds(castMs+skill.GetHitTime()+1000));
        var effect=RetailNpcVampireCastFixture.ObservedEffectForOfflineFixture(skill);Assert.NotNull(effect);
        Assert.False(effect.IsInSuccessEffects(1));Assert.Equal(hp,player.GetLifeStats().GetCurrentHp());
        Assert.Equal(100,npc.GetLifeStats().GetCurrentHp());Assert.Empty(h.Clock.Faults);
    }
}
