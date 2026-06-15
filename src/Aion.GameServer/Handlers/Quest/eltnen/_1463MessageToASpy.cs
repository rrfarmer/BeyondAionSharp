using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Balthazar
/// </summary>
public class _1463MessageToASpy : AbstractQuestHandler
{
    public _1463MessageToASpy() : base(1463)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203940).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(203940).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203903).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(204424).AddOnTalkEvent(questId);
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
            if (targetId == 203940)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 1011);
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
                case 203903:
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                        {
                            if (qs.GetQuestVarById(0) == 0)
                            {
                                return SendQuestDialog(env, 1352);
                            }
                            else if (qs.GetQuestVarById(0) == 2)
                            {
                                return SendQuestDialog(env, 2375);
                            }
                            return false;
                        }
                        case DialogAction.SETPRO1:
                        {
                            if (player.GetInventory().GetItemCountByItemId(182201382) == 0)
                                if (!GiveQuestItem(env, 182201382, 1))
                                    return true;
                            qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                            UpdateQuestStatus(env);
                            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                            return true;
                        }
                        case DialogAction.SELECT_QUEST_REWARD:
                        {
                            qs.SetQuestVar(3);
                            RemoveQuestItem(env, 182201383, 1);
                            qs.SetStatus(QuestStatus.REWARD);
                            UpdateQuestStatus(env);
                            return SendQuestEndDialog(env);
                        }
                    }
                    return false;
                case 204424:
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                        {
                            if (qs.GetQuestVarById(0) == 1)
                            {
                                return SendQuestDialog(env, 1693);
                            }
                            return false;
                        }
                        case DialogAction.SETPRO2:
                        {
                            qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                            RemoveQuestItem(env, 182201382, 1);
                            if (player.GetInventory().GetItemCountByItemId(182201383) == 0)
                                if (!GiveQuestItem(env, 182201383, 1))
                                    return true;
                            UpdateQuestStatus(env);
                            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                            return true;
                        }
                        default:
                            return SendQuestEndDialog(env);
                    }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203903)
            {
                if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD)
                    return SendQuestDialog(env, 5);
                else
                    return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
