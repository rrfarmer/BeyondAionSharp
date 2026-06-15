using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _3088InCiderTrading : AbstractQuestHandler
{
    public _3088InCiderTrading() : base(3088)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(798202).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(798202).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798201).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798204).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798132).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798166).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 798202)
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
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 798202)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (qs.GetQuestVarById(0) == 0)
                    {
                        if (qs.GetRewardGroup() == null)
                            return SendQuestDialog(env, 1011);
                        else if (qs.GetRewardGroup() == 0)
                            return SendQuestDialog(env, 1352);
                        else if (qs.GetRewardGroup() == 1)
                            return SendQuestDialog(env, 2034);
                        else if (qs.GetRewardGroup() == 2)
                            return SendQuestDialog(env, 3398);
                    }
                }
                else if (dialogActionId == DialogAction.SELECT1_1)
                {
                    if (player.GetInventory().GetItemCountByItemId(160003020) >= 1)
                        return SendQuestDialog(env, 1012);
                    else
                        return SendQuestDialog(env, 1267);
                }
                else if (dialogActionId == DialogAction.SELECT1_2)
                {
                    if (player.GetInventory().GetItemCountByItemId(160003020) >= 10)
                        return SendQuestDialog(env, 1097);
                    else
                        return SendQuestDialog(env, 1267);
                }
                else if (dialogActionId == DialogAction.SELECT1_3)
                {
                    if (player.GetInventory().GetItemCountByItemId(160003020) >= 100)
                        return SendQuestDialog(env, 1182);
                    else
                        return SendQuestDialog(env, 1267);
                }
                else if (dialogActionId == DialogAction.SELECT1)
                {
                    return SendQuestDialog(env, 1011);
                }
                else if (dialogActionId == DialogAction.SETPRO1)
                {
                    qs.SetRewardGroup(0);
                    player.GetInventory().DecreaseByItemId(160003020, 1);
                    return SendQuestDialog(env, 1352);
                }
                else if (dialogActionId == DialogAction.SETPRO2)
                {
                    return DefaultCloseDialog(env, 0, 2, workItems[0].GetItemId(), workItems[0].GetCount());
                }
                else if (dialogActionId == DialogAction.SETPRO3)
                {
                    qs.SetRewardGroup(1);
                    player.GetInventory().DecreaseByItemId(160003020, 10);
                    return SendQuestDialog(env, 2034);
                }
                else if (dialogActionId == DialogAction.SETPRO4)
                {
                    return DefaultCloseDialog(env, 0, 4);
                }
                else if (dialogActionId == DialogAction.SETPRO7)
                {
                    qs.SetRewardGroup(2);
                    player.GetInventory().DecreaseByItemId(160003020, 100);
                    return SendQuestDialog(env, 3398);
                }
                else if (dialogActionId == DialogAction.SETPRO8)
                {
                    return DefaultCloseDialog(env, 0, 8);
                }
            }
            if (targetId == 798201)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (qs.GetQuestVarById(0) == 2)
                    {
                        return SendQuestDialog(env, 1693);
                    }
                    if (qs.GetQuestVarById(0) == 5)
                    {
                        return SendQuestDialog(env, 2716);
                    }
                }
                else if (dialogActionId == DialogAction.SETPRO6)
                {
                    return DefaultCloseDialog(env, 5, 6);
                }
                else if (dialogActionId == DialogAction.SELECT_QUEST_REWARD)
                {
                    return DefaultCloseDialog(env, 2, 2, true, true);
                }
            }
            if (targetId == 798204)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (qs.GetQuestVarById(0) == 4)
                    {
                        return SendQuestDialog(env, 2375);
                    }
                }
                else if (dialogActionId == DialogAction.SETPRO5)
                {
                    return DefaultCloseDialog(env, 4, 5);
                }
            }
            if (targetId == 798132)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (qs.GetQuestVarById(0) == 6)
                    {
                        return SendQuestDialog(env, 3057);
                    }
                }
                else if (dialogActionId == DialogAction.SELECT_QUEST_REWARD)
                {
                    return DefaultCloseDialog(env, 6, 6, true, true);
                }
            }
            if (targetId == 798166)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (qs.GetQuestVarById(0) == 8)
                    {
                        if (player.GetInventory().GetItemCountByItemId(182208064) >= 1)
                            return SendQuestDialog(env, 3739);
                    }
                }
                else if (dialogActionId == DialogAction.SELECT_QUEST_REWARD)
                {
                    player.GetInventory().DecreaseByItemId(182208064, 1);
                    return DefaultCloseDialog(env, 8, 8, true, true);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 798201 && qs.GetQuestVarById(0) == 2 || targetId == 798132 && qs.GetQuestVarById(0) == 6
                || targetId == 798166 && qs.GetQuestVarById(0) == 8)
            {
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
