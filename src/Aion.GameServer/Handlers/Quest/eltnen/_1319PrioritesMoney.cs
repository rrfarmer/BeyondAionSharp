using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Xitanium
/// </summary>
public class _1319PrioritesMoney : AbstractQuestHandler
{
    public _1319PrioritesMoney() : base(1319)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203908).AddOnQuestStart(questId); // Priorite
        qe.RegisterQuestNpc(203908).AddOnTalkEvent(questId); // Priorite
        qe.RegisterQuestNpc(203923).AddOnTalkEvent(questId); // Krato
        qe.RegisterQuestNpc(203910).AddOnTalkEvent(questId); // Hebestis
        qe.RegisterQuestNpc(203906).AddOnTalkEvent(questId); // Benos
        qe.RegisterQuestNpc(203915).AddOnTalkEvent(questId); // Diokles
        qe.RegisterQuestNpc(203907).AddOnTalkEvent(questId); // Tuskeos
        qe.RegisterQuestNpc(798050).AddOnTalkEvent(questId); // Girrinerk
        qe.RegisterQuestNpc(798049).AddOnTalkEvent(questId); // Shaoranyerk
        qe.RegisterQuestNpc(205240).AddOnTalkEvent(questId); // Arnesonerk
    }

    public override bool OnDialogEvent(QuestEnv env)
    { // NEED FIX ITEM
        Player player = env.GetPlayer();
        int targetId = 0;
        if (env.GetVisibleObject() is Npc)
            targetId = ((Npc)env.GetVisibleObject()).GetNpcId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null || qs.IsStartable())
        {
            if (targetId == 203908)
            { // Priorite
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        { // Reward
            if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                return SendQuestDialog(env, 4080);
            else if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD)
            {
                qs.SetQuestVar(8);
                qs.SetStatus(QuestStatus.REWARD);
                UpdateQuestStatus(env);
                return SendQuestEndDialog(env);
            }
            else
                return SendQuestEndDialog(env);
        }
        else if (targetId == 203923)
        { // Krato
            if (qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) == 0)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1352);
                else if (env.GetDialogActionId() == DialogAction.SETPRO1)
                {
                    qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                    UpdateQuestStatus(env);
                    PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                    return true;
                }
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (targetId == 203910)
        { // Hebestis
            if (qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) == 1)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1693);
                else if (env.GetDialogActionId() == DialogAction.SETPRO2)
                {
                    qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                    UpdateQuestStatus(env);
                    PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                    return true;
                }
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (targetId == 203906)
        { // Benos
            if (qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) == 2)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 2034);
                else if (env.GetDialogActionId() == DialogAction.SETPRO3)
                {
                    qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                    UpdateQuestStatus(env);
                    PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                    return true;
                }
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (targetId == 203915)
        { // Diokles
            if (qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) == 3)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 2375);
                else if (env.GetDialogActionId() == DialogAction.SETPRO4)
                {
                    qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                    UpdateQuestStatus(env);
                    PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                    return true;
                }
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (targetId == 203907)
        { // Tuskeos
            if (qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) == 4)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 2716);
                else if (env.GetDialogActionId() == DialogAction.SETPRO5)
                {
                    qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                    UpdateQuestStatus(env);
                    PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                    return true;
                }
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (targetId == 798050)
        { // Girrinerk
            if (qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) == 5)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 3057);
                else if (env.GetDialogActionId() == DialogAction.SETPRO6)
                {
                    qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                    UpdateQuestStatus(env);
                    PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                    return true;
                }
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (targetId == 798049)
        { // Shaoranranerk
            if (qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) == 6)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 3398);
                else if (env.GetDialogActionId() == DialogAction.SETPRO7)
                {
                    qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                    UpdateQuestStatus(env);
                    PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                    return true;
                }
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (targetId == 205240)
        { // Arnesonerk
            if (qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) == 7)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 3739);
                else if (env.GetDialogActionId() == DialogAction.SETPRO8 && qs.GetStatus() != QuestStatus.COMPLETE)
                {
                    qs.SetStatus(QuestStatus.REWARD);
                    UpdateQuestStatus(env);
                    PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                    return true;
                }
                else
                    return SendQuestStartDialog(env);
            }
        }

        return false;
    }
}
