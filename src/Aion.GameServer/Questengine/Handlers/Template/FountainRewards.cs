using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Questengine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Questengine.Handlers.Template;

/// <summary>Java parity: questEngine/handlers/template/FountainRewards (Wakizashi, vlog, Bobobear, Luzien, Pad). static import DialogAction.*→DialogAction.X; SETPRO1 case can fall through to return false (explicit break). QuestService/PacketSendUtility/SM_SYSTEM_MESSAGE red-tolerated.</summary>
public class FountainRewards : AbstractTemplateQuestHandler
{
    private readonly HashSet<int> startNpcIds = new();

    public FountainRewards(int questId, List<int> startNpcIds) : base(questId)
    {
        if (startNpcIds != null)
            this.startNpcIds.UnionWith(startNpcIds);
    }

    public override void Register()
    {
        foreach (int startNpcId in startNpcIds)
        {
            qe.RegisterQuestNpc(startNpcId).AddOnQuestStart(questId);
            qe.RegisterQuestNpc(startNpcId).AddOnTalkEvent(questId);
        }
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = env.GetTargetId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();

        if (qs == null || qs.IsStartable())
        {
            if (startNpcIds.Contains(targetId)) // Coin Fountain
            {
                switch (dialogActionId)
                {
                    case DialogAction.USE_OBJECT:
                        if (!QuestService.InventoryItemCheck(env, true))
                        {
                            return true;
                        }
                        else
                            return SendQuestSelectionDialog(env);
                    case DialogAction.SETPRO1:
                        if (QuestService.CollectItemCheck(env, false))
                        {
                            if (!player.GetInventory().IsFullSpecialCube())
                            {
                                if (QuestService.StartQuest(env))
                                {
                                    ChangeQuestStep(env, 0, 0, true);
                                    return SendQuestDialog(env, 5);
                                }
                            }
                            else
                            {
                                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_FULL_INVENTORY());
                                return SendQuestSelectionDialog(env);
                            }
                        }
                        else
                        {
                            return SendQuestSelectionDialog(env);
                        }
                        break;
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (startNpcIds.Contains(targetId)) // Coin Fountain
            {
                if (dialogActionId == DialogAction.SELECTED_QUEST_NOREWARD)
                {
                    if (QuestService.CollectItemCheck(env, true))
                        return SendQuestEndDialog(env);
                }
                else
                {
                    return QuestService.AbandonQuest(player, questId);
                }
            }
        }
        return false;
    }
}
