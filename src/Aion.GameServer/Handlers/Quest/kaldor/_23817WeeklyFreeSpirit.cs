using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/**
 * @author Ritsu
 */
public class _23817WeeklyFreeSpirit : AbstractQuestHandler
{
    public _23817WeeklyFreeSpirit() : base(23817)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(804590).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(804590).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(804594).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 804590)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 4762);
                    case DialogAction.QUEST_ACCEPT_SIMPLE:
                        return SendQuestStartDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            switch (targetId)
            {
                case 804594:
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            {
                                if (var == 0)
                                    return SendQuestDialog(env, 1011);
                                return false;
                            }
                        case DialogAction.SET_SUCCEED:
                            {
                                return DefaultCloseDialog(env, 0, 0, true, false);
                            }
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 804590)
                switch (dialogActionId)
                {
                    case DialogAction.USE_OBJECT:
                        return SendQuestDialog(env, 10002);
                    case DialogAction.SELECT_QUEST_REWARD:
                        return SendQuestDialog(env, 5);
                    default:
                        return SendQuestEndDialog(env);
                }
        }
        return false;
    }
}
