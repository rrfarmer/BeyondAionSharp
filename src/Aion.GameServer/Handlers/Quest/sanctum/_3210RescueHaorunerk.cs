using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _3210RescueHaorunerk : AbstractQuestHandler
{
    public _3210RescueHaorunerk() : base(3210)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(798318).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(798318).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798331).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798333).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(215056).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(215080).AddOnKillEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();

        int targetId = 0;
        if (env.GetVisibleObject() is Npc)
            targetId = ((Npc)env.GetVisibleObject()).GetNpcId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 798318)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 4762);
                    case DialogAction.ASK_QUEST_ACCEPT:
                        return SendQuestDialog(env, 4);
                    case DialogAction.QUEST_REFUSE_1:
                        return SendQuestDialog(env, 1004);
                    case DialogAction.QUEST_ACCEPT_1:
                        return SendQuestStartDialog(env);
                }
            }
        }

        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 798333 && qs.GetQuestVarById(0) == 0)
            { // Haorunerk's Corpse
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 1011);
                }
                else if (dialogActionId == DialogAction.SETPRO1)
                {
                    return DefaultCloseDialog(env, 0, 1);
                }
            }
        }

        if (targetId == 798331)
        {
            if (qs != null && qs.GetStatus() == QuestStatus.START)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 10002);
                }
                if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD && qs.GetQuestVarById(1) == 1 && qs.GetQuestVarById(2) == 1)
                {
                    qs.SetStatus(QuestStatus.REWARD);
                    UpdateQuestStatus(env);
                    return SendQuestDialog(env, 5);
                }
            }
            return SendQuestEndDialog(env);
        }
        return false;
    }

    public override bool OnKillEvent(QuestEnv env)
    {
        if (DefaultOnKillEvent(env, 215056, 0, 1, 1) || DefaultOnKillEvent(env, 215080, 0, 1, 2))
        {
            return true;
        }
        return false;
    }
}
