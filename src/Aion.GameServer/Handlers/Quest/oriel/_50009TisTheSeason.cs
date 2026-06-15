using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _50009TisTheSeason : AbstractQuestHandler
{
    public _50009TisTheSeason() : base(50009)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(831032).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(831038).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(831032).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(831038).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 831032 || targetId == 831038)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 1011);
                    case DialogAction.QUEST_ACCEPT_1:
                    case DialogAction.QUEST_ACCEPT_SIMPLE:
                        return SendQuestStartDialog(env);
                    case DialogAction.QUEST_REFUSE_1:
                    case DialogAction.QUEST_REFUSE_SIMPLE:
                        return SendQuestDialog(env, 1004);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 831032 || targetId == 831038)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 2375);
                    case DialogAction.SELECT_QUEST_REWARD:
                        ChangeQuestStep(env, 0, 0, true);
                        return SendQuestEndDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 831032 || targetId == 831038)
                return SendQuestEndDialog(env);
        }
        return false;
    }
}
