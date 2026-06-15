using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _1484ChiyorinrinerksRequest : AbstractQuestHandler
{
    public _1484ChiyorinrinerksRequest() : base(1484)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(798127).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(798127).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(204045).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(204048).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(204011).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798126).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;
        if (env.GetVisibleObject() is Npc npc)
            targetId = npc.GetNpcId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (targetId == 798127)
        {
            if (qs == null || qs.IsStartable())
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (targetId == 204045)
        {
            if (qs != null && qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) == 0)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1352);
                else if (env.GetDialogActionId() == DialogAction.SETPRO1)
                {
                    ChangeQuestStep(env, 0, 1);
                    GiveQuestItem(env, workItems[0].GetItemId(), workItems[0].GetCount());
                    return CloseDialogWindow(env);
                }
            }
        }
        else if (targetId == 204048)
        {
            if (qs != null && qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) == 1)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1693);
                else if (env.GetDialogActionId() == DialogAction.SETPRO2)
                {
                    ChangeQuestStep(env, 1, 2);
                    GiveQuestItem(env, workItems[1].GetItemId(), workItems[1].GetCount());
                    return CloseDialogWindow(env);
                }
            }
        }
        else if (targetId == 204011)
        {
            if (qs != null && qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) == 2)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 2034);
                else if (env.GetDialogActionId() == DialogAction.SETPRO3)
                {
                    ChangeQuestStep(env, 2, 3);
                    GiveQuestItem(env, workItems[^1].GetItemId(), workItems[^1].GetCount());
                    return CloseDialogWindow(env);
                }
            }
        }
        else if (targetId == 798126)
        {
            if (qs != null)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT && qs.GetStatus() == QuestStatus.START)
                    return SendQuestDialog(env, 2375);
                else if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD && qs.GetStatus() != QuestStatus.COMPLETE)
                    ChangeQuestStep(env, 3, 4, true);
                return SendQuestEndDialog(env);
            }
        }
        return base.OnDialogEvent(env);
    }
}
