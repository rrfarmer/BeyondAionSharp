using System.Collections.Generic;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Skill;
using Aion.GameServer.SkillEngine;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Ai.Handler;

/// <summary>
/// Java parity: ai/handler/ReturningEventHandler (ATracer). NPC return-home / back-home dispatch (returning state, walk/move, dispel,
/// post-spawn skills, instance handler onBackHome). AiEventType.BACK_HOME -> AiEventType.BackHome; AIState.RETURNING/IDLE -> Returning/
/// Idle. EmoteManager/WalkManager/SkillEngine/DispelSlotType red-tolerated where unported.
/// </summary>
public class ReturningEventHandler
{
    public static void OnNotAtHome(NpcAI npcAI)
    {
        if(Aion.GameServer.Controllers.Movement.SkyTempleNpcNavigation.Enabled(npcAI.GetOwner()))
        {
            var owner=npcAI.GetOwner();
            if(!Aion.GameServer.Controllers.Movement.SkyTempleNpcReturnState.PrepareEncounterEnd(owner)||
                !Aion.GameServer.Controllers.Movement.SkyTempleNpcReturnState.TryRead(owner,out var frame)||!frame.Enter(owner))return;
            if(npcAI.SetStateIfNot(AIState.RETURNING))
            {npcAI.SetSubStateIfNot(AISubState.NONE);EmoteManager.EmoteStartReturning(owner);}
            // No Java StartWalking fast path and no one-metre birth approximation.
            owner.GetMoveController().ReturnToLastStepOrSpawn();
            return;
        }
        if (npcAI.IsLogging())
        {
            AILogger.Info(npcAI, "onNotAtHome");
        }
        if (!npcAI.IsMoveSupported())
        {
            npcAI.OnGeneralEvent(AiEventType.BackHome);
        }
        else if (npcAI.SetStateIfNot(AIState.RETURNING))
        {
            npcAI.SetSubStateIfNot(AISubState.NONE);
            if (npcAI.IsLogging())
            {
                AILogger.Info(npcAI, "returning and restoring");
            }
            Npc npc = npcAI.GetOwner();
            EmoteManager.EmoteStartReturning(npc);
            if (npc.IsPathWalker() && WalkManager.StartWalking(npcAI))
                return;
            npc.GetMoveController().ReturnToLastStepOrSpawn();
        }
    }

    public static void OnBackHome(NpcAI npcAI)
    {
        if(Aion.GameServer.Controllers.Movement.SkyTempleNpcNavigation.Enabled(npcAI.GetOwner()))
        {
            if(Aion.GameServer.Controllers.Movement.SkyTempleNpcReturnState.TryRead(npcAI.GetOwner(),out var frame))CompleteRetailReturn(npcAI,frame);
            return;
        }
        if (npcAI.IsLogging())
        {
            AILogger.Info(npcAI, "onBackHome");
        }
        npcAI.GetOwner().GetMoveController().ClearBackSteps();
        if (npcAI.SetStateIfNot(AIState.IDLE))
        {
            npcAI.SetSubStateIfNot(AISubState.NONE);
            npcAI.GetOwner().GetEffectController().RemoveByDispelSlotType(DispelSlotType.BUFF);
            EmoteManager.EmoteStartIdling(npcAI.GetOwner());
            npcAI.Think();
            Npc npc = npcAI.GetOwner();
            List<NpcSkillEntry> skills = npc.GetSkillList().GetPostSpawnSkills();
            if (skills.Count != 0)
                foreach (NpcSkillEntry s in skills)
                    Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(npc, s.GetSkillId(), s.GetSkillLevel(), npc).UseWithoutPropSkill();
        }
        npcAI.GetOwner().GetPosition().GetWorldMapInstance().GetInstanceHandler().OnBackHome(npcAI.GetOwner());
    }

    internal static void CompleteRetailReturn(NpcAI npcAI,Aion.GameServer.Controllers.Movement.SkyTempleNpcReturnState.Frame frame)
    {
        var owner=npcAI.GetOwner();
        lock(owner)
        {
            lock(owner.GetMoveController())
            {
                if(!frame.Complete(owner))return;
                owner.GetMoveController().ClearBackSteps();
                owner.GetPosition().SetH(frame.Heading);npcAI.SetStateIfNot(AIState.IDLE);npcAI.SetSubStateIfNot(AISubState.NONE);
            }
            owner.GetMoveController().StopRetailReturnMotion(frame);
        // Reevaluation may launch skills. No controller/nav/SkillState monitor
        // crosses those callbacks. The owner monitor excludes replacement birth;
        // this is not a global safety claim for arbitrary custom callbacks.
        if(!frame.LifeIsCurrent(owner))return;
        Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(owner,new Aion.GameServer.Network.Aion.ServerPackets.SM_HEADING_UPDATE(owner));
        EmoteManager.EmoteStartIdling(owner);
        if(!frame.LifeIsCurrent(owner))return;
        owner.GetPosition().GetWorldMapInstance().GetInstanceHandler().OnBackHome(owner);
        owner.GetKnownList().ForEachObject(candidate=>
        {
            if(frame.LifeIsCurrent(owner)&&candidate is Creature creature)CreatureEventHandler.CheckAggro(npcAI,creature);
        });
        if(frame.LifeIsCurrent(owner))npcAI.Think();
        }
    }
}
