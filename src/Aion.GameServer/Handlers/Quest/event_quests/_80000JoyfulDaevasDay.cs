using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

namespace Aion.GameServer.Handlers.Quest;

public class _80000JoyfulDaevasDay : AbstractQuestHandler
{
    public _80000JoyfulDaevasDay() : base(80000)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(798415).AddOnTalkEvent(questId); // Ias
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
        else if (env.GetTargetId() == 798415) // Ias
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
