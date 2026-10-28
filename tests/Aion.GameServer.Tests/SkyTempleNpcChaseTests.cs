using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.Tests.Ai;
using Aion.GameServer.Utils;
using Aion.GameServer.Controllers.Effects;
using Aion.GameServer.Dataholders;
using Aion.GameServer.SkillEngine.Model;
using System.Reflection;

namespace Aion.GameServer.Tests;

/// <summary>Actual current NPC/hate/effect/attack owners with the shared monotonic
/// clock. Expected738 boundaries come from native103210/102790/102820/11d360;
/// no chosen native flags, no second task and no geometry-success callback.</summary>
[Collection("GoldenDataManager")]
public sealed class SkyTempleNpcChaseTests
{
    private const string ProfileFile=@"C:\Users\ryanf\Documents\Aion-NPC-Discovery\implementation\pilot25\data\sky-temple-cohort.json";
    private static string Profiles()=>Environment.GetEnvironmentVariable("AION_RETAIL_SKYTEMPLE_TEST_DATA")??ProfileFile;
    private static void RequireProfiles()=>Skip.IfNot(File.Exists(Profiles()),"Pinned actual25 profile artifact is required.");
    private sealed class Fixture:IDisposable
    {
        private readonly IDisposable profiles;
        internal readonly BossAiHarness H;
        internal readonly Npc Npc;
        internal readonly Player Target;
        internal uint Tick=1000;
        internal Fixture(int npcId=212769)
        {
            profiles=SkyTempleNpcProfileCatalog.InstallForTests(File.ReadAllBytes(Profiles()));
            H=BossAiHarness.For(320050000).WithWorldSize(512).Build();
            SystemClock.UseSource(()=>Tick);
            Npc=H.SpawnWithAi(npcId,"general",391.25f,406.25f,374.58f);
            Target=H.SpawnPlayer(391.25f,404.25f,374.58f);
            H.Engage(Npc,Target); // Sets real target/hate, reaching NpcController.OnAddHate.
        }
        internal SkyTempleNpcSkillState.ChaseDecision Evaluate()
        {Assert.True(SkyTempleNpcSkillState.TryEvaluateChase(Npc,out var value));return value;}
        internal SkyTempleNpcSkillState.ChaseSnapshot Snapshot()=>SkyTempleNpcSkillState.GetChaseSnapshot(Npc);
        public void Dispose(){H.Dispose();profiles.Dispose();SystemClock.UseSystemClock();}
    }

