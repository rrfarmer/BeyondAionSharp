using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Effects;

namespace Aion.GameServer.Ai.Handler;

/// <summary>
/// Java parity: ai/handler/AttackEventHandler (ATracer). Attack/forced-attack/attack-complete/finish dispatch with fear/confuse gating.
/// AIState.RETURNING/IDLE/WALKING/FEAR/CONFUSE/FIGHT -> PascalCase; AiEventType.NOT_AT_HOME -> AiEventType.NotAtHome; AbnormalState
/// .FEAR/CONFUSE SCREAMING. AttackManager/EmoteManager/WalkManager red-tolerated where unported.
/// </summary>
public class AttackEventHandler
{
    public static void OnAttack(NpcAI npcAI, Creature creature)
    {
        if (npcAI.IsLogging())
        {
            AILogger.Info(npcAI, "onAttack");
        }
        if (creature == null || creature.IsDead())
        {
            return;
        }
        // TODO lock or better switch
        if (npcAI.IsInState(AIState.RETURNING))
        {
            npcAI.GetOwner().GetMoveController().AbortMove();
            npcAI.SetStateIfNot(AIState.IDLE);
            npcAI.OnGeneralEvent(AiEventType.NotAtHome);
            return;
        }
        if (!npcAI.CanThink())
        {
            return;
        }
        if (npcAI.IsInState(AIState.WALKING))
        {
            WalkManager.StopWalking(npcAI);
        }
        npcAI.GetOwner().GetGameStats().RenewLastAttackedTime();
        bool allowFight = npcAI.GetState() != AIState.FEAR && !npcAI.GetOwner().GetEffectController().IsAbnormalSet(AbnormalState.FEAR)
                && npcAI.GetState() != AIState.CONFUSE && !npcAI.GetOwner().GetEffectController().IsAbnormalSet(AbnormalState.CONFUSE);
        if (allowFight && npcAI.SetStateIfNot(AIState.FIGHT))
        {
            if (npcAI.IsLogging())
                AILogger.Info(npcAI, "onAttack() -> startAttacking");
            npcAI.SetSubStateIfNot(AISubState.NONE);
            if (npcAI.GetOwner().CanSee(creature))
                npcAI.GetOwner().SetTarget(creature);
            AttackManager.StartAttacking(npcAI);
            ShoutEventHandler.OnAttackBegin(npcAI);
        }
    }

    public static void OnForcedAttack(NpcAI npcAI)
    {
        OnAttack(npcAI, (Creature)npcAI.GetOwner().GetTarget());
    }

    public static void OnAttackComplete(NpcAI npcAI)
    {
        if (npcAI.IsLogging())
        {
            AILogger.Info(npcAI, "onAttackComplete: " + npcAI.GetOwner().GetGameStats().GetLastAttackTimeDelta());
        }
        npcAI.GetOwner().GetGameStats().RenewLastAttackTime();
        AttackManager.ScheduleNextAttack(npcAI);
    }

    public static void OnFinishAttack(NpcAI npcAI)
    {
        if (!npcAI.CanThink())
        {
            return;
        }
        if (npcAI.IsLogging())
        {
            AILogger.Info(npcAI, "onFinishAttack");
        }
        Npc npc = npcAI.GetOwner();
        EmoteManager.EmoteStopAttacking(npc);
        ShoutEventHandler.OnAttackEnd(npcAI);
        npc.GetController().LoseAggro(true);
        npc.SetSkillNumber(0);
    }
}
