using System;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Retail;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.Ai.Manager;

/// <summary>
/// Java parity: ai/manager/AttackManager (ATracer). Drives NPC attack scheduling/intention + target-too-far chase/giveup logic.
/// AttackIntention SCREAMING (SIMPLE_ATTACK/SKILL_ATTACK/FINISH_ATTACK); AISubState.NONE/TARGET_LOST -> None/TargetLost; AiEventType.
/// TARGET_CHANGED/TARGET_GIVEUP -> TargetChanged/TargetGiveup; AggroTarget.MOST_HATED SCREAMING; instanceof -> is. schedule(Runnable,2000)
/// -> async ThreadPool idiom. SimpleAttackManager/SkillAttackManager red-tolerated (port next ticks).
/// </summary>
public class AttackManager
{
    private static readonly ILogger RetailLog=AionLog.For(nameof(AttackManager));
    public static void StartAttacking(NpcAI npcAI)
    {
        if (npcAI.IsLogging())
        {
            AILogger.Info(npcAI, "AttackManager: startAttacking");
        }
        npcAI.GetOwner().GetGameStats().SetFightStartingTime();
        SkyTempleNpcSkillState.BeginEncounter(npcAI.GetOwner());
        Aion.GameServer.Controllers.Movement.SkyTempleNpcReturnState.EncounterStarted(npcAI.GetOwner());
        VisibleObject target = npcAI.GetOwner().GetTarget();
        if (target is Creature creature) // may be null at this point (-> triggering then TARGET_GIVEUP)
            EmoteManager.EmoteStartAttacking(npcAI.GetOwner(), creature);
        ScheduleNextAttack(npcAI);
    }

    public static void ScheduleNextAttack(NpcAI npcAI)
    {
        if(UpdateRetailChase(npcAI))return;
        if (npcAI.IsLogging())
        {
            AILogger.Info(npcAI, "AttackManager: scheduleNextAttack");
        }
        // don't start attack while in casting substate
        AISubState subState = npcAI.GetSubState();
        if (subState == AISubState.NONE)
        {
            ChooseAttack(npcAI, npcAI.GetOwner().GetGameStats().GetNextAttackInterval());
        }
        else
        {
            if (npcAI.IsLogging())
            {
                AILogger.Info(npcAI, "Will not choose attack in substate" + subState);
            }
        }
    }

    protected static void ChooseAttack(NpcAI npcAI, int delay)
    {
        AttackIntention attackIntention = npcAI.ChooseAttackIntention();
        if (npcAI.IsLogging())
        {
            AILogger.Info(npcAI, "AttackManager: chooseAttack " + attackIntention + " delay " + delay);
        }
        if (!npcAI.CanThink())
        {
            return;
        }
        switch (attackIntention)
        {
            case AttackIntention.SIMPLE_ATTACK:
                SimpleAttackManager.PerformAttack(npcAI, delay);
                break;
            case AttackIntention.SKILL_ATTACK:
                SkillAttackManager.PerformAttack(npcAI, delay);
                break;
            case AttackIntention.FINISH_ATTACK:
                npcAI.Think();
                break;
        }
    }

    public static void TargetTooFar(NpcAI npcAI)
    {
        Npc npc = npcAI.GetOwner();
        if (npcAI.IsLogging())
        {
            AILogger.Info(npcAI, "AttackManager: attackTimeDelta " + npc.GetGameStats().GetLastAttackTimeDelta());
        }

        bool retail=SkyTempleNpcProfileCatalog.TryGet(npc,out _);
        if(retail&&UpdateRetailChase(npcAI))return;

        // switch target if there is more hated creature
        if (npc.GetGameStats().GetLastChangeTargetTimeDelta() > 5)
        {
            Creature mostHated = npc.GetAggroList().GetTarget(AggroTarget.MOST_HATED);
            if (mostHated != null && !npc.IsTargeting(mostHated.GetObjectId()))
            {
                if (npcAI.IsLogging())
                {
                    AILogger.Info(npcAI, "AttackManager: switching target during chase");
                }
                npcAI.OnCreatureEvent(AiEventType.TargetChanged, mostHated);
                return;
            }
        }
        if(retail)
        {
            // Preserve the existing most-hated switch until its native event
            // producer is integrated. CanSee is concealment/GM visibility, not
            // route LOS. NativeCanAttack removes an invalid/unseen current target;
            // it has no Java2second loss task or Java target/home radius.
            if(npc.GetTarget() is not Creature current||current.IsDead()||!current.IsSpawned()||
                current.GetWorldId()!=npc.GetWorldId()||current.GetInstanceId()!=npc.GetInstanceId()||!npc.CanSee(current))
            {npcAI.OnGeneralEvent(AiEventType.TargetGiveup);return;}
            if(npcAI.IsMoveSupported())npc.GetMoveController().MoveToTargetObject();
            else npcAI.OnGeneralEvent(AiEventType.TargetGiveup);
            return;
        }
        if (!npc.CanSee(npc.GetTarget()))
        {
            if (npcAI.SetSubStateIfNot(AISubState.TARGET_LOST))
            {
                ThreadPoolManager.GetInstance().Schedule(ct =>
                {
                    if (npcAI.IsInSubState(AISubState.TARGET_LOST) && npc.IsSpawned() && !npc.IsDead())
                        npcAI.OnGeneralEvent(AiEventType.TargetGiveup);
                    return ValueTask.CompletedTask;
                }, TimeSpan.FromMilliseconds(2000));
            }
            return;
        }
        if (CheckGiveupDistance(npcAI))
        {
            npcAI.OnGeneralEvent(AiEventType.TargetGiveup);
            return;
        }
        if (npcAI.IsMoveSupported())
        {
            npc.GetMoveController().MoveToTargetObject();
            return;
        }
        npcAI.OnGeneralEvent(AiEventType.TargetGiveup);
    }

