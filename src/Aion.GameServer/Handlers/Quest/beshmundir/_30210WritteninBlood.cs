using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _30210WritteninBlood : AbstractQuestHandler
{
    public _30210WritteninBlood() : base(30210)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203837).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(203837).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798941).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int targetId = env.GetTargetId();
        int dialogActionId = env.GetDialogActionId();
        if (qs == null || qs.IsStartable())
        {
            if (targetId == 203837)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 4762);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            if (targetId == 203837)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else if (dialogActionId == DialogAction.CHECK_USER_HAS_QUEST_ITEM)
                {
                    if (var == 0 && player.GetInventory().GetItemCountByItemId(182209613) >= 30)
                    {
                        RemoveQuestItem(env, 182209613, 30);
                        ChangeQuestStep(env, 0, 0, true);
                        return SendQuestDialog(env, 10000);
                    }
                    else
                        return SendQuestDialog(env, 10001);
                }
                return false;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 798941)
            {
                switch (dialogActionId)
                {
                    case DialogAction.USE_OBJECT:
                        return SendQuestDialog(env, 10002);
                    case DialogAction.SELECT_QUEST_REWARD:
                        return SendQuestDialog(env, 5);
                    default:
                        return SendQuestEndDialog(env);
                }
            }
        }
        return false;
    }
}
