using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _51009TheGiftOfCharity : AbstractQuestHandler
{
    public _51009TheGiftOfCharity() : base(51009)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(831033).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(831039).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(831033).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(831039).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 831033 || targetId == 831039)
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
            if (targetId == 831033 || targetId == 831039)
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
            if (targetId == 831033 || targetId == 831039)
                return SendQuestEndDialog(env);
        }
        return false;
    }
}
