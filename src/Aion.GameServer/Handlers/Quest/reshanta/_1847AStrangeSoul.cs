using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Hilgert
/// </summary>
public class _1847AStrangeSoul : AbstractQuestHandler
{
    public _1847AStrangeSoul() : base(1847)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(278592).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203933).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203991).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (env.GetVisibleObject() is Npc)
            targetId = ((Npc)env.GetVisibleObject()).GetNpcId();

        if (qs == null || qs.IsStartable())
        {
            if (env.GetDialogActionId() == DialogAction.QUEST_ACCEPT_1)
            {
                QuestService.StartQuest(env);
                PacketSendUtility.SendPacket(player, new SmDialogWindow(0, 0));
                return true;
            }
            else
                PacketSendUtility.SendPacket(player, new SmDialogWindow(0, 0));
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 278592)
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
            }
            else if (targetId == 203933)
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
            }
            else if (targetId == 203991)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 2375);
                else if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD)
                {
                    qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                    qs.SetStatus(QuestStatus.REWARD);
                    UpdateQuestStatus(env);
                    PacketSendUtility.SendPacket(player, new SmDialogWindow(env.GetVisibleObject().GetObjectId(), 10));
                    return SendQuestEndDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD && targetId == 203991)
        {
            return SendQuestEndDialog(env);
        }
        return false;
    }
}