    /// <summary>Called by existing movement/attack updates only. True consumes this
    /// update; no new task or fabricated arrival. Controller->nav snapshot->skill
    /// gate order matches the movement retirement owner; AI callbacks hold neither
    /// nav nor SkillState.Gate. Action deadline producers use the same controller.</summary>
    internal static bool UpdateRetailChase(NpcAI ai)
    {
        var npc=ai.GetOwner();
        if(!SkyTempleNpcProfileCatalog.TryGet(npc,out _))return false;
        SkyTempleNpcSkillState.ChaseDecision decision;
        lock(npc.GetMoveController())
        {
            if(!SkyTempleNpcSkillState.TryEvaluateChase(npc,out decision))return false;
            if(decision.Disposition==SkyTempleNpcSkillState.ChaseDisposition.Unsupported)return true;
            if(decision.Disposition!=SkyTempleNpcSkillState.ChaseDisposition.Expired)return false;
        }
        DispatchRetailChaseExpiry(ai,decision);
        return true;
    }

    internal static bool DispatchRetailChaseExpiry(NpcAI ai,SkyTempleNpcSkillState.ChaseDecision decision)
    {
        var npc=ai.GetOwner();
        lock(npc.GetMoveController())
        {
            if(!decision.TryClaimExpiry(npc))return false;
            if(npc.GetCastingSkill()?.GetSkillTemplate().IsMaintain()==true)
            {RetailLog.LogWarning("SkyTemple return unsupported active maintained cast npc={Npc}",npc.GetObjectId());return false;}
        }
        // GetAllEffects snapshots both maps (including NoShow) then releases its
        // reader. StartEffect can hold Effect->SetAbnormal->controller: never
        // acquire an effect monitor while holding the controller in this owner.
        if(!Aion.GameServer.Controllers.Movement.SkyTempleNpcReturnState.TryCleanupOrder(npc,out var cleanup))return false;
        foreach(var effect in cleanup)
        {
            lock(npc.GetMoveController())if(!decision.ClaimedIdentityIsCurrent(npc))return false;
            if(!npc.GetEffectController().GetAllEffects().Any(current=>ReferenceEquals(current,effect)))continue;
            effect.EndEffect(false);
        }
        lock(npc)lock(npc.GetMoveController())
        {
            // Cleanup may renew738 through an abnormal clear. Native has already
            // decided to stop; only actor/target/world/profile identity is rechecked.
            if(!decision.ClaimedIdentityIsCurrent(npc)||ai.GetState()!=AIState.FIGHT)return false;
            if(!Aion.GameServer.Controllers.Movement.SkyTempleNpcReturnState.PrepareChaseTimeout(npc))return false;
            RetailLog.LogInformation("SkyTemple chase expired npc={Npc} template={Template} target={Target} tick={Tick} deadline738={Deadline} failedApproach={Failure} map={Map} instance={Instance}",
                npc.GetObjectId(),npc.GetNpcId(),npc.GetTarget()?.GetObjectId(),decision.Tick,decision.Deadline,decision.ApproachFailure,npc.GetWorldId(),npc.GetInstanceId());
            ai.OnGeneralEvent(AiEventType.TargetGiveup);
            return true;
        }
    }

    private static bool CheckGiveupDistance(NpcAI npcAI)
    {
        Npc npc = npcAI.GetOwner();
        // if target run away too far
        VisibleObject target = npc.GetTarget();
        if (target != null)
        {
            if (npcAI.IsLogging())
                AILogger.Info(npcAI, "AttackManager: distanceToTarget " + PositionUtil.GetDistance(npc, target, false));
            int maxChaseDistance = npc.IsBoss() ? 50 : npc.GetPosition().GetWorldMapInstance().GetTemplate().GetAiInfo().GetChaseTarget();
            if (!PositionUtil.IsInRange(npc, target, maxChaseDistance))
                return true;
        }
        double distanceToHome = npc.GetDistanceToSpawnLocation();
        // if npc is far away from home
        int chaseHome = npc.IsBoss() ? 150 : npc.GetPosition().GetWorldMapInstance().GetTemplate().GetAiInfo().GetChaseHome();
        if (distanceToHome > chaseHome)
        {
            return true;
        }
        // start thinking about home after 100 meters and no attack for 10 seconds (only for default monsters)
        if (chaseHome <= 200)
        { // TODO: Check Client and use chase_user_by_trace value
            if ((npc.GetGameStats().GetLastAttackTimeDelta() > 20 && npc.GetGameStats().GetLastAttackedTimeDelta() > 20)
                || (distanceToHome > chaseHome / 2 && npc.GetGameStats().GetLastAttackedTimeDelta() > 10))
            {
                return true;
            }
        }
        return false;
    }
}
