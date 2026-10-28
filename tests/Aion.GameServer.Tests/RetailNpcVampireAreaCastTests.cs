using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Tests.Ai;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class RetailNpcVampireAreaCastTests
{
    private sealed class UnreviewedShield : AttackCalcObserver
    {
        internal int Calls;
        public override void CheckShield(List<AttackResult> attacks,Effect effect,Creature attacker) => Calls++;
    }

    [SkippableTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void RejectedDelayedPrimaryDoesNotCancelThePreviouslyScheduledRetailHeal(bool reuseLife)
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TIMING_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TIMING_SHA256");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(hash),"Requires reviewed retail input bundle");
        using var active=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(path!),hash!,Path.GetDirectoryName(path!)!);
        using var h=BossAiHarness.For(220050000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var npc=h.SpawnWithAi(214435,"retail_stat_base_fixture");var player=RetailNpcStatPeriodicTests.Player(h,220050000);
        player.GetCommonData().SetPlayerClass(PlayerClass.PRIEST);player.GetCommonData().SetLevel(65);
        player.GetGameStats().UpdateStatsTemplate();PlayerStatFunctions.AddPredefinedStatFunctions(player);
        player.GetLifeStats().OnStatsChange(null!);player.GetLifeStats().SetCurrentHp(player.GetLifeStats().GetMaxHp());
        player.GetLifeStats().SetCurrentMp(player.GetLifeStats().GetMaxMp());
        npc.GetLifeStats().SetCurrentHp(100);npc.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.MAGICAL_ACCURACY,10000)]);
        BossAiHarness.MakeMutuallyKnown(npc,player);
        var skill=Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(npc,16596,38,player);
        Assert.NotNull(skill);skill.GetEffectedList().Add(player);
        RetailNpcVampireCastFixture.AttachSelectedInputsForOfflineFixture(skill,(_,hi)=>hi);
        int castMs=(int)MathF.Floor(skill.GetSkillTemplate().GetDuration()*(npc.GetGameStats().GetCastSpeed()/1000f)+0.5f);
        Assert.True(skill.UseWithoutPropSkill());h.Clock.Advance(TimeSpan.FromMilliseconds(castMs));
        var effect=RetailNpcVampireCastFixture.ObservedEffectForOfflineFixture(skill);Assert.NotNull(effect);
        int damage=effect.GetReserveds(1).GetValue();Assert.True(damage>0);
        var shield=new UnreviewedShield();
        if(reuseLife){h.World.Despawn(player);h.World.Spawn(player);}
        else player.GetObserveController().AddAttackCalcObserver(shield);
        int targetHp=player.GetLifeStats().GetCurrentHp();
        h.Clock.Advance(TimeSpan.FromMilliseconds(skill.GetHitTime()+1000));
        Assert.True(effect.PreparedNpcDrainInitialAborted);
        Assert.Equal(targetHp,player.GetLifeStats().GetCurrentHp());
        Assert.Equal(Math.Min(npc.GetLifeStats().GetMaxHp(),100+RetailNpcStatSpellDrain.HealAmount(damage,100)),
            npc.GetLifeStats().GetCurrentHp());
        Assert.False(player.GetEffectController().IsInAnyAbnormalState(AbnormalState.CURSE));
        Assert.Equal(0,shield.Calls);Assert.Empty(h.Clock.Faults);
    }

    [SkippableFact]
    public void SelectedAreaCastKeepsSeparatePlayerCurseAndHealInputs()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TIMING_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TIMING_SHA256");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(hash),"Requires reviewed retail input bundle");
        using var active=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(path!),hash!,Path.GetDirectoryName(path!)!);
        using var h=BossAiHarness.For(220050000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var npc=h.SpawnWithAi(214583,"retail_stat_base_fixture");
        var first=RetailNpcStatPeriodicTests.Player(h,220050000);
        var second=RetailNpcStatPeriodicTests.Player(h,220050000);
        foreach(var player in new[]{first,second})
        {
            player.GetCommonData().SetPlayerClass(PlayerClass.PRIEST);
            player.GetCommonData().SetLevel(ReferenceEquals(player,first)?65:64);
            player.GetGameStats().UpdateStatsTemplate();PlayerStatFunctions.AddPredefinedStatFunctions(player);
            player.GetLifeStats().OnStatsChange(null!);
            player.GetLifeStats().SetCurrentHp(player.GetLifeStats().GetMaxHp());
            player.GetLifeStats().SetCurrentMp(player.GetLifeStats().GetMaxMp());
            BossAiHarness.MakeMutuallyKnown(npc,player);
        }
        int firstHp=first.GetLifeStats().GetCurrentHp(), secondHp=second.GetLifeStats().GetCurrentHp();
        int firstMax=first.GetLifeStats().GetMaxHp(),secondMax=second.GetLifeStats().GetMaxHp();
        npc.GetLifeStats().SetCurrentHp(100);
        npc.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.MAGICAL_ACCURACY,10000)]);
        var skill=Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(npc,18460,41,first);
        Assert.NotNull(skill);
        skill.GetEffectedList().Add(first);skill.GetEffectedList().Add(second);
        var firstInputs=RetailNpcVampireCastFixture.AttachSelectedInputsForOfflineFixture(skill,(_,hi)=>hi);
        int castMs=(int)MathF.Floor(skill.GetSkillTemplate().GetDuration()*(npc.GetGameStats().GetCastSpeed()/1000f)+0.5f);
        Assert.True(skill.UseWithoutPropSkill());
        h.Clock.Advance(TimeSpan.FromMilliseconds(castMs+skill.GetHitTime()));
        Assert.Empty(h.Clock.Faults);
        Assert.True(first.GetLifeStats().GetCurrentHp()<firstHp);
        Assert.True(second.GetLifeStats().GetCurrentHp()<secondHp);
        Assert.True(first.GetEffectController().IsInAnyAbnormalState(AbnormalState.CURSE));
        Assert.True(second.GetEffectController().IsInAnyAbnormalState(AbnormalState.CURSE));
        Assert.True(first.GetLifeStats().GetMaxHp()<firstMax);
        Assert.True(second.GetLifeStats().GetMaxHp()<secondMax);
        Assert.NotNull(firstInputs.PreparedPlan);
        var firstEffect=RetailNpcVampireCastFixture.ObservedEffectForOfflineFixture(skill,first);
        var secondEffect=RetailNpcVampireCastFixture.ObservedEffectForOfflineFixture(skill,second);
        Assert.NotNull(firstEffect);Assert.NotNull(secondEffect);Assert.NotSame(firstEffect,secondEffect);
        int firstDamage=firstEffect.GetReserveds(1).GetValue(),secondDamage=secondEffect.GetReserveds(1).GetValue();
        Assert.True(firstDamage>0);Assert.True(secondDamage>0);
        Assert.Equal(RetailNpcCurseStatMath.CurrentMode2(RetailNpcCurseStatMath.Resource.Hp,
            firstMax,first.GetLifeStats().GetMaxHp(),firstHp-firstDamage),first.GetLifeStats().GetCurrentHp());
        Assert.Equal(RetailNpcCurseStatMath.CurrentMode2(RetailNpcCurseStatMath.Resource.Hp,
            secondMax,second.GetLifeStats().GetMaxHp(),secondHp-secondDamage),second.GetLifeStats().GetCurrentHp());
        h.Clock.Advance(TimeSpan.FromMilliseconds(1000));
        Assert.Equal(Math.Min(npc.GetLifeStats().GetMaxHp(),100+RetailNpcStatSpellDrain.HealAmount(firstDamage,100)
            +RetailNpcStatSpellDrain.HealAmount(secondDamage,100)),npc.GetLifeStats().GetCurrentHp());
        Assert.Empty(h.Clock.Faults);
    }
}
