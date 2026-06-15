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
/// @author Ritsu, Majka
/// </summary>
public class _2239MalodorAntidote : AbstractQuestHandler
{
    public _2239MalodorAntidote() : base(2239)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203613).AddOnQuestStart(questId); // Gilungk
        qe.RegisterQuestNpc(203613).AddOnTalkEvent(questId); // Gilungk
        qe.RegisterQuestNpc(203630).AddOnTalkEvent(questId); // Vovetirn
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;
        if (env.GetVisibleObject() is Npc)
        {
            targetId = ((Npc)env.GetVisibleObject()).GetNpcId();
        }
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 203613)
            { // Gilungk
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 1011);
                }
                else
                {
                    return SendQuestStartDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            if (targetId == 203630)
            { // Vovetirn
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 0)
                        {
                            return SendQuestDialog(env, 1352);
                        }
                        if (var == 1)
                        {
                            return SendQuestDialog(env, 1693);
                        }
                        return false;
                    case DialogAction.SETPRO1:
                        if (var == 0)
                        {
                            qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                            UpdateQuestStatus(env);
                            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                            return true;
                        }
                        return false;
                    case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                        if (var == 1)
                        {
                            if (QuestService.CollectItemCheck(env, true))
                            {
                                if (!GiveQuestItem(env, 182203227, 1))
                                { // Ampha antidote
                                    return true;
                                }
                                qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 2);
                                UpdateQuestStatus(env);
                                return SendQuestDialog(env, 1779);
                            }
                            else
                            {
                                return SendQuestDialog(env, 1694);
                            }
                        }
                        break;
                    case DialogAction.SETPRO2:
                        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                        return true;
                }
            }
            if (targetId == 203613)
            { // Gilungk
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 3)
                        {
                            return SendQuestDialog(env, 2034);
                        }
                        return false;
                    case DialogAction.SETPRO3:
                        if (var == 3)
                        {
                            qs.SetStatus(QuestStatus.REWARD);
                            UpdateQuestStatus(env);
                            return SendQuestDialog(env, 5);
                        }
                        break;
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203613)
            { // Gilungk
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
