using Aion.GameServer.Controllers.Attack;
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
public sealed class RetailNpcVampireShieldTests
{
    private sealed class UnreviewedOwner : IStatOwner { }

    [SkippableFact]
    public void LateStoneSkinDoesNotCancelDelayedVampireHitOrConsumeShield()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_VAMPIRE_SHIELD_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_VAMPIRE_SHIELD_SHA256");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(hash),"Requires source-checked vampire and shield package");
        using var active=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(path!),hash!,Path.GetDirectoryName(path!)!);
        using var h=BossAiHarness.For(220050000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var npc=h.SpawnWithAi(214435,"retail_stat_base_fixture");var player=RetailNpcStatPeriodicTests.Player(h,220050000);
        player.GetCommonData().SetPlayerClass(PlayerClass.PRIEST);player.GetCommonData().SetLevel(65);
        player.GetGameStats().UpdateStatsTemplate();PlayerStatFunctions.AddPredefinedStatFunctions(player);
        player.GetLifeStats().OnStatsChange(null!);player.GetLifeStats().SetCurrentHp(player.GetLifeStats().GetMaxHp());
        player.GetLifeStats().SetCurrentMp(player.GetLifeStats().GetMaxMp());
        int hp=player.GetLifeStats().GetCurrentHp(),maxHp=player.GetLifeStats().GetMaxHp();npc.GetLifeStats().SetCurrentHp(100);
        npc.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.MAGICAL_ACCURACY,10000)]);
        BossAiHarness.MakeMutuallyKnown(npc,player);
        var skill=Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(npc,16596,38,player);
        Assert.NotNull(skill);skill.GetEffectedList().Add(player);
        RetailNpcVampireCastFixture.AttachSelectedInputsForOfflineFixture(skill,(_,hi)=>hi);
        int castMs=(int)MathF.Floor(skill.GetSkillTemplate().GetDuration()*(npc.GetGameStats().GetCastSpeed()/1000f)+0.5f);
        Assert.True(skill.UseWithoutPropSkill());h.Clock.Advance(TimeSpan.FromMilliseconds(castMs));
        var effect=RetailNpcVampireCastFixture.ObservedEffectForOfflineFixture(skill);Assert.NotNull(effect);
        int damage=effect.GetReserveds(1).GetValue();Assert.InRange(damage,1,hp-1);
        var shieldTemplate=DataManager.SKILL_DATA.GetSkillTemplate(1165);
        var lateShield=new Effect(new Skill(shieldTemplate,player,1,player,null!),player);
        lateShield.AddAllEffectToSucess();lateShield.AddToEffectedController();
        h.Clock.Advance(TimeSpan.FromMilliseconds(skill.GetHitTime()));
        Assert.False(effect.PreparedNpcDrainInitialAborted);
        Assert.True(player.GetEffectController().IsInAnyAbnormalState(AbnormalState.CURSE));
        Assert.Equal(RetailNpcCurseStatMath.CurrentMode2(RetailNpcCurseStatMath.Resource.Hp,
            maxHp,player.GetLifeStats().GetMaxHp(),hp-damage),player.GetLifeStats().GetCurrentHp());
        h.Clock.Advance(TimeSpan.FromMilliseconds(1000));
        Assert.Equal(Math.Min(npc.GetLifeStats().GetMaxHp(),100+RetailNpcStatSpellDrain.HealAmount(damage,100)),npc.GetLifeStats().GetCurrentHp());
        var probe=new Effect(new Skill(DataManager.SKILL_DATA.GetSkillTemplate(17900),npc,37,player,null!),player);
        Assert.True(player.GetObserveController().TryCalculatePlayerRetailNpcDrain(probe,player,2189,AttackStatus.NORMALHIT,out _));
        Assert.Equal(0,probe.GetReserveds(1).GetValue());Assert.True(lateShield.RetailHasEnded);
        Assert.Empty(h.Clock.Faults);effect.EndEffect();
    }
    [SkippableFact]
    public void SelectedSameSkillReplacementRebuildsTheCurseAfterRemoval()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_VAMPIRE_SHIELD_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_VAMPIRE_SHIELD_SHA256");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(hash),"Requires source-checked vampire package");
        using var active=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(path!),hash!,Path.GetDirectoryName(path!)!);
        using var h=BossAiHarness.For(220050000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var npc=h.SpawnWithAi(214435,"retail_stat_base_fixture");var player=RetailNpcStatPeriodicTests.Player(h,220050000);
        player.GetCommonData().SetPlayerClass(PlayerClass.PRIEST);player.GetCommonData().SetLevel(65);
        player.GetGameStats().UpdateStatsTemplate();PlayerStatFunctions.AddPredefinedStatFunctions(player);
        player.GetLifeStats().OnStatsChange(null!);player.GetLifeStats().SetCurrentHp(player.GetLifeStats().GetMaxHp());
        player.GetLifeStats().SetCurrentMp(player.GetLifeStats().GetMaxMp());
        npc.GetLifeStats().SetCurrentHp(100);npc.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.MAGICAL_ACCURACY,10000)]);
        BossAiHarness.MakeMutuallyKnown(npc,player);int originalMax=player.GetLifeStats().GetMaxHp();
        Skill BeginCast(out int castMs)
        {
            var skill=Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(npc,16596,38,player);
            Assert.NotNull(skill);skill.GetEffectedList().Add(player);
            RetailNpcVampireCastFixture.AttachSelectedInputsForOfflineFixture(skill,(_,hi)=>hi);
            Assert.True(skill.UseWithoutPropSkill());
            castMs=(int)MathF.Floor(skill.GetSkillTemplate().GetDuration()*(npc.GetGameStats().GetCastSpeed()/1000f)+0.5f);
            return skill;
        }
        var first=BeginCast(out int firstCastMs);h.Clock.Advance(TimeSpan.FromMilliseconds(firstCastMs+first.GetHitTime()));
        var old=RetailNpcVampireCastFixture.ObservedEffectForOfflineFixture(first);Assert.NotNull(old);
        int firstDamage=old.GetReserveds(1).GetValue();
        Assert.True(player.GetEffectController().IsInAnyAbnormalState(AbnormalState.CURSE));
        int cursedMax=player.GetLifeStats().GetMaxHp();Assert.True(cursedMax<originalMax);
        var probe=new Effect(first,player);
        Assert.True(RetailNpcCurseResourceOwner.CanReplaceSelectedForOfflineFixture(player,probe));
        var second=BeginCast(out int secondCastMs);h.Clock.Advance(TimeSpan.FromMilliseconds(secondCastMs));
        var current=RetailNpcVampireCastFixture.ObservedEffectForOfflineFixture(second);Assert.NotNull(current);
        Assert.True(current.IsInSuccessEffects(1));Assert.True(current.IsInSuccessEffects(2));
        int damage=current.GetReserveds(1).GetValue();int impact=Math.Max(second.GetHitTime()-50,100);
        h.Clock.Advance(TimeSpan.FromMilliseconds(impact-1));int before=player.GetLifeStats().GetCurrentHp();
        Assert.InRange(damage,1,before-1);h.Clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(before-damage,player.GetLifeStats().GetCurrentHp());
        h.Clock.Advance(TimeSpan.FromMilliseconds(second.GetHitTime()-impact));
        Assert.True(old.RetailHasEnded);Assert.False(current.RetailHasEnded);
        Assert.True(player.GetEffectController().IsInAnyAbnormalState(AbnormalState.CURSE));
        Assert.Equal(cursedMax,player.GetLifeStats().GetMaxHp());
        int restored=RetailNpcCurseStatMath.CurrentMode2(RetailNpcCurseStatMath.Resource.Hp,cursedMax,originalMax,before-damage);
        Assert.Equal(RetailNpcCurseStatMath.CurrentMode2(RetailNpcCurseStatMath.Resource.Hp,originalMax,cursedMax,restored),
            player.GetLifeStats().GetCurrentHp());
        h.Clock.Advance(TimeSpan.FromMilliseconds(1000));
        Assert.Equal(Math.Min(npc.GetLifeStats().GetMaxHp(),100+RetailNpcStatSpellDrain.HealAmount(firstDamage,100)
            +RetailNpcStatSpellDrain.HealAmount(damage,100)),npc.GetLifeStats().GetCurrentHp());
        var nextProbe=new Effect(second,player);
        Assert.True(RetailNpcCurseResourceOwner.CanReplaceSelectedForOfflineFixture(player,nextProbe));
        var unknown=new UnreviewedOwner();
        player.GetGameStats().AddEffectOnly(unknown,[new StatRateFunction(StatEnum.MAXHP,10,true)]);
        Assert.False(RetailNpcCurseResourceOwner.CanReplaceSelectedForOfflineFixture(player,nextProbe));
        player.GetGameStats().EndEffect(unknown);
        Assert.False(RetailNpcCurseResourceOwner.CanReplaceSelectedForOfflineFixture(player,nextProbe));
        Assert.Empty(h.Clock.Faults);current.EndEffect();Assert.Equal(originalMax,player.GetLifeStats().GetMaxHp());
    }

    [SkippableTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnsupportedCurseInputsDoNotSpendShieldOrApplyPrimary(bool resourceModifier)
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_VAMPIRE_SHIELD_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_VAMPIRE_SHIELD_SHA256");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(hash),"Requires source-checked vampire and shield package");
        using var active=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(path!),hash!,Path.GetDirectoryName(path!)!);
        using var h=BossAiHarness.For(220050000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var npc=h.SpawnWithAi(214435,"retail_stat_base_fixture");var player=RetailNpcStatPeriodicTests.Player(h,220050000);
        player.GetCommonData().SetPlayerClass(PlayerClass.PRIEST);player.GetCommonData().SetLevel(65);
        player.GetGameStats().UpdateStatsTemplate();PlayerStatFunctions.AddPredefinedStatFunctions(player);
        player.GetLifeStats().OnStatsChange(null!);player.GetLifeStats().SetCurrentHp(player.GetLifeStats().GetMaxHp());
        player.GetLifeStats().SetCurrentMp(player.GetLifeStats().GetMaxMp());
        int hp=player.GetLifeStats().GetCurrentHp();npc.GetLifeStats().SetCurrentHp(100);
        npc.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.MAGICAL_ACCURACY,10000)]);
        BossAiHarness.MakeMutuallyKnown(npc,player);
        var shield=new Effect(new Skill(DataManager.SKILL_DATA.GetSkillTemplate(1165),player,1,player,null!),player);
        shield.AddAllEffectToSucess();shield.AddToEffectedController();
        player.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(
            resourceModifier?StatEnum.MAXHP:StatEnum.ABNORMAL_RESISTANCE_ALL,100)]);
        var skill=Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(npc,16596,38,player);
        Assert.NotNull(skill);skill.GetEffectedList().Add(player);
        var inputs=RetailNpcVampireCastFixture.AttachSelectedInputsForOfflineFixture(skill,(_,hi)=>hi);
        Assert.Equal(resourceModifier,inputs.CanCaptureForOfflineFixture(player));
        var preflightEffect=new Effect(skill,player);
        Assert.False(inputs.CanPrepareForOfflineFixture(player,preflightEffect));
        if(resourceModifier)
        {
            Assert.False(RetailNpcCurseResourcePlanBuilder.TryPreflightFromCatalog(player,preflightEffect,out var failure));
            Assert.Equal(RetailNpcCurseResourcePlanBuilder.Failure.ModifierInputsUnavailable,failure);
        }
        int castMs=(int)MathF.Floor(skill.GetSkillTemplate().GetDuration()*(npc.GetGameStats().GetCastSpeed()/1000f)+0.5f);
        Assert.True(skill.UseWithoutPropSkill());h.Clock.Advance(TimeSpan.FromMilliseconds(castMs+skill.GetHitTime()+1000));
        var effect=RetailNpcVampireCastFixture.ObservedEffectForOfflineFixture(skill);Assert.NotNull(effect);
        Assert.False(effect.IsInSuccessEffects(1));Assert.Equal(hp,player.GetLifeStats().GetCurrentHp());
        Assert.Equal(100,npc.GetLifeStats().GetCurrentHp());Assert.False(shield.RetailHasEnded);
        var probe=new Effect(new Skill(DataManager.SKILL_DATA.GetSkillTemplate(17900),npc,37,player,null!),player);
        Assert.True(player.GetObserveController().TryCalculatePlayerRetailNpcDrain(probe,player,2189,AttackStatus.NORMALHIT,out _));
        Assert.Equal(0,probe.GetReserveds(1).GetValue());Assert.True(shield.RetailHasEnded);
        Assert.Empty(h.Clock.Faults);
    }

    [SkippableFact]
    public void AreaVampireCastKeepsShieldAndHealOwnedByEachTarget()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_VAMPIRE_SHIELD_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_VAMPIRE_SHIELD_SHA256");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(hash),"Requires source-checked vampire and shield package");
        using var active=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(path!),hash!,Path.GetDirectoryName(path!)!);
        using var h=BossAiHarness.For(220050000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var npc=h.SpawnWithAi(214583,"retail_stat_base_fixture");
        var shielded=RetailNpcStatPeriodicTests.Player(h,220050000);var open=RetailNpcStatPeriodicTests.Player(h,220050000);
        foreach(var player in new[]{shielded,open})
        {
            player.GetCommonData().SetPlayerClass(PlayerClass.PRIEST);player.GetCommonData().SetLevel(65);
            player.GetGameStats().UpdateStatsTemplate();PlayerStatFunctions.AddPredefinedStatFunctions(player);
            player.GetLifeStats().OnStatsChange(null!);player.GetLifeStats().SetCurrentHp(player.GetLifeStats().GetMaxHp());
            player.GetLifeStats().SetCurrentMp(player.GetLifeStats().GetMaxMp());BossAiHarness.MakeMutuallyKnown(npc,player);
        }
        int shieldedHp=shielded.GetLifeStats().GetCurrentHp(),shieldedMax=shielded.GetLifeStats().GetMaxHp();
        int openHp=open.GetLifeStats().GetCurrentHp(),openMax=open.GetLifeStats().GetMaxHp();
        npc.GetLifeStats().SetCurrentHp(100);npc.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.MAGICAL_ACCURACY,10000)]);
        var shieldTemplate=DataManager.SKILL_DATA.GetSkillTemplate(1165);
        var shieldEffect=new Effect(new Skill(shieldTemplate,shielded,1,shielded,null!),shielded);
        shieldEffect.AddAllEffectToSucess();shieldEffect.AddToEffectedController();
        var skill=Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(npc,18460,41,shielded);
        Assert.NotNull(skill);skill.GetEffectedList().Add(shielded);skill.GetEffectedList().Add(open);
        RetailNpcVampireCastFixture.AttachSelectedInputsForOfflineFixture(skill,(_,hi)=>hi);
        int castMs=(int)MathF.Floor(skill.GetSkillTemplate().GetDuration()*(npc.GetGameStats().GetCastSpeed()/1000f)+0.5f);
        Assert.True(skill.UseWithoutPropSkill());h.Clock.Advance(TimeSpan.FromMilliseconds(castMs+skill.GetHitTime()));
        var protectedEffect=RetailNpcVampireCastFixture.ObservedEffectForOfflineFixture(skill,shielded);
        var openEffect=RetailNpcVampireCastFixture.ObservedEffectForOfflineFixture(skill,open);
        Assert.NotNull(protectedEffect);Assert.NotNull(openEffect);Assert.NotSame(protectedEffect,openEffect);
        int protectedDamage=protectedEffect.GetReserveds(1).GetValue(),openDamage=openEffect.GetReserveds(1).GetValue();
        Assert.Equal(ShieldType.NORMAL.GetId(),protectedEffect.GetShieldDefense());Assert.Equal(0,openEffect.GetShieldDefense());
        Assert.True(openDamage>0);Assert.True(shielded.GetEffectController().IsInAnyAbnormalState(AbnormalState.CURSE));
        Assert.True(open.GetEffectController().IsInAnyAbnormalState(AbnormalState.CURSE));
        Assert.Equal(RetailNpcCurseStatMath.CurrentMode2(RetailNpcCurseStatMath.Resource.Hp,
            shieldedMax,shielded.GetLifeStats().GetMaxHp(),shieldedHp-protectedDamage),shielded.GetLifeStats().GetCurrentHp());
        Assert.Equal(RetailNpcCurseStatMath.CurrentMode2(RetailNpcCurseStatMath.Resource.Hp,
            openMax,open.GetLifeStats().GetMaxHp(),openHp-openDamage),open.GetLifeStats().GetCurrentHp());
        h.Clock.Advance(TimeSpan.FromMilliseconds(1000));
        Assert.Equal(Math.Min(npc.GetLifeStats().GetMaxHp(),100+RetailNpcStatSpellDrain.HealAmount(protectedDamage,100)
            +RetailNpcStatSpellDrain.HealAmount(openDamage,100)),npc.GetLifeStats().GetCurrentHp());
        Assert.Empty(h.Clock.Faults);protectedEffect.EndEffect();openEffect.EndEffect();if(!shieldEffect.RetailHasEnded)shieldEffect.EndEffect();
    }

    [SkippableTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void SelectedVampireCastUsesRegisteredStoneSkinPerTarget(bool partiallySpent)
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_VAMPIRE_SHIELD_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_VAMPIRE_SHIELD_SHA256");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(hash),"Requires source-checked vampire and shield package");
        using var active=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(path!),hash!,Path.GetDirectoryName(path!)!);
        using var h=BossAiHarness.For(220050000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var npc=h.SpawnWithAi(214435,"retail_stat_base_fixture");var player=RetailNpcStatPeriodicTests.Player(h,220050000);
        player.GetCommonData().SetPlayerClass(PlayerClass.PRIEST);player.GetCommonData().SetLevel(65);
        player.GetGameStats().UpdateStatsTemplate();PlayerStatFunctions.AddPredefinedStatFunctions(player);
        player.GetLifeStats().OnStatsChange(null!);player.GetLifeStats().SetCurrentHp(player.GetLifeStats().GetMaxHp());
        player.GetLifeStats().SetCurrentMp(player.GetLifeStats().GetMaxMp());
        int originalHp=player.GetLifeStats().GetCurrentHp(),originalMax=player.GetLifeStats().GetMaxHp();
        npc.GetLifeStats().SetCurrentHp(100);npc.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.MAGICAL_ACCURACY,10000)]);
        BossAiHarness.MakeMutuallyKnown(npc,player);
        var shieldTemplate=DataManager.SKILL_DATA.GetSkillTemplate(1165);
        var shieldEffect=new Effect(new Skill(shieldTemplate,player,1,player,null!),player);
        shieldEffect.AddAllEffectToSucess();shieldEffect.AddToEffectedController();
        Assert.Same(shieldEffect,player.GetEffectController().FindBySkillId(1165));
        Assert.True(player.GetObserveController().CanCalculateRetailPlayerDrain(player));
        if(partiallySpent)
        {
            var probeTemplate=DataManager.SKILL_DATA.GetSkillTemplate(17900);
            var probe=new Effect(new Skill(probeTemplate,npc,37,player,null!),player);
            Assert.True(player.GetObserveController().TryCalculatePlayerRetailNpcDrain(probe,player,2000,AttackStatus.NORMALHIT,out _));
            Assert.Equal(0,probe.GetReserveds(1).GetValue());Assert.False(shieldEffect.RetailHasEnded);
        }
        var skill=Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(npc,16596,38,player);
        Assert.NotNull(skill);skill.GetEffectedList().Add(player);
        RetailNpcVampireCastFixture.AttachSelectedInputsForOfflineFixture(skill,(_,hi)=>hi);
        int castMs=(int)MathF.Floor(skill.GetSkillTemplate().GetDuration()*(npc.GetGameStats().GetCastSpeed()/1000f)+0.5f);
        Assert.True(skill.UseWithoutPropSkill());h.Clock.Advance(TimeSpan.FromMilliseconds(castMs));
        var effect=RetailNpcVampireCastFixture.ObservedEffectForOfflineFixture(skill);Assert.NotNull(effect);
        Assert.True(effect.IsInSuccessEffects(1));Assert.True(effect.IsInSuccessEffects(2));
        int damage=effect.GetReserveds(1).GetValue();Assert.Equal(partiallySpent,damage>0);
        Assert.Equal(ShieldType.NORMAL.GetId(),effect.GetShieldDefense());Assert.Equal(partiallySpent,shieldEffect.RetailHasEnded);
        h.Clock.Advance(TimeSpan.FromMilliseconds(skill.GetHitTime()));
        Assert.False(effect.PreparedNpcDrainInitialAborted);
        Assert.True(player.GetEffectController().IsInAnyAbnormalState(AbnormalState.CURSE));
        Assert.Equal(RetailNpcCurseStatMath.CurrentMode2(RetailNpcCurseStatMath.Resource.Hp,
            originalMax,player.GetLifeStats().GetMaxHp(),originalHp-damage),player.GetLifeStats().GetCurrentHp());
        int cursedHp=player.GetLifeStats().GetCurrentHp(),cursedMax=player.GetLifeStats().GetMaxHp();
        var replacementProbe=new Effect(skill,player);
        Assert.False(RetailNpcCurseResourcePlanBuilder.TryPreflightFromCatalog(player,replacementProbe,out _));
        Assert.True(player.GetEffectController().IsInAnyAbnormalState(AbnormalState.CURSE));
        Assert.Equal(cursedHp,player.GetLifeStats().GetCurrentHp());Assert.Equal(cursedMax,player.GetLifeStats().GetMaxHp());
        h.Clock.Advance(TimeSpan.FromMilliseconds(1000));
        Assert.Equal(Math.Min(npc.GetLifeStats().GetMaxHp(),100+RetailNpcStatSpellDrain.HealAmount(damage,100)),npc.GetLifeStats().GetCurrentHp());
        Assert.Empty(h.Clock.Faults);effect.EndEffect();if(!shieldEffect.RetailHasEnded)shieldEffect.EndEffect();
    }
}
