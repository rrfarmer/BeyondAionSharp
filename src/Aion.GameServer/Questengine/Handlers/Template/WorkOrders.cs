using System.Collections.Generic;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Quest;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Items;

namespace Aion.GameServer.QuestEngine.Handlers.Template;

/// <summary>Java parity: questEngine/handlers/template/WorkOrders (Mr. Poke, Bobobear, Pad). DialogPage.X.id()→.Id(); DataManager/QuestService/RecipeService/ItemService red-tolerated.</summary>
public class WorkOrders : AbstractTemplateQuestHandler
{
    private readonly HashSet<int> startNpcIds = new();
    private readonly List<QuestItems> giveComponents = new();
    private readonly int recipeId;

    public WorkOrders(int questId, List<int> startNpcIds, List<QuestItems> giveComponents, int recipeId) : base(questId)
    {
        this.startNpcIds.UnionWith(startNpcIds);
        this.giveComponents.AddRange(giveComponents);
        this.recipeId = recipeId;
    }

    public override void Register()
    {
        foreach (int startNpcId in startNpcIds)
        {
            qe.RegisterQuestNpc(startNpcId).AddOnQuestStart(questId);
            qe.RegisterQuestNpc(startNpcId).AddOnTalkEvent(questId);
        }
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (startNpcIds.Contains(targetId))
        {
            if (qs == null || qs.IsStartable())
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, DialogPage.ASK_QUEST_ACCEPT_WINDOW.Id());
                    case DialogAction.QUEST_ACCEPT_1:
                        if (RecipeService.ValidateNewRecipe(player, recipeId) != null)
                        {
                            if (QuestService.StartQuest(env))
                            {
                                foreach (QuestItems qi in giveComponents)
                                    ItemService.AddItem(player, qi.GetItemId(), qi.GetCount(), true);
                                RecipeService.AddRecipe(player, recipeId, false);
                                CloseDialogWindow(env);
                                return true;
                            }
                        }
                        return false;
                    case DialogAction.COMBINE_TASK:
                        env.SetQuestId(0);
                        return SendQuestDialog(env, DialogPage.COMBINETASK_WINDOW.Id());
                }
            }
            else if (qs.GetStatus() == QuestStatus.START)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    int var = qs.GetQuestVarById(0);
                    if (QuestService.CollectItemCheck(env, false))
                    {
                        ChangeQuestStep(env, var, var, true); // reward
                        QuestService.RemoveQuestWorkItems(player, qs);
                        return SendQuestDialog(env, DialogPage.SELECT_QUEST_REWARD_WINDOW1.Id());
                    }
                    else
                    {
                        return SendQuestSelectionDialog(env);
                    }
                }
            }
            else if (qs.GetStatus() == QuestStatus.REWARD)
            {
                CollectItems collectItems = DataManager.QUEST_DATA.GetQuestById(questId).GetCollectItems();
                long count = 0;
                foreach (CollectItem collectItem in collectItems.GetCollectItem())
                {
                    count = player.GetInventory().GetItemCountByItemId(collectItem.GetItemId());
                    if (count > 0)
                        player.GetInventory().DecreaseByItemId(collectItem.GetItemId(), count);
                }
                player.GetRecipeList().DeleteRecipe(player, recipeId);
                if (dialogActionId == DialogAction.USE_OBJECT)
                {
                    QuestService.FinishQuest(env);
                    env.SetQuestId(questId);
                    return SendQuestDialog(env, 1008);
                }
                else
                {
                    return SendQuestEndDialog(env);
                }
            }
        }
        return false;
    }
}
