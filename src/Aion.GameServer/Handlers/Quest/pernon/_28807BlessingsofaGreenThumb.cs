using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _28807BlessingsofaGreenThumb : AbstractQuestHandler
{
    public _28807BlessingsofaGreenThumb() : base(28807)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(830211).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(830211).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(730524).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = env.GetTargetId();
        int dialogActionId = env.GetDialogActionId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 830211)
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
            switch (targetId)
            {
                case 730524:
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            return SendQuestDialog(env, 1352);
                        case DialogAction.SELECT2_1:
                            return SendQuestDialog(env, 1353);
                        case DialogAction.SETPRO1:
                            return DefaultCloseDialog(env, 0, 1);
                    }
                    break;
                case 830211:
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                        {
                            return SendQuestDialog(env, 2375);
                        }
                        case DialogAction.SELECT_QUEST_REWARD:
                            ChangeQuestStep(env, 1, 1, true);
                            return SendQuestDialog(env, 5);
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 830211)
            {
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
