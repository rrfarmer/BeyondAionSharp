using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/**
 * @author Ritsu
 */
public class _80275EventEmpiresPast : AbstractQuestHandler
{
    public _80275EventEmpiresPast() : base(80275)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(831117).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(831117).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 831117)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else
                    return SendQuestStartDialog(env);
            }
        }

        if (qs == null)
            return false;

        else if (qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            switch (targetId)
            {
                case 831117:
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            {
                                if (var == 0)
                                    return SendQuestDialog(env, 2375);
                                return false;
                            }
                        case DialogAction.SELECT_QUEST_REWARD:
                            {
                                ChangeQuestStep(env, 0, 0, true);
                                return SendQuestDialog(env, 5);
                            }
                        case DialogAction.FINISH_DIALOG:
                            {
                                return SendQuestSelectionDialog(env);
                            }
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 831117)
                return SendQuestEndDialog(env);
        }
        return false;
    }
}
