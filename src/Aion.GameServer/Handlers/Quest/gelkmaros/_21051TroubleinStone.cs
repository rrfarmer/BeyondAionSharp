using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author zhkchi, vlog
/// </summary>
public class _21051TroubleinStone : AbstractQuestHandler
{
    public _21051TroubleinStone() : base(21051)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(799291).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(799291).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 799291) // Aquila
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 1011);
                    default:
                        return SendQuestStartDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 799291) // Aquila
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 2375);
                    case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                        return CheckQuestItems(env, 0, 0, true, 5, 2716); // reward
                    case DialogAction.FINISH_DIALOG:
                        return SendQuestSelectionDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 799291) // Aquila
            {
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
