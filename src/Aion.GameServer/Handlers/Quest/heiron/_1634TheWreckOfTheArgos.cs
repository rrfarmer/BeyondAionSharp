using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest
{
    /// <summary>
    /// @author Balthazar
    /// </summary>
    public class _1634TheWreckOfTheArgos : AbstractQuestHandler
    {
        public _1634TheWreckOfTheArgos() : base(1634)
        {
        }

        public override void Register()
        {
            qe.RegisterQuestNpc(204547).AddOnQuestStart(questId);
            qe.RegisterQuestNpc(204547).AddOnTalkEvent(questId);
            qe.RegisterQuestNpc(204540).AddOnTalkEvent(questId);
            qe.RegisterQuestNpc(790018).AddOnTalkEvent(questId);
            qe.RegisterQuestNpc(204541).AddOnTalkEvent(questId);
        }

        public override bool OnDialogEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);

            int targetId = 0;
            if (env.GetVisibleObject() is Npc)
                targetId = ((Npc)env.GetVisibleObject()).GetNpcId();

            if (qs == null || qs.IsStartable())
            {
                if (targetId == 204547)
                {
                    if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    {
                        return SendQuestDialog(env, 4762);
                    }
                    else
                        return SendQuestStartDialog(env);
                }
            }

            if (qs == null)
                return false;

            if (qs.GetStatus() == QuestStatus.START)
            {
                switch (targetId)
                {
                    case 204547:
                        switch (env.GetDialogActionId())
                        {
                            case DialogAction.QUEST_SELECT:
                                {
                                    long itemCount1 = player.GetInventory().GetItemCountByItemId(182201760);
                                    if (qs.GetQuestVarById(0) == 0 && itemCount1 >= 3)
                                    {
                                        return SendQuestDialog(env, 1011);
                                    }
                                    return false;
                                }
                            case DialogAction.SELECT_NONE_1:
                                {
                                    qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                                    UpdateQuestStatus(env);
                                    PacketSendUtility.SendPacket(player, new SmDialogWindow(env.GetVisibleObject().GetObjectId(), 0));
                                    return true;
                                }
                        }
                        return false;
                    case 204540:
                        switch (env.GetDialogActionId())
                        {
                            case DialogAction.QUEST_SELECT:
                                {
                                    return SendQuestDialog(env, 1693);
                                }
                            case DialogAction.SELECT3_1:
                                {
                                    qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                                    RemoveQuestItem(env, 182201760, 1);
                                    UpdateQuestStatus(env);
                                    PacketSendUtility.SendPacket(player, new SmDialogWindow(env.GetVisibleObject().GetObjectId(), 0));
                                    return true;
                                }
                        }
                        return false;
                    case 790018:
                        switch (env.GetDialogActionId())
                        {
                            case DialogAction.QUEST_SELECT:
                                {
                                    return SendQuestDialog(env, 2034);
                                }
                            case DialogAction.SELECT4_1:
                                {
                                    qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                                    RemoveQuestItem(env, 182201760, 1);
                                    qs.SetStatus(QuestStatus.REWARD);
                                    UpdateQuestStatus(env);
                                    PacketSendUtility.SendPacket(player, new SmDialogWindow(env.GetVisibleObject().GetObjectId(), 0));
                                    return true;
                                }
                        }
                        break;
                }
            }
            else if (qs.GetStatus() == QuestStatus.REWARD)
            {
                if (targetId == 204541)
                {
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                            return SendQuestDialog(env, 2375);
                        case DialogAction.SELECTED_QUEST_NOREWARD:
                            return SendQuestEndDialog(env, new int[] { 182201760 });
                    }
                    return SendQuestEndDialog(env);
                }
            }
            return false;
        }
    }
}
