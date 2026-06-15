using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>Java parity: quest/crafting/_19000ExpertEssencetappersTest (Gigi, vlog).</summary>
public class _19000ExpertEssencetappersTest : AbstractQuestHandler
{
    private static readonly int itemId1 = 152003004;
    private static readonly int itemId2 = 152003005;
    private static readonly int itemId3 = 152003006;

    public _19000ExpertEssencetappersTest() : base(19000)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203780).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(203780).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203781).AddOnTalkEvent(questId);
        qe.RegisterOnGetItem(122001250, questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 203780) // Cornelius
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
            int var = qs.GetQuestVarById(0);
            switch (targetId)
            {
                case 203781: // Sabotes
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 0)
                            {
                                return SendQuestDialog(env, 1011);
                            }
                            return false;
                        case DialogAction.SETPRO1:
                            GiveQuestItem(env, 122001250, 1);
                            return SendQuestSelectionDialog(env);
                    }
                    break;
                case 203780: // Cornelius
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 1)
                            {
                                return SendQuestDialog(env, 1352);
                            }
                            return false;
                        case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                            long itemCount1 = player.GetInventory().GetItemCountByItemId(itemId1);
                            long itemCount2 = player.GetInventory().GetItemCountByItemId(itemId2);
                            long itemCount3 = player.GetInventory().GetItemCountByItemId(itemId3);
                            if (itemCount1 >= 1 && itemCount2 >= 1 && itemCount3 >= 1)
                            {
                                RemoveQuestItem(env, itemId1, itemCount1);
                                RemoveQuestItem(env, itemId2, itemCount2);
                                RemoveQuestItem(env, itemId3, itemCount3);
                                qs.SetStatus(QuestStatus.REWARD);
                                UpdateQuestStatus(env);
                                return SendQuestDialog(env, 5);
                            }
                            else
                            {
                                return SendQuestDialog(env, 10001);
                            }
                        case DialogAction.FINISH_DIALOG:
                            return SendQuestSelectionDialog(env);
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203780) // Cornelius
            {
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }

    public override bool OnGetItemEvent(QuestEnv env)
    {
        return DefaultOnGetItemEvent(env, 0, 1, false); // 1
    }
}