    [SkippableTheory]
    [InlineData(0u)]
    [InlineData(1000u)]
    public void NumericFifteenExpiresAtExactUnsignedDeadline(uint initial)
    {
        RequireProfiles();using var f=new Fixture();f.Tick=initial;
        SkyTempleNpcSkillState.BeginEncounter(f.Npc);
        Assert.Equal(initial+15000u,f.Snapshot().Deadline);Assert.Equal(15000u,f.Snapshot().TemplateMilliseconds);
        f.Tick=initial+14999u;Assert.Equal(SkyTempleNpcSkillState.ChaseDisposition.Continue,f.Evaluate().Disposition);
        f.Tick=initial+15000u;Assert.Equal(SkyTempleNpcSkillState.ChaseDisposition.Expired,f.Evaluate().Disposition);
    }
    [SkippableFact]
    public void NativeAbsoluteUnsignedComparisonIsNotSilentlyReplacedByWrapElapsedPolicy()
    {
        RequireProfiles();using var f=new Fixture();f.Tick=uint.MaxValue-100;
        SkyTempleNpcSkillState.BeginEncounter(f.Npc);
        Assert.Equal(14899u,f.Snapshot().Deadline);
        // Native ADD wraps but738<=GetTickCount remains an absolute unsigned compare.
        Assert.Equal(SkyTempleNpcSkillState.ChaseDisposition.Expired,f.Evaluate().Disposition);
    }
    [SkippableFact]
    public void StopChaseConsumesItsFreshTickAfterTheParentCapture()
    {
        RequireProfiles();using var f=new Fixture();int reads=0;
        SystemClock.UseSource(()=>++reads==1?15999L:16000L);
        var result=f.Evaluate();
        Assert.Equal(2,reads);Assert.Equal(16000u,result.Tick);
        Assert.Equal(SkyTempleNpcSkillState.ChaseDisposition.Expired,result.Disposition);
        f.Npc.GetEffectController().SetAbnormal(AbnormalState.STUN);reads=0;
        SystemClock.UseSource(()=>{reads++;return 20000;});
        Assert.Equal(SkyTempleNpcSkillState.ChaseDisposition.Suppressed,f.Evaluate().Disposition);
        Assert.Equal(1,reads); // Suppression skips CheckStopChase's inner tick call.
    }
    [SkippableFact]
    public void ActualCurrentTargetHateRenewsButOtherTargetHateDoesNot()
    {
        RequireProfiles();using var f=new Fixture();Assert.Equal(16000u,f.Snapshot().Deadline);
        f.Tick=6000;f.Npc.GetAggroList().AddHate(f.Target,1);
        Assert.Equal(21000u,f.Snapshot().Deadline);Assert.Equal(6000u,f.Snapshot().LastRefresh);
        var other=f.H.SpawnPlayer(390,404,374.58f);BossAiHarness.MakeMutuallyKnown(f.Npc,other);
        f.Tick=7000;f.Npc.GetAggroList().AddHate(other,1);
        Assert.Same(f.Target,f.Npc.GetTarget());Assert.Equal(21000u,f.Snapshot().Deadline);
    }
    [SkippableFact]
    public void ActualNormalEmissionExtendsRecoveryPlusChaseTimeout()
    {
        RequireProfiles();using var f=new Fixture();f.Tick=5000;
        int actualAttackDelay=f.Npc.GetGameStats().GetAttackSpeed().GetCurrent();
        SkyTempleNpcSkillState.NormalEmitted(f.Npc,f.Tick);
        // All25 verified max animation words are below base attackdelay, so728 wins.
        Assert.Equal(checked(5000u+(uint)actualAttackDelay+15000u),f.Snapshot().Deadline);
        f.Tick=f.Snapshot().Deadline-1;Assert.Equal(SkyTempleNpcSkillState.ChaseDisposition.Continue,f.Evaluate().Disposition);
    }
    [SkippableFact]
    public void SpatialMotionAndRepeatedChaseQueriesDoNotInventTimerRenewals()
    {
        RequireProfiles();using var f=new Fixture();uint deadline=f.Snapshot().Deadline;
        f.Tick=9000;f.H.World.UpdatePosition(f.Npc,391.25f,405.25f,374.58f,0,false);
        Assert.Equal(deadline,f.Snapshot().Deadline);
        Assert.Equal(SkyTempleNpcSkillState.ChaseDisposition.Continue,f.Evaluate().Disposition);
        f.Tick=deadline;Assert.Equal(SkyTempleNpcSkillState.ChaseDisposition.Expired,f.Evaluate().Disposition);
        Assert.Equal(deadline,f.Snapshot().Deadline);
    }
    [SkippableFact]
    public void ActualStunSuppressionAndActualExitRefreshTheSourceDeadline()
    {
        RequireProfiles();using var f=new Fixture();var effects=f.Npc.GetEffectController();
        effects.SetAbnormal(AbnormalState.STUN);f.Tick=16001;
        Assert.Equal(SkyTempleNpcSkillState.ChaseDisposition.Suppressed,f.Evaluate().Disposition);
        Assert.Equal(16000u,f.Snapshot().Deadline);
        f.Tick=16002;effects.UnsetAbnormal(AbnormalState.STUN);
        Assert.Equal(31002u,f.Snapshot().Deadline);
        f.Tick=31001;Assert.Equal(SkyTempleNpcSkillState.ChaseDisposition.Continue,f.Evaluate().Disposition);
        f.Tick=31002;Assert.Equal(SkyTempleNpcSkillState.ChaseDisposition.Expired,f.Evaluate().Disposition);
    }
    [SkippableFact]
    public void NativeAbsentClearStillNotifiesAndRefreshesNpcDeadline()
    {
        RequireProfiles();using var f=new Fixture();f.Tick=6000;
        f.Npc.GetEffectController().UnsetAbnormal(AbnormalState.STUN);
        Assert.Equal(21000u,f.Snapshot().Deadline);Assert.Equal(0u,f.Snapshot().Native188);
    }
    [SkippableFact]
    public void OverlappingEffectiveStatusDoesNotOverwriteSourceNpcClearShadow()
    {
        RequireProfiles();using var f=new Fixture();var effects=f.Npc.GetEffectController();
        // Explicit current registry fixture for the existing duplicate-count branch.
        // This is not a claim that two whole spell Check/Begin programs were run.
        var template=DataManager.SKILL_DATA.GetSkillTemplate(16556);Assert.NotNull(template);
        var first=new Effect(f.Target,f.Npc,template,1);first.SetAbnormal(AbnormalState.STUN);
        var second=new Effect(f.Target,f.Npc,template,1);second.SetAbnormal(AbnormalState.STUN);
        var field=typeof(EffectController).GetField("abnormalEffectMap",BindingFlags.Instance|BindingFlags.NonPublic);
        Assert.NotNull(field);var map=Assert.IsType<Dictionary<string,Effect>>(field.GetValue(effects));
        map.Add("declared-stun-first",first);map.Add("declared-stun-second",second);
        try
        {
            effects.SetAbnormal(AbnormalState.STUN);effects.SetAbnormal(AbnormalState.STUN);
            Assert.Equal(0x1000u,f.Snapshot().Native188);
            f.Tick=7000;effects.UnsetAbnormal(AbnormalState.STUN);
            Assert.True(effects.IsAbnormalSet(AbnormalState.STUN)); // Keep4.8 effective stack.
            Assert.Equal(0u,f.Snapshot().Native188);Assert.Equal(22000u,f.Snapshot().Deadline);
            f.Tick=22000;Assert.Equal(SkyTempleNpcSkillState.ChaseDisposition.Expired,f.Evaluate().Disposition);
        }
        finally{map.Clear();effects.UnsetAbnormal(AbnormalState.STUN);}
    }
    [SkippableFact]
    public void OldCapturedExpiryAndAbnormalOperationCannotAffectReplacementLife()
    {
        RequireProfiles();using var f=new Fixture();
        var operation=SkyTempleNpcSkillState.AbnormalChange.Capture(f.Npc);Assert.NotNull(operation);
        f.Tick=16000;var expired=f.Evaluate();Assert.Equal(SkyTempleNpcSkillState.ChaseDisposition.Expired,expired.Disposition);
        long life=f.Npc.GetController().RetailLifeEpoch;
        f.H.World.Despawn(f.Npc);Assert.False(f.Npc.IsSpawned());
        Assert.NotEqual(life,f.Npc.GetController().RetailLifeEpoch);
        f.H.World.Spawn(f.Npc);f.Tick=20000;f.H.Engage(f.Npc,f.Target);
        Assert.True(f.Npc.IsSpawned());Assert.Equal(35000u,f.Snapshot().Deadline);
        operation.Set(AbnormalState.STUN);
        Assert.False(AttackManager.DispatchRetailChaseExpiry((NpcAI)f.Npc.GetAi(),expired));
        Assert.Equal(0u,f.Snapshot().Native188);Assert.True(f.Snapshot().Started);
        Assert.Equal(AIState.FIGHT,f.Npc.GetAi().GetState());Assert.Same(f.Target,f.Npc.GetTarget());
        Assert.Equal(BossAiHarness.InitialHate,f.Npc.GetAggroList().GetHate(f.Target));
    }
    [SkippableFact]
    public void ReentrantExpiryClockRetirementCannotGiveUpTheReplacementEncounter()
    {
        RequireProfiles();using var f=new Fixture();f.Tick=16000;
        long life=f.Npc.GetController().RetailLifeEpoch;int reads=0;bool replaced=false;
        SystemClock.UseSource(()=>
        {
            if(++reads==2&&!replaced) // The fresh CheckStopChase read, after the first life check.
            {
                replaced=true;f.H.World.Despawn(f.Npc);f.H.World.Spawn(f.Npc);
                f.Tick=20000;f.H.Engage(f.Npc,f.Target);
            }
            return f.Tick;
        });
        Assert.True(AttackManager.UpdateRetailChase((NpcAI)f.Npc.GetAi()));
        Assert.True(replaced);Assert.NotEqual(life,f.Npc.GetController().RetailLifeEpoch);
        Assert.Equal(AIState.FIGHT,f.Npc.GetAi().GetState());Assert.Same(f.Target,f.Npc.GetTarget());
        Assert.Equal(35000u,f.Snapshot().Deadline);Assert.Equal(0u,f.Snapshot().Native188);
        Assert.Equal(BossAiHarness.InitialHate,f.Npc.GetAggroList().GetHate(f.Target));
    }
    [SkippableFact]
    public void CapturedExpiryCannotConsumeAnActualHateRefresh()
    {
        RequireProfiles();using var f=new Fixture();f.Tick=16000;var expired=f.Evaluate();
        f.Npc.GetAggroList().AddHate(f.Target,1);
        Assert.Equal(31000u,f.Snapshot().Deadline);
        Assert.False(AttackManager.DispatchRetailChaseExpiry((NpcAI)f.Npc.GetAi(),expired));
        Assert.Equal(AIState.FIGHT,f.Npc.GetAi().GetState());Assert.Same(f.Target,f.Npc.GetTarget());
    }
    [SkippableFact]
    public void CapturedExpiryCannotGiveUpAnActuallyRetargetedEncounter()
    {
        RequireProfiles();using var f=new Fixture();f.Tick=16000;var expired=f.Evaluate();
        var other=f.H.SpawnPlayer(390.25f,404.25f,374.58f);
        f.H.Engage(f.Npc,other);
        Assert.False(AttackManager.DispatchRetailChaseExpiry((NpcAI)f.Npc.GetAi(),expired));
        Assert.Equal(AIState.FIGHT,f.Npc.GetAi().GetState());Assert.Same(other,f.Npc.GetTarget());
        Assert.Equal(BossAiHarness.InitialHate,f.Npc.GetAggroList().GetHate(other));
    }
    [SkippableFact]
    public void UnprovedAliasHistoryDoesNotBecomeVerifiedClearAfterLegacyUnset()
    {
        RequireProfiles();using var f=new Fixture();var effects=f.Npc.GetEffectController();
        effects.SetAbnormal(AbnormalState.STUNLIKE);
        Assert.Equal(SkyTempleNpcSkillState.ChaseDisposition.Unsupported,f.Evaluate().Disposition);
        effects.UnsetAbnormal(AbnormalState.STUNLIKE);
        Assert.False(effects.IsAbnormalSet(AbnormalState.STUNLIKE));
        Assert.Equal(SkyTempleNpcSkillState.ChaseDisposition.Unsupported,f.Evaluate().Disposition);
        Assert.NotEqual(0u,f.Snapshot().UnprovedHistory);
    }
    [SkippableFact]
    public void SanctuaryUsesOuterUpdateRefreshAndIsNotAnOutgoingMaskRefresh()
    {
        RequireProfiles();using var f=new Fixture();var effects=f.Npc.GetEffectController();
        effects.SetAbnormal(AbnormalState.SANCTUARY);f.Tick=20000;
        Assert.Equal(SkyTempleNpcSkillState.ChaseDisposition.Suppressed,f.Evaluate().Disposition);
        Assert.Equal(35000u,f.Snapshot().Deadline);
        f.Tick=21000;effects.UnsetAbnormal(AbnormalState.SANCTUARY);
        Assert.Equal(35000u,f.Snapshot().Deadline); //110fd0 does not test bit31.
        f.Tick=35000;Assert.Equal(SkyTempleNpcSkillState.ChaseDisposition.Expired,f.Evaluate().Disposition);
    }
    [SkippableFact]
    public void ExistingAttackOwnerConsumesExpiryWithoutInventingAnEmission()
    {
        RequireProfiles();using var f=new Fixture();f.Tick=16000;
        int hp=f.Npc.GetLifeStats().GetCurrentHp();
        Assert.True(AttackManager.UpdateRetailChase((NpcAI)f.Npc.GetAi()));
        Assert.NotEqual(AIState.FIGHT,f.Npc.GetAi().GetState());
        Assert.Equal(0,f.Npc.GetAggroList().GetHate(f.Target));
        Assert.Equal(hp,f.Npc.GetLifeStats().GetCurrentHp());
    }
    [SkippableFact]
    public void CohortTargetTooFarDoesNotApplyLegacyFiftyMetreCutoff()
    {
        RequireProfiles();using var f=new Fixture();
        f.H.World.UpdatePosition(f.Target,391.25f,300.25f,374.58f,0,false);
        BossAiHarness.MakeMutuallyKnown(f.Npc,f.Target);f.Tick=1500;
        Assert.True(PositionUtil.GetDistance(f.Npc,f.Target,false)>100);
        AttackManager.TargetTooFar((NpcAI)f.Npc.GetAi());
        Assert.Equal(AIState.FIGHT,f.Npc.GetAi().GetState());Assert.Same(f.Target,f.Npc.GetTarget());
        Assert.Equal(16000u,f.Snapshot().Deadline);
    }
    [SkippableFact]
    public void ExistingMostHatedSwitchStillRunsBeforeCohortMoveBranch()
    {
        RequireProfiles();using var f=new Fixture();
        var other=f.H.SpawnPlayer(391.25f,300.25f,374.58f);
        BossAiHarness.MakeMutuallyKnown(f.Npc,other);f.Npc.GetAggroList().AddHate(other,2000);
        Assert.Same(f.Target,f.Npc.GetTarget());Assert.True(f.Npc.GetAggroList().GetHate(other)>f.Npc.GetAggroList().GetHate(f.Target));
        f.Tick=7000;Assert.True(f.Npc.GetGameStats().GetLastChangeTargetTimeDelta()>5);
        AttackManager.TargetTooFar((NpcAI)f.Npc.GetAi());
        Assert.Same(other,f.Npc.GetTarget());Assert.Equal(AIState.FIGHT,f.Npc.GetAi().GetState());
        Assert.Equal(16000u,f.Snapshot().Deadline); // A retarget is not an emitted action/reset.
    }
    [SkippableFact]
    public void SourceSelfSkillCancellationReplaces738WithRetryPlusFifteenSeconds()
    {
        RequireProfiles();using var f=new Fixture(212777);f.Tick=100000;
        // Actual selected self-protection cast avoids inventing a target/range result.
        var skill=Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(f.Npc,16522,22,f.Npc);
        Assert.NotNull(skill);Assert.True(skill.UseSkill());Assert.Same(skill,f.Npc.GetCastingSkill());
        Assert.True(f.Snapshot().Deadline>115000u); // Actual cast/recovery producer extended738.
        f.Npc.GetController().CancelCurrentSkill(f.Target);
        Assert.Equal(115100u,f.Snapshot().Deadline);Assert.Equal(100,SkyTempleNpcSkillState.GetNextMovementInterval(f.Npc));
    }
    [Theory]
    [InlineData(AbnormalState.PARALYZE,0x4u)]
    [InlineData(AbnormalState.SLEEP,0x8u)]
    [InlineData(AbnormalState.ROOT,0x10u)]
    [InlineData(AbnormalState.CHARM,0x40u)]
    [InlineData(AbnormalState.STUN,0x1000u)]
    [InlineData(AbnormalState.PETRIFICATION,0x2000u)]
    [InlineData(AbnormalState.STUMBLE,0x4000u)]
    [InlineData(AbnormalState.STAGGER,0x8000u)]
    [InlineData(AbnormalState.OPENAERIAL,0x10000u)]
    [InlineData(AbnormalState.SPIN,0x80000u)]
    [InlineData(AbnormalState.PULLED,0x400000u)]
    [InlineData(AbnormalState.SIMPLE_MOVE_BACK,0x1000000u)]
    [InlineData(AbnormalState.SANCTUARY,0x80000000u)]
    public void NamedStatusMappingUsesProvedMainMagicToNpcWriterJoin(AbnormalState state,uint expected)
    {Assert.True(SkyTempleNpcSkillState.TryChaseFlags(state,out uint actual));Assert.Equal(expected,actual);}
    [Theory]
    [InlineData(AbnormalState.STUNLIKE)]
    [InlineData(AbnormalState.CANT_MOVE_OR_ATTACK)]
    [InlineData(AbnormalState.UNK)]
    [InlineData(AbnormalState.UNK_2)]
    public void UnprovedActiveAliasesCannotBeAdmittedAsClearNativeFlags(AbnormalState state)
    {Assert.False(SkyTempleNpcSkillState.TryChaseFlags(state,out _));}
    [Fact]
    public void UnconsumedNamedStatesDoNotBecomeChaseSuppression()
    {
        Assert.True(SkyTempleNpcSkillState.TryChaseFlags(AbnormalState.POISON|AbnormalState.BLEED|AbnormalState.BLIND|
            AbnormalState.DISEASE|AbnormalState.SILENCE|AbnormalState.FEAR|AbnormalState.CURSE|AbnormalState.CONFUSE|
            AbnormalState.SNARE|AbnormalState.SLOW|AbnormalState.BIND|AbnormalState.DEFORM|AbnormalState.NOFLY|
            AbnormalState.HIDE|AbnormalState.INVULNERABLE_WING,out uint result));
        Assert.Equal(0u,result);
    }
}
