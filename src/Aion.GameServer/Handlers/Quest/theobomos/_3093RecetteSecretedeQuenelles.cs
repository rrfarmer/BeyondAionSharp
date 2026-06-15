using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

public class _3093RecetteSecretedeQuenelles : AbstractQuestHandler
{
    public _3093RecetteSecretedeQuenelles() : base(3093)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(798185).AddOnQuestStart(questId); // Bororinerk
        qe.RegisterQuestNpc(798185).AddOnTalkEvent(questId); // Bororinerk
        qe.RegisterQuestNpc(798177).AddOnTalkEvent(questId); // Gastak
        qe.RegisterQuestNpc(798179).AddOnTalkEvent(questId); // Jabala
        qe.RegisterQuestNpc(203784).AddOnTalkEvent(questId); // Hestia
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;
        if (env.GetVisibleObject() is Npc npc)
            targetId = npc.GetNpcId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null || qs.IsStartable())
        {
            if (targetId == 798185) // Bororinerk
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else if (env.GetDialogActionId() == DialogAction.QUEST_ACCEPT_1)
                {
                    if (GiveQuestItem(env, 182206062, 1))
                        return SendQuestStartDialog(env);
                }

                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD) // Reward
        {
            if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                return SendQuestDialog(env, 2375);
            else if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD)
            {
                RemoveQuestItem(env, 182208052, 1);
                return SendQuestEndDialog(env);
            }
            else
                return SendQuestEndDialog(env);
        }
        else if (targetId == 798177) // Gastak
        {
            if (qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) == 1)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1352);
                else if (env.GetDialogActionId() == DialogAction.SETPRO1)
                {
                    qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                    UpdateQuestStatus(env);
                    PacketSendUtility.SendPacket(player, new SmDialogWindow(env.GetVisibleObject().GetObjectId(), 10));
                    return true;
                }
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (targetId == 798179) // Jabala
        {
            if (qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) == 2)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1693);
                else if (env.GetDialogActionId() == DialogAction.SETPRO2)
                {
                    qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                    UpdateQuestStatus(env);
                    PacketSendUtility.SendPacket(player, new SmDialogWindow(env.GetVisibleObject().GetObjectId(), 10));
                    return true;
                }
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (targetId == 203784) // Hestia
        {
            if (qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) == 3)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 2034);
                else if (env.GetDialogActionId() == DialogAction.SETPRO3)
                {
                    GiveQuestItem(env, 182208052, 1);
                    qs.SetStatus(QuestStatus.REWARD);
                    UpdateQuestStatus(env);
                    PacketSendUtility.SendPacket(player, new SmDialogWindow(env.GetVisibleObject().GetObjectId(), 10));
                    return true;
                }
                else
                    return SendQuestStartDialog(env);
            }
        }

        return false;
    }
}
