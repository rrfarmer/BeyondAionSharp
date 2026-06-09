using System.Collections.Generic;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Templates;
using Aion.GameServer.Questengine.Model;
using Aion.GameServer.Services;

namespace Aion.GameServer.Questengine.Handlers.Template;

/// <summary>Java parity: questEngine/handlers/template/RelicRewards (Bobobear, Rolandas, Pad). Set.addAll→UnionWith; DataManager/QuestService/CommonData red-tolerated.</summary>
public class RelicRewards : AbstractTemplateQuestHandler
{
    private readonly HashSet<int> startNpcIds = new();
    private bool isDataDriven;

    public RelicRewards(int questId, List<int> startNpcIds) : base(questId)
    {
        if (startNpcIds != null)
            this.startNpcIds.UnionWith(startNpcIds);
        isDataDriven = DataManager.QUEST_DATA.GetQuestById(questId).IsDataDriven();
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

        if (qs == null || qs.IsStartable())
        {
            if (startNpcIds.Contains(targetId))
            {
                switch (dialogActionId)
                {
                    case DialogAction.EXCHANGE_COIN:
                        QuestTemplate template = DataManager.QUEST_DATA.GetQuestById(env.GetQuestId());
                        if (player.GetCommonData().GetLevel() >= template.GetMinlevelPermitted())
                        {
                            if (QuestService.CheckAndGetCollectItemQuestRewardCategory(env) != -1)
                                return SendQuestDialog(env, isDataDriven ? 4762 : 1011);
                            else
                                return SendQuestDialog(env, 3398);
                        }
                        else
                            return SendQuestDialog(env, 3398);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) == 0)
        {
            if (startNpcIds.Contains(targetId))
            {
                int rewardId = -1;
                switch (dialogActionId)
                {
                    case DialogAction.USE_OBJECT:
                        return SendQuestDialog(env, isDataDriven ? 4762 : 1011);
                    case DialogAction.SELECT1:
                        rewardId = QuestService.CheckAndGetCollectItemQuestRewardCategory(env, 0);
                        break;
                    case DialogAction.SELECT2:
                        rewardId = QuestService.CheckAndGetCollectItemQuestRewardCategory(env, 1);
                        break;
                    case DialogAction.SELECT3:
                        rewardId = QuestService.CheckAndGetCollectItemQuestRewardCategory(env, 2);
                        break;
                    case DialogAction.SELECT4:
                        rewardId = QuestService.CheckAndGetCollectItemQuestRewardCategory(env, 3);
                        break;
                }
                if (rewardId != -1)
                {
                    qs.SetRewardGroup(rewardId);
                    qs.SetQuestVar(rewardId + 1);
                    qs.SetStatus(QuestStatus.REWARD);
                    UpdateQuestStatus(env);
                    return SendQuestDialog(env, rewardId + 5);
                }
                else
                    return SendQuestDialog(env, 1009);
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (startNpcIds.Contains(targetId))
            {
                int var = qs.GetQuestVarById(0);
                switch (dialogActionId)
                {
                    case DialogAction.USE_OBJECT:
                        return SendQuestDialog(env, var + 4);
                    case DialogAction.SELECTED_QUEST_NOREWARD:
                        SendQuestEndDialog(env);
                        return true;
                }
            }
        }
        return false;
    }
}
