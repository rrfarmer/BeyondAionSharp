using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

namespace Aion.GameServer.Handlers.Quest;

public class _80001FestiveDaevasDay : AbstractQuestHandler
{
    public _80001FestiveDaevasDay() : base(80001)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(798417).AddOnTalkEvent(questId); // Belenus
        qe.RegisterOnLevelChanged(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        if (env.GetTargetId() == 0)
        {
            if (env.GetDialogActionId() == DialogAction.QUEST_ACCEPT_1)
            {
                QuestService.StartEventQuest(env, QuestStatus.START);
                CloseDialogWindow(env);
                return true;
            }
        }
        else if (env.GetTargetId() == 798417) // Belenus
        {
            if (qs != null)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT && qs.GetStatus() == QuestStatus.START)
                {
                    return SendQuestDialog(env, 2375);
                }
                else if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD)
                {
                    qs.SetQuestVar(1);
                    qs.SetStatus(QuestStatus.REWARD);
                    UpdateQuestStatus(env);
                }
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
