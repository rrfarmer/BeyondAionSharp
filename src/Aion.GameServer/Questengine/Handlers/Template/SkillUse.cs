using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Questengine.Handlers.Models;
using Aion.GameServer.Questengine.Model;

namespace Aion.GameServer.Questengine.Handlers.Template;

/// <summary>Java parity: questEngine/handlers/template/SkillUse (vlog, Bobobear, Pad). Set.addAll→UnionWith; Set.equals→SetEquals; isEmpty→Count==0; Collections.emptyList→new List; bit-packed skill-use counters preserved; QuestSkillData red-tolerated.</summary>
public class SkillUse : AbstractTemplateQuestHandler
{
    private readonly HashSet<int> startNpcIds = new();
    private readonly HashSet<int> endNpcIds = new();
    private readonly List<QuestSkillData> qsd;

    public SkillUse(int questId, List<int> startNpcIds, List<int> endNpcIds, List<QuestSkillData> qsd) : base(questId)
    {
        if (startNpcIds != null)
            this.startNpcIds.UnionWith(startNpcIds);
        if (endNpcIds != null)
            this.endNpcIds.UnionWith(endNpcIds);
        else
            this.endNpcIds.UnionWith(this.startNpcIds);
        this.qsd = qsd == null ? new List<QuestSkillData>() : qsd;
    }

    public override void Register()
    {
        foreach (int startNpcId in startNpcIds)
        {
            qe.RegisterQuestNpc(startNpcId).AddOnQuestStart(questId);
            qe.RegisterQuestNpc(startNpcId).AddOnTalkEvent(questId);
        }
        if (!endNpcIds.SetEquals(startNpcIds))
        {
            foreach (int endNpcId in endNpcIds)
                qe.RegisterQuestNpc(endNpcId).AddOnTalkEvent(questId);
        }
        foreach (QuestSkillData questSkillData in qsd)
        {
            foreach (int skillId in questSkillData.GetSkillIds())
                qe.RegisterQuestSkill(skillId, questId);
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
            if (startNpcIds.Count == 0 || startNpcIds.Contains(targetId))
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 4762);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            // TODO: check skill use count, see MonsterHunt.java how to get total count
            int var = qs.GetQuestVarById(0);
            if (endNpcIds.Contains(targetId))
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 10002);
                }
                else if (dialogActionId == DialogAction.SELECT_QUEST_REWARD)
                {
                    ChangeQuestStep(env, var, var, true); // reward
                    return SendQuestDialog(env, 5);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (endNpcIds.Contains(targetId))
            {
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }

    public override bool OnUseSkillEvent(QuestEnv env, int skillId)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            byte rewardCount = 0;
            bool success = false;
            foreach (QuestSkillData qd in qsd)
            {
                if (qd.GetSkillIds().Contains(skillId))
                {
                    int endVar = qd.GetEndVar();
                    int varId = qd.GetVarNum();
                    int total = 0;
                    do
                    {
                        int currentVar = qs.GetQuestVarById(varId);
                        total += currentVar << ((varId - qd.GetVarNum()) * 6);
                        endVar >>= 6;
                        varId++;
                    } while (endVar > 0);
                    total += 1;
                    if (total <= qd.GetEndVar())
                    {
                        for (int varsUsed = qd.GetVarNum(); varsUsed < varId; varsUsed++)
                        {
                            int value = total & 0x3F;
                            total >>= 6;
                            qs.SetQuestVarById(varsUsed, value);
                        }
                        if (qs.GetQuestVarById(qd.GetVarNum()) == qd.GetEndVar())
                            rewardCount++;
                        UpdateQuestStatus(env);
                        success = true;
                    }
                }
            }
            if (rewardCount == qsd.Count)
            {
                if (qs.GetQuestVarById(0) == 0)
                    qs.SetQuestVarById(0, 1);
                qs.SetStatus(QuestStatus.REWARD);
                UpdateQuestStatus(env);
            }
            return success;
        }
        return false;
    }
}
