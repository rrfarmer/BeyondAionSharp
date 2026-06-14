using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _11053TheseShoesAreMadeForStalking : AbstractQuestHandler
{
    public _11053TheseShoesAreMadeForStalking() : base(11053)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(799015).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(799015).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();
        if (qs == null || qs.IsStartable())
        {
            if (targetId == 799015)
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
            if (targetId == 799015)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 2375);
                }
                else if (dialogActionId == DialogAction.CHECK_USER_HAS_QUEST_ITEM)
                {
                    long itemCount = player.GetInventory().GetItemCountByItemId(182206838);
                    if (player.GetInventory().TryDecreaseKinah(50000) && itemCount > 29)
                    {
                        player.GetInventory().DecreaseByItemId(182206838, 30);
                        ChangeQuestStep(env, 0, 0, true);
                        return SendQuestDialog(env, 5);
                    }
                    else
                        return SendQuestDialog(env, 2716);
                }
                else if (dialogActionId == DialogAction.FINISH_DIALOG)
                {
                    return DefaultCloseDialog(env, 0, 0);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 799015)
                return SendQuestEndDialog(env);
        }
        return false;
    }
}
