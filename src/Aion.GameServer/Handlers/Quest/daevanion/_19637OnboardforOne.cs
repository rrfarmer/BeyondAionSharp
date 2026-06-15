using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Pad
/// </summary>
public class _19637OnboardforOne : AbstractQuestHandler
{
    private const int npcId = 798926;
    private static readonly int[] mobIds = { 215502, 215503, 215500, 215501 };

    public _19637OnboardforOne() : base(19637)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(npcId).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(npcId).AddOnTalkEvent(questId);
        foreach (int mobId in mobIds)
        {
            qe.RegisterQuestNpc(mobId).AddOnKillEvent(questId);
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
            if (targetId == npcId)
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
            if (targetId == npcId)
            {
                if (dialogActionId == DialogAction.USE_OBJECT)
                {
                    return SendQuestDialog(env, 5);
                }
                else
                {
                    return SendQuestEndDialog(env);
                }
            }
        }
        return false;
    }

    public override bool OnKillEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            int var1 = qs.GetQuestVarById(1);
            int targetId = env.GetTargetId();
            foreach (int mobId in mobIds)
            {
                if (targetId == mobId)
                {
                    if (qs.GetQuestVarById(0) == 0)
                    {
                        if (0 <= var1 && var1 < 9)
                        {
                            ChangeQuestStep(env, var1, var1 + 1, false, 1);
                            return true;
                        }
                        else if (var1 == 9)
                        {
                            qs.SetQuestVarById(0, 1);
                            qs.SetStatus(QuestStatus.REWARD);
                            UpdateQuestStatus(env);
                            return true;
                        }
                    }
                }
            }
        }
        return false;
    }
}
