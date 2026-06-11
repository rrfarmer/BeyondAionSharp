using System;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Ai.Handler;

/// <summary>
/// Java parity: ai/handler/ThinkEventHandler (ATracer). NPC think tick: dead/thinking guards, inactive-region branch, fight/idle
/// dispatch. try/finally unsetThinking; switch on AiState (Fight/Idle/Walking); instanceof+pattern -> is Creature; AISubState.FREEZE/NONE;
/// AIEventType.NOT_AT_HOME/ATTACK_FINISH/BACK_HOME -> NotAtHome/AttackFinish/BackHome; schedule(Runnable,500) -> async ThreadPool idiom.
/// AttackManager/WalkManager/SM_HEADING_UPDATE red-tolerated where unported.
/// </summary>
public class ThinkEventHandler
{
    public static void OnThink(NpcAI npcAI)
    {
        if (npcAI.IsDead())
        {
            AILogger.Info(npcAI, "can't think in dead state");
            return;
        }
        if (!npcAI.SetThinking())
        {
            AILogger.Info(npcAI, "skipped onThink because AI is already thinking");
            return;
        }
        try
        {
            if (!npcAI.GetOwner().GetPosition().IsMapRegionActive() || npcAI.GetSubState() == AiSubState.Freeze)
            {
                ThinkInInactiveRegion(npcAI);
                return;
            }
            if (npcAI.IsLogging())
            {
                AILogger.Info(npcAI, "think in ai state: " + npcAI.GetState());
            }
            switch (npcAI.GetState())
            {
                case AiState.Fight:
                    ThinkAttack(npcAI);
                    break;
                case AiState.Idle:
                    ThinkIdle(npcAI);
                    break;
            }
        }
        finally
        {
            npcAI.UnsetThinking();
        }
    }

    private static void ThinkInInactiveRegion(NpcAI npcAI)
    {
        if (npcAI.IsInState(AiState.Walking))
        {
            WalkManager.StopWalking(npcAI);
            return;
        }
        if (!npcAI.CanThink())
        {
            return;
        }
        if (npcAI.IsLogging())
        {
            AILogger.Info(npcAI, "think (inactive region) in ai state: " + npcAI.GetState());
        }
        switch (npcAI.GetState())
        {
            case AiState.Fight:
                ThinkAttack(npcAI);
                break;
            default:
                if (!npcAI.GetOwner().IsAtSpawnLocation())
                {
                    npcAI.OnGeneralEvent(AiEventType.NotAtHome);
                }
                break;
        }
    }

    public static void ThinkAttack(NpcAI npcAI)
    {
        Npc npc = npcAI.GetOwner();
        if (npc.GetTarget() is Creature target && npc.GetAggroList().IsHating(target))
        {
            AttackManager.ScheduleNextAttack(npcAI);
        }
        else
        {
            npcAI.SetSubStateIfNot(AiSubState.None);
            npc.ClearQueuedSkills();
            npc.GetGameStats().SetLastSkill(null);
            npc.GetGameStats().ResetFightStats();
            npcAI.OnGeneralEvent(AiEventType.AttackFinish);
            npcAI.OnGeneralEvent(npc.IsAtSpawnLocation() ? AiEventType.BackHome : AiEventType.NotAtHome);
        }
    }

    public static void ThinkIdle(NpcAI npcAI)
    {
        if (npcAI.IsMoveSupported() && npcAI.GetOwner().IsWalker())
            WalkManager.StartWalking(npcAI);
        else if (ShouldResetHeading(npcAI))
        {
            ThreadPoolManager.GetInstance().Schedule(ct =>
            {
                if (ShouldResetHeading(npcAI))
                {
                    npcAI.GetPosition().SetH(npcAI.GetOwner().GetSpawn().GetHeading());
                    PacketSendUtility.BroadcastPacket(npcAI.GetOwner(), new SM_HEADING_UPDATE(npcAI.GetOwner()));
                }
                return ValueTask.CompletedTask;
            }, TimeSpan.FromMilliseconds(500));
        }
    }

    private static bool ShouldResetHeading(NpcAI npcAI)
    {
        SpawnTemplate spawn = npcAI.GetOwner().GetSpawn();
        return npcAI.GetTarget() == null && spawn != null && !npcAI.GetOwner().GetMoveController().IsInMove() && npcAI.GetPosition().GetHeading() != spawn.GetHeading() && npcAI.GetOwner().IsAtSpawnLocation();
    }
}
