using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Ai.Handler;

/// <summary>
/// Java parity: ai/handler/TalkEventHandler (ATracer). NPC talk/simple-talk/finish-talk dispatch (dialog quest hook, town-residence
/// special case, dialog window). instanceof Player -> is Player; DialogAction.USE_OBJECT const; AISubState.TALK -> Talk; AIState.FOLLOWING
/// -> Following. QuestEngine/TownService/DialogPage red-tolerated where unported.
/// </summary>
public class TalkEventHandler
{
    public static void OnTalk(NpcAI npcAI, Creature creature)
    {
        OnSimpleTalk(npcAI, creature);

        if (creature is Player player)
        {
            if (QuestEngine.GetInstance().OnDialog(new QuestEnv(npcAI.GetOwner(), player, 0, DialogAction.USE_OBJECT)))
                return;
            // only player villagers can use villager npcs in oriel/pernon
            switch (npcAI.GetOwner().GetObjectTemplate().GetTitleId())
            {
                case 462877:
                    int playerTownId = TownService.GetInstance().GetTownResidence(player);
                    int currentTownId = TownService.GetInstance().GetTownIdByPosition(player);
                    if (playerTownId != currentTownId)
                    {
                        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(npcAI.GetOwner().GetObjectId(), 44));
                    }
                    else
                    {
                        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(npcAI.GetOwner().GetObjectId(), 10));
                    }
                    return;
                default:
                    int dialogPageId = DialogPage.GetStartPageId(npcAI.GetOwner(), player);
                    PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(npcAI.GetOwner().GetObjectId(), dialogPageId));
                    break;
            }
        }
    }

    public static void OnSimpleTalk(NpcAI npcAI, Creature creature)
    {
        if (npcAI.GetOwner().GetObjectTemplate().IsDialogNpc())
        {
            npcAI.SetSubStateIfNot(AiSubState.Talk);
            npcAI.GetOwner().SetTarget(creature);
        }
    }

    public static void OnFinishTalk(NpcAI npcAI, Creature creature)
    {
        Npc owner = npcAI.GetOwner();
        if (owner.IsTargeting(creature.GetObjectId()))
        {
            if (npcAI.GetState() == AiState.Following)
            {
                npcAI.Think();
            }
            else
            {
                owner.SetTarget(null);
            }
        }
    }
}
