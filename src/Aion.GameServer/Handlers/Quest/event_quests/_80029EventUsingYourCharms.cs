using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _80029EventUsingYourCharms : AbstractQuestHandler
{
    public _80029EventUsingYourCharms() : base(80029)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(799766).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(799766).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = env.GetTargetId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        if (qs == null || qs.IsStartable())
            return false;

        if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 799766)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else if (env.GetDialogActionId() == DialogAction.QUEST_ACCEPT_1)
                    return SendQuestDialog(env, 2375);
                else if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD)
                {
                    DefaultCloseDialog(env, 0, 0, true, true);
                    return SendQuestDialog(env, 5);
                }
                else if (env.GetDialogActionId() == DialogAction.SELECTED_QUEST_NOREWARD)
                    return SendQuestRewardDialog(env, 799766, 5);
                else
                    return SendQuestStartDialog(env);
            }
        }
        return SendQuestRewardDialog(env, 799766, 0);
    }
}
