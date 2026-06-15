using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

public class _2722TheComfortsofHome : AbstractQuestHandler
{
    public _2722TheComfortsofHome() : base(2722)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(278047).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(278056).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(278126).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(278043).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(278032).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(278037).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(278040).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(278068).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(278066).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(278047).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (env.GetVisibleObject() is Npc npc)
            targetId = npc.GetNpcId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 278047)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 4762);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            if (targetId == 278056)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else if (env.GetDialogActionId() == DialogAction.SETPRO1)
                {
                    qs.SetQuestVarById(0, var + 1);
                    UpdateQuestStatus(env);
                    PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                    return true;
                }
            }
            else if (targetId == 278126)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1352);
                else if (env.GetDialogActionId() == DialogAction.SETPRO2)
                {
                    qs.SetQuestVarById(0, var + 1);
                    UpdateQuestStatus(env);
                    PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                    return true;
                }
            }
            else if (targetId == 278043)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1693);
                else if (env.GetDialogActionId() == DialogAction.SETPRO3)
                {
                    qs.SetQuestVarById(0, var + 1);
                    UpdateQuestStatus(env);
                    PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                    return true;
                }
            }
            else if (targetId == 278032)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 2034);
                else if (env.GetDialogActionId() == DialogAction.SETPRO4)
                {
                    qs.SetQuestVarById(0, var + 1);
                    UpdateQuestStatus(env);
                    PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                    return true;
                }
            }
            else if (targetId == 278037)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 2375);
                else if (env.GetDialogActionId() == DialogAction.SETPRO5)
                {
                    qs.SetQuestVarById(0, var + 1);
                    UpdateQuestStatus(env);
                    PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                    return true;
                }
            }
            else if (targetId == 278040)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 2716);
                else if (env.GetDialogActionId() == DialogAction.SETPRO6)
                {
                    qs.SetQuestVarById(0, var + 1);
                    UpdateQuestStatus(env);
                    PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                    return true;
                }
            }
            else if (targetId == 278068)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 3057);
                else if (env.GetDialogActionId() == DialogAction.SETPRO7)
                {
                    qs.SetQuestVarById(0, var + 1);
                    UpdateQuestStatus(env);
                    PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                    return true;
                }
            }
            else if (targetId == 278066)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 3398);
                else if (env.GetDialogActionId() == DialogAction.SET_SUCCEED)
                {
                    if (!GiveQuestItem(env, 182205654, 1))
                        return true;
                    qs.SetStatus(QuestStatus.REWARD);
                    UpdateQuestStatus(env);
                    PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                    return true;
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD && targetId == 278047)
        {
            return SendQuestEndDialog(env);
        }
        return false;
    }
}
