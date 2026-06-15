using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Cheatkiller
/// </summary>
public class _21468TheStruggleWithin : AbstractQuestHandler
{
    public _21468TheStruggleWithin() : base(21468)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestSkill(9832, questId);
        qe.RegisterQuestSkill(9833, questId);
        qe.RegisterQuestSkill(9834, questId);
        qe.RegisterQuestNpc(799526).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(799503).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 799526)
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
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 799503)
            {
                switch (dialogActionId)
                {
                    case DialogAction.USE_OBJECT:
                        return SendQuestDialog(env, 10002);
                    default:
                        {
                            return SendQuestEndDialog(env);
                        }
                }
            }
        }
        return false;
    }

    public override bool OnUseSkillEvent(QuestEnv env, int skillUsedId)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            int var1 = qs.GetQuestVarById(1);
            int var2 = qs.GetQuestVarById(2);
            int var3 = qs.GetQuestVarById(3);
            if (skillUsedId == 9832)
            {
                if (var1 < 10)
                {
                    qs.SetQuestVarById(1, var1 + 1);
                    UpdateQuestStatus(env);
                    Reward(qs, env);
                }
            }
            else if (skillUsedId == 9833)
            {
                if (var2 < 5)
                {
                    qs.SetQuestVarById(2, var2 + 1);
                    UpdateQuestStatus(env);
                    Reward(qs, env);
                }
            }
            else if (skillUsedId == 9834)
            {
                if (var3 < 3)
                {
                    qs.SetQuestVarById(3, var3 + 1);
                    UpdateQuestStatus(env);
                    Reward(qs, env);
                }
            }
        }
        return false;
    }

    private void Reward(QuestState qs, QuestEnv env)
    {
        if (qs.GetQuestVarById(1) == 10 && qs.GetQuestVarById(2) == 5 && qs.GetQuestVarById(3) == 3)
        {
            qs.SetStatus(QuestStatus.REWARD);
            UpdateQuestStatus(env);
        }
    }
}
