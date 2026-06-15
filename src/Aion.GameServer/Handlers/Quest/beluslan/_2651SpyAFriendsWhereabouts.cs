using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _2651SpyAFriendsWhereabouts : AbstractQuestHandler
{
    public _2651SpyAFriendsWhereabouts() : base(2651)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204775).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(204775).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(204764).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(204650).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = env.GetTargetId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 204775) // Betoni
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 1011);
                }
                else
                {
                    return SendQuestStartDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            switch (targetId)
            {
                case 204764: // Epona
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 0)
                            {
                                return SendQuestDialog(env, 1352);
                            }
                            return false;
                        case DialogAction.SETPRO1:
                            return DefaultCloseDialog(env, 0, 1); // 1
                    }
                    break;
                case 204650: // Nesteto
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 1)
                            {
                                return SendQuestDialog(env, 2375);
                            }
                            return false;
                        case DialogAction.SELECT_QUEST_REWARD:
                            ChangeQuestStep(env, 1, 1, true); // reward
                            return SendQuestDialog(env, 5);
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 204650) // Nesteto
            {
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
