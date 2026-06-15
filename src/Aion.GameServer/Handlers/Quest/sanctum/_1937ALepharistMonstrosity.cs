using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Gigi
/// </summary>
public class _1937ALepharistMonstrosity : AbstractQuestHandler
{
    public _1937ALepharistMonstrosity() : base(1937)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203833).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(203833).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203837).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203761).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(204573).AddOnTalkEvent(questId);
    }

    public override void OnLevelChangedEvent(Player player)
    {
        DefaultOnLevelChangedEvent(player, 1936);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;
        if (env.GetVisibleObject() is Npc)
            targetId = ((Npc)env.GetVisibleObject()).GetNpcId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null)
        {
            if (targetId == 203833)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 203837:
                    if (qs.GetQuestVarById(0) == 0)
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
                    break;
                case 203761:
                    if (qs.GetQuestVarById(0) == 1)
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
                    break;
                case 203833:
                    if (qs.GetQuestVarById(0) == 2)
                    {
                        if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                            return SendQuestDialog(env, 2034);
                        else if (env.GetDialogActionId() == DialogAction.SETPRO3)
                        {
                            qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                            UpdateQuestStatus(env);
                            PacketSendUtility.SendPacket(player, new SmDialogWindow(env.GetVisibleObject().GetObjectId(), 10));
                            return true;
                        }
                    }
                    break;
                case 204573:
                    if (qs.GetQuestVarById(0) == 3)
                    {
                        if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                            return SendQuestDialog(env, 2375);
                        else if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD)
                        {
                            qs.SetStatus(QuestStatus.REWARD);
                            UpdateQuestStatus(env);
                            return SendQuestEndDialog(env);
                        }
                        else
                            return SendQuestEndDialog(env);
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 204573)
                return SendQuestEndDialog(env);
        }
        return false;
    }
}
