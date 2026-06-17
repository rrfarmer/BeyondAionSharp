using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

public class _3095ADecisiveClue : AbstractQuestHandler
{
    public _3095ADecisiveClue() : base(3095)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(700422).AddOnQuestStart(questId); // Faded Book
        qe.RegisterQuestNpc(700422).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798225).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203898).AddOnTalkEvent(questId);
        qe.RegisterQuestItem(182208053, questId);
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
            if (targetId == 0)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_ACCEPT_1)
                {
                    QuestService.StartQuest(env);
                    return CloseDialogWindow(env);
                }
            }
            else if (targetId == 700422)
            {
                return GiveQuestItem(env, 182208053, 1);
            }
        }

        switch (targetId)
        {
            case 798225:
                if (qs != null && qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) == 0)
                {
                    if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                        return SendQuestDialog(env, 1352);
                    else if (env.GetDialogActionId() == DialogAction.SETPRO1)
                    {
                        qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                        RemoveQuestItem(env, 182208053, 1);
                        UpdateQuestStatus(env);
                        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                        return true;
                    }
                    else
                        return SendQuestStartDialog(env);
                }
                else if (qs != null && qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) == 2)
                {
                    if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                        return SendQuestDialog(env, 2375);
                    else if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD)
                    {
                        qs.SetStatus(QuestStatus.REWARD);
                        UpdateQuestStatus(env);
                        return SendQuestDialog(env, 5);
                    }
                    else
                        return SendQuestStartDialog(env);
                }
                else if (qs != null && qs.GetStatus() == QuestStatus.REWARD)
                    return SendQuestEndDialog(env);
                return false;
            case 203898:
                if (qs != null && qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) == 1)
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
                break;
        }
        return false;
    }

    public override HandlerResult OnItemUseEvent(QuestEnv env, Item item)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null || qs.IsStartable())
        {
            return HandlerResultExtensions.FromBoolean(SendQuestDialog(env, 4));
        }
        return HandlerResult.FAILED;
    }
}
