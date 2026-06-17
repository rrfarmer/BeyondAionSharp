using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Gigi, Pad
/// </summary>
public class _29002ExpertAethertappersTest : AbstractQuestHandler
{
    private const int itemId1 = 152003007;
    private const int itemId2 = 152003008;

    public _29002ExpertAethertappersTest() : base(29002)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204257).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(204257).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(204099).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 204257)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 4762);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 204099:
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            return SendQuestDialog(env, 1011);
                        case DialogAction.SETPRO1:
                            if (!GiveQuestItem(env, 122001251, 1))
                                return true;
                            qs.SetQuestVarById(0, 1);
                            UpdateQuestStatus(env);
                            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                            return true;
                    }
                    return false;
                case 204257:
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                        {
                            long itemCount1 = player.GetInventory().GetItemCountByItemId(itemId1);
                            long itemCount2 = player.GetInventory().GetItemCountByItemId(itemId2);
                            if (itemCount1 >= 1 && itemCount2 >= 1)
                            {
                                RemoveQuestItem(env, itemId1, itemCount1);
                                RemoveQuestItem(env, itemId2, itemCount2);
                                qs.SetStatus(QuestStatus.REWARD);
                                UpdateQuestStatus(env);
                                return SendQuestDialog(env, 5);
                            }
                            else
                            {
                                return SendQuestDialog(env, 10001);
                            }
                        }
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 204257)
            {
                if (dialogActionId == DialogAction.CHECK_USER_HAS_QUEST_ITEM)
                    return SendQuestDialog(env, 5);
                else
                    return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
