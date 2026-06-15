using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _3913ASecretSummons : AbstractQuestHandler
{
    public _3913ASecretSummons() : base(3913)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203725).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(203725).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203752).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(204656).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = env.GetTargetId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 203725)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 1011);
                    case DialogAction.ASK_QUEST_ACCEPT:
                        return SendQuestDialog(env, 4);
                    case DialogAction.QUEST_ACCEPT_1:
                        return SendQuestStartDialog(env);
                }
                return base.OnDialogEvent(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 203752)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 1352);
                    case DialogAction.SELECT2_1:
                        return SendQuestDialog(env, 1353);
                    case DialogAction.SELECT2_1_1:
                        return SendQuestDialog(env, 1354);
                    case DialogAction.SELECT2_1_1_1:
                        return SendQuestDialog(env, 1355);
                    case DialogAction.SETPRO1:
                        return DefaultCloseDialog(env, 0, 1);
                }
            }
            else if (targetId == 204656)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 2375);
                    case DialogAction.SELECT_QUEST_REWARD:
                        return DefaultCloseDialog(env, 1, 2, true, true);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 204656)
            {
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
