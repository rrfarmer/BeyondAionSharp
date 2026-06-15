using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

public class _3013AClueLeftByTheDead : AbstractQuestHandler
{
    public _3013AClueLeftByTheDead() : base(3013)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(798132).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(798132).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798146).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        int targetId = 0;
        if (env.GetVisibleObject() is Npc npc)
            targetId = npc.GetNpcId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 798132)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 4762);
                    default:
                        return SendQuestStartDialog(env);
                }
            }
        }

        if (qs == null)
            return false;

        if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 798132:
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                        {
                            if (qs.GetQuestVarById(0) == 0)
                            {
                                return SendQuestDialog(env, 1011);
                            }
                            return false;
                        }
                        case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                        {
                            long itemCount = player.GetInventory().GetItemCountByItemId(182208008);
                            if (itemCount >= 1)
                            {
                                RemoveQuestItem(env, 182208008, itemCount);
                                ChangeQuestStep(env, 0, 1);
                                return SendQuestDialog(env, 10000);
                            }
                            else
                            {
                                return SendQuestDialog(env, 10001);
                            }
                        }
                    }
                    return false;
                case 798146:
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                        {
                            if (qs.GetQuestVarById(0) == 1)
                            {
                                return SendQuestDialog(env, 1352);
                            }
                            return false;
                        }
                        case DialogAction.SET_SUCCEED:
                        {
                            qs.SetStatus(QuestStatus.REWARD);
                            UpdateQuestStatus(env);
                            PacketSendUtility.SendPacket(player, new SmDialogWindow(env.GetVisibleObject().GetObjectId(), 10));
                            return true;
                        }
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 798132)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 10002);
                    case DialogAction.SELECT_QUEST_REWARD:
                        return SendQuestDialog(env, 5);
                    default:
                        return SendQuestEndDialog(env);
                }
            }
        }
        return false;
    }
}
