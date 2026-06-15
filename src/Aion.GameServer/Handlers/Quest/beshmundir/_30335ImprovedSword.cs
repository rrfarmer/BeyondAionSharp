using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

namespace Aion.GameServer.Handlers.Quest;

public class _30335ImprovedSword : AbstractQuestHandler
{
    public _30335ImprovedSword() : base(30335)
    {
    }

    public override void Register()
    {
        int[] debilkarims = { 286904, 281419, 215795 };
        qe.RegisterQuestNpc(799336).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(799336).AddOnTalkEvent(questId);
        qe.RegisterOnGetItem(182209733, questId);
        foreach (int debilkarim in debilkarims)
        {
            qe.RegisterQuestNpc(debilkarim).AddOnKillEvent(questId);
        }
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 799336)
            { // Tataka
                if (player.GetInventory().GetItemCountByItemId(100000944) >= 1)
                {
                    if (dialogActionId == DialogAction.QUEST_SELECT)
                    {
                        return SendQuestDialog(env, 4762);
                    }
                    else
                    {
                        return SendQuestStartDialog(env);
                    }
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 799336)
            { // Tataka
                if (dialogActionId == DialogAction.USE_OBJECT)
                {
                    if (player.GetInventory().GetItemCountByItemId(182209733) > 0)
                    {
                        return SendQuestDialog(env, 10002);
                    }
                }
                else
                {
                    RemoveQuestItem(env, 182209733, 1);
                    return SendQuestEndDialog(env);
                }
            }
        }
        return false;
    }

    public override bool OnKillEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int targetId = env.GetTargetId();
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 286904:
                case 281419:
                case 215795:
                    if (QuestService.CollectItemCheck(env, true))
                    {
                        return GiveQuestItem(env, 182209733, 1);
                    }
                    break;
            }
        }
        return false;
    }

    public override bool OnGetItemEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            ChangeQuestStep(env, 0, 0, true); // reward
            return true;
        }
        return false;
    }
}
