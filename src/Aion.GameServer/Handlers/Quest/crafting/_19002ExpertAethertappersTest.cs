using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>Java parity: quest/crafting/_19002ExpertAethertappersTest (Gigi, Pad).</summary>
public class _19002ExpertAethertappersTest : AbstractQuestHandler
{
    private static readonly int itemId1 = 152003007;
    private static readonly int itemId2 = 152003008;

    public _19002ExpertAethertappersTest() : base(19002)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203782).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(203782).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203783).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 203782)
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
                case 203783:
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
                case 203782:
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
            if (targetId == 203782)
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
