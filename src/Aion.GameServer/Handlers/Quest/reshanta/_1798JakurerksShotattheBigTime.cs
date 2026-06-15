using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Hilgert
/// </summary>
public class _1798JakurerksShotattheBigTime : AbstractQuestHandler
{
    public _1798JakurerksShotattheBigTime() : base(1798)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(279007).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(279007).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(263568).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(263266).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(264768).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(271053).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(266553).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(270151).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(269251).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(268051).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(260235).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (env.GetVisibleObject() is Npc npc)
            targetId = npc.GetNpcId();

        if (targetId == 279007)
        {
            if (qs == null || qs.IsStartable())
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 4762);
                else
                    return SendQuestStartDialog(env);
            }
            else if (qs.GetStatus() == QuestStatus.REWARD)
                return SendQuestEndDialog(env);
        }
        else if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 263568)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else if (env.GetDialogActionId() == DialogAction.SETPRO1)
                {
                    qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                    UpdateQuestStatus(env);
                    PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                    return true;
                }
            }
            else if (targetId == 263266)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1352);
                else if (env.GetDialogActionId() == DialogAction.SETPRO2)
                {
                    qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                    UpdateQuestStatus(env);
                    PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                    return true;
                }
            }
            else if (targetId == 264768)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1693);
                else if (env.GetDialogActionId() == DialogAction.SETPRO3)
                {
                    qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                    UpdateQuestStatus(env);
                    PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                    return true;
                }
            }
            else if (targetId == 271053)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 2035);
                else if (env.GetDialogActionId() == DialogAction.SETPRO4)
                {
                    qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                    UpdateQuestStatus(env);
                    PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                    return true;
                }
            }
            else if (targetId == 266553)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 2375);
                else if (env.GetDialogActionId() == DialogAction.SETPRO5)
                {
                    qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                    UpdateQuestStatus(env);
                    PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                    return true;
                }
            }
            else if (targetId == 270151)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 2716);
                else if (env.GetDialogActionId() == DialogAction.SETPRO6)
                {
                    qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                    UpdateQuestStatus(env);
                    PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                    return true;
                }
            }
            else if (targetId == 269251)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 3057);
                else if (env.GetDialogActionId() == DialogAction.SETPRO7)
                {
                    qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                    UpdateQuestStatus(env);
                    PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                    return true;
                }
            }
            else if (targetId == 268051)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 3398);
                else if (env.GetDialogActionId() == DialogAction.SETPRO8)
                {
                    qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                    UpdateQuestStatus(env);
                    PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                    return true;
                }
            }
            else if (targetId == 260235)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 3740);
                else if (env.GetDialogActionId() == DialogAction.SET_SUCCEED)
                {
                    qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                    qs.SetStatus(QuestStatus.REWARD);
                    UpdateQuestStatus(env);
                    PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                    return true;
                }
            }
        }
        return false;
    }
}
