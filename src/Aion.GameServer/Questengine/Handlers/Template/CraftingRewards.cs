using System;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services.Craft;

namespace Aion.GameServer.QuestEngine.Handlers.Template;

/// <summary>Java parity: questEngine/handlers/template/CraftingRewards (Bobobear, Pad). IllegalStateException→InvalidOperationException; DataManager/CraftSkillUpdateService/SkillList red-tolerated.</summary>
public class CraftingRewards : AbstractTemplateQuestHandler
{
    private readonly int startNpcId, endNpcId;
    private readonly int skillId;
    private readonly int levelReward;
    private readonly int questMovie;
    private readonly bool isDataDriven;

    public CraftingRewards(int questId, int startNpcId, int skillId, int levelReward, int endNpcId, int questMovie) : base(questId)
    {
        this.startNpcId = startNpcId;
        this.endNpcId = endNpcId != 0 ? endNpcId : startNpcId;
        this.skillId = skillId;
        this.levelReward = levelReward;
        this.questMovie = questMovie;
        isDataDriven = DataManager.QUEST_DATA.GetQuestById(questId).IsDataDriven();
    }

    public override void Register()
    {
        if (startNpcId != 0)
        {
            qe.RegisterQuestNpc(startNpcId).AddOnQuestStart(questId);
            qe.RegisterQuestNpc(startNpcId).AddOnTalkEvent(questId);
        }
        if (endNpcId != startNpcId)
        {
            qe.RegisterQuestNpc(endNpcId).AddOnTalkEvent(questId);
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
            if (targetId == startNpcId && CanLearn(player))
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, isDataDriven ? 4762 : 1011);
                    default:
                        return SendQuestStartDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == endNpcId && CanLearn(player))
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, isDataDriven ? 1011 : 2375);
                    case DialogAction.SELECT_QUEST_REWARD:
                        qs.SetQuestVar(0);
                        qs.SetStatus(QuestStatus.REWARD);
                        UpdateQuestStatus(env);
                        player.GetSkillList().AddSkill(player, skillId, levelReward);
                        if (questMovie != 0)
                            PlayQuestMovie(env, questMovie);
                        return SendQuestEndDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == endNpcId)
            {
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }

    private bool CanLearn(Player player)
    {
        if (levelReward == 400)
            return CraftSkillUpdateService.GetInstance().CanLearnMoreExpertCraftingSkill(player);
        if (levelReward == 500)
            return CraftSkillUpdateService.GetInstance().CanLearnMoreMasterCraftingSkill(player);
        throw new InvalidOperationException("Unhandled levelReward " + levelReward);
    }
}
