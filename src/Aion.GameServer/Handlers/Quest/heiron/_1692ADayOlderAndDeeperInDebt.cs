using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _1692ADayOlderAndDeeperInDebt : AbstractQuestHandler
{
    public _1692ADayOlderAndDeeperInDebt() : base(1692)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(798386).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(798386).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 798386)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 1011);
                }
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 798386)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (qs.GetQuestVarById(0) == 0)
                    {
                        return SendQuestDialog(env, 2375);
                    }
                }
                else if (dialogActionId == DialogAction.CHECK_USER_HAS_QUEST_ITEM)
                {
                    return CheckQuestItems(env, 0, 1, true, 5, 2716);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 798386)
            {
                if (dialogActionId == DialogAction.USE_OBJECT)
                {
                    return SendQuestDialog(env, 5);
                }
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
