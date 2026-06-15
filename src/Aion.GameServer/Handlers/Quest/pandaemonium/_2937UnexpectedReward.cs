using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Gigi, vlog
/// </summary>
public class _2937UnexpectedReward : AbstractQuestHandler
{
    public _2937UnexpectedReward() : base(2937)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204092).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(204092).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798059).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 204092) // Talon
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 1011);
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
            if (targetId == 798059) // Nekorunuerk
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 0)
                        {
                            return SendQuestDialog(env, 2375);
                        }
                        return false;
                    case DialogAction.SELECT_QUEST_REWARD:
                        qs.SetQuestVar(1);
                        qs.SetStatus(QuestStatus.REWARD);
                        UpdateQuestStatus(env);
                        return SendQuestDialog(env, 5);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 798059) // Nekorunuerk
            {
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
