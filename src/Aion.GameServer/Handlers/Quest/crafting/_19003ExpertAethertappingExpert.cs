using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>Java parity: quest/crafting/_19003ExpertAethertappingExpert (Gigi, Pad).</summary>
public class _19003ExpertAethertappingExpert : AbstractQuestHandler
{
    public _19003ExpertAethertappingExpert() : base(19003)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203782).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(203782).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203700).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 203782)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 1011);
                    case DialogAction.ASK_QUEST_ACCEPT:
                        return SendQuestDialog(env, 4);
                    case DialogAction.QUEST_ACCEPT_1:
                    case DialogAction.QUEST_ACCEPT_SIMPLE:
                        return SendQuestStartDialog(env, 182206128, 1);
                    case DialogAction.QUEST_REFUSE_1:
                    case DialogAction.QUEST_REFUSE_SIMPLE:
                        return SendQuestDialog(env, 1004);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 203700:
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            qs.SetStatus(QuestStatus.REWARD);
                            UpdateQuestStatus(env);
                            return SendQuestDialog(env, 2375);
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203700)
            {
                if (dialogActionId == DialogAction.CHECK_USER_HAS_QUEST_ITEM)
                    return SendQuestDialog(env, 5);
                else
                {
                    player.GetSkillList().AddSkill(player, 30003, 400);
                    RemoveQuestItem(env, 182206128, 1);
                    return SendQuestEndDialog(env);
                }
            }
        }
        return false;
    }
}
