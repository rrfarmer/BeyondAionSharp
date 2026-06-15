using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author zhkchi
/// </summary>
public class _21053DramataDrama : AbstractQuestHandler
{
    public _21053DramataDrama() : base(21053)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(799320).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(799320).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 799320)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 1011);
                    case DialogAction.SELECT1_1:
                        return SendQuestDialog(env, 1012);
                    case DialogAction.ASK_QUEST_ACCEPT:
                        return SendQuestDialog(env, 4);
                    case DialogAction.QUEST_ACCEPT_1:
                        return SendQuestStartDialog(env);
                    case DialogAction.QUEST_REFUSE_1:
                        return SendQuestDialog(env, 1004);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            if (targetId == 799320)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 2375);
                    case DialogAction.SELECT4:
                        return SendQuestDialog(env, 2034);
                    case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                        return CheckQuestItems(env, var, var, true, 5, 2716);
                    case DialogAction.FINISH_DIALOG:
                        return SendQuestSelectionDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 799320)
            {
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
