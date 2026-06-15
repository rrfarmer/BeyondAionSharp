using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _28832TakingtheTour : AbstractQuestHandler
{
    public _28832TakingtheTour() : base(28832)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(830532).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(830532).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(830085).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 830532)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            switch (targetId)
            {
                case 830085:
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
                            PlayQuestMovie(env, 802);
                            ChangeQuestStep(env, 0, 0, true);
                            return SendQuestDialog(env, 5);
                        }
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 830085)
                return SendQuestEndDialog(env);
        }
        return false;
    }
}
