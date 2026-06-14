using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _80330TransformWithTheMagicCane : AbstractQuestHandler
{
    public _80330TransformWithTheMagicCane() : base(80330)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(831527).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(831527).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 831527)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 1011);
                    case DialogAction.QUEST_ACCEPT_1:
                    case DialogAction.QUEST_ACCEPT_SIMPLE:
                        return SendQuestStartDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 831527)
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                    case DialogAction.SELECT_QUEST_REWARD:
                        ChangeQuestStep(env, 0, 0, true); // reward
                        return SendQuestDialog(env, 5);
                }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 831527)
                return SendQuestEndDialog(env);
        }
        return false;
    }
}
