using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _1367MabangtahsFeast : AbstractQuestHandler
{
    private static readonly int ITEM_ID_1 = 182201331;
    private static readonly int ITEM_ID_2 = 182201332;
    private static readonly int ITEM_ID_3 = 182201333;

    private long itemCount1, itemCount2, itemCount3;

    public _1367MabangtahsFeast() : base(1367)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204023).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(204023).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;
        if (env.GetVisibleObject() is Npc npc)
            targetId = npc.GetNpcId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (targetId == 204023)
        {
            if (qs == null || qs.IsStartable())
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else
                    return SendQuestStartDialog(env);
            }
            else if (qs.GetStatus() == QuestStatus.START)
            {
                itemCount1 = player.GetInventory().GetItemCountByItemId(ITEM_ID_1);
                itemCount2 = player.GetInventory().GetItemCountByItemId(ITEM_ID_2);
                itemCount3 = player.GetInventory().GetItemCountByItemId(ITEM_ID_3);
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT && qs.GetQuestVarById(0) == 0)
                {
                    if (itemCount1 >= 1 || itemCount2 >= 5 || itemCount3 >= 2)
                    {
                        return SendQuestDialog(env, 1352);
                    }
                    else
                    {
                        return SendQuestDialog(env, 1693);
                    }
                }
                else if (env.GetDialogActionId() == DialogAction.SETPRO1)
                {
                    if (itemCount1 >= 1)
                    {
                        qs.SetStatus(QuestStatus.REWARD);
                        UpdateQuestStatus(env);
                        RemoveQuestItems(env);
                        qs.SetRewardGroup(0);
                        return SendQuestDialog(env, DialogPageExtensions.GetRewardPageByIndex(qs.GetRewardGroup()).Id());
                    }
                    else
                        return SendQuestDialog(env, 1352);
                }
                else if (env.GetDialogActionId() == DialogAction.SETPRO2)
                {
                    if (itemCount2 >= 5)
                    {
                        qs.SetStatus(QuestStatus.REWARD);
                        UpdateQuestStatus(env);
                        RemoveQuestItems(env);
                        qs.SetRewardGroup(1);
                        return SendQuestDialog(env, DialogPageExtensions.GetRewardPageByIndex(qs.GetRewardGroup()).Id());
                    }
                    else
                        return SendQuestDialog(env, 1352);
                }
                else if (env.GetDialogActionId() == DialogAction.SETPRO3)
                {
                    if (itemCount3 >= 2)
                    {
                        qs.SetStatus(QuestStatus.REWARD);
                        UpdateQuestStatus(env);
                        RemoveQuestItems(env);
                        qs.SetRewardGroup(2);
                        return SendQuestDialog(env, DialogPageExtensions.GetRewardPageByIndex(qs.GetRewardGroup()).Id());
                    }
                    else
                        return SendQuestDialog(env, 1352);
                }
                else
                    return SendQuestStartDialog(env);
            }
            else if (qs.GetStatus() == QuestStatus.REWARD)
            {
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }

    private void RemoveQuestItems(QuestEnv env)
    {
        RemoveQuestItem(env, ITEM_ID_1, itemCount1);
        RemoveQuestItem(env, ITEM_ID_2, itemCount2);
        RemoveQuestItem(env, ITEM_ID_3, itemCount3);
    }
}
