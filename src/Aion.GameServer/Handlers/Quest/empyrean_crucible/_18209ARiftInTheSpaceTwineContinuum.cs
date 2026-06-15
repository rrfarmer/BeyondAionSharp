using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/**
 * @author Kamui
 */
public class _18209ARiftInTheSpaceTwineContinuum : AbstractQuestHandler
{
    public _18209ARiftInTheSpaceTwineContinuum() : base(18209)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(205309).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(205309).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(217819).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(218185).AddOnKillEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 205309)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 4762);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 205309)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.SELECT_QUEST_REWARD:
                        return SendQuestDialog(env, 5);
                    default:
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
            int var = qs.GetQuestVarById(0);
            int var1 = qs.GetQuestVarById(1);

            if (var == 0 && var1 < 4)
                return DefaultOnKillEvent(env, 217819, 0, 4, 1);
            else if (var == 0 && var1 == 4)
                return DefaultOnKillEvent(env, 217819, 0, 1, 0);
            else if (var == 1 && env.GetTargetId() == 218185)
            {
                qs.SetQuestVarById(2, 1);
                qs.SetStatus(QuestStatus.REWARD);
                UpdateQuestStatus(env);
                return true;
            }
        }
        return false;
    }
}
