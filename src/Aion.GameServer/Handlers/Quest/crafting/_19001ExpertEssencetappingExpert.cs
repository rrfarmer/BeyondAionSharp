using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>Java parity: quest/crafting/_19001ExpertEssencetappingExpert (Gigi, Pad).</summary>
public class _19001ExpertEssencetappingExpert : AbstractQuestHandler
{
    public _19001ExpertEssencetappingExpert() : base(19001)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203780).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(203780).AddOnTalkEvent(questId);
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
            if (targetId == 203780)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 1011);
                    case DialogAction.ASK_QUEST_ACCEPT:
                        return SendQuestDialog(env, 4);
                    case DialogAction.QUEST_ACCEPT_1:
                    case DialogAction.QUEST_ACCEPT_SIMPLE:
                        return SendQuestStartDialog(env, 182206127, 1);
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
                    player.GetSkillList().AddSkill(player, 30002, 400);
                    RemoveQuestItem(env, 182206127, 1);
                    return SendQuestEndDialog(env);
                }
            }
        }
        return false;
    }
}
