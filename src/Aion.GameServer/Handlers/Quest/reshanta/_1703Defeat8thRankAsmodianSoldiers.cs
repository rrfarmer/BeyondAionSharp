using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils.Stats;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Hilgert, vlog
/// </summary>
public class _1703Defeat8thRankAsmodianSoldiers : AbstractQuestHandler
{
    public _1703Defeat8thRankAsmodianSoldiers() : base(1703)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(278503).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(278503).AddOnTalkEvent(questId);
        qe.RegisterOnKillRanked(AbyssRankEnum.GRADE8_SOLDIER, questId);
    }

    public override bool OnKillRankedEvent(QuestEnv env)
    {
        return DefaultOnKillRankedEvent(env, 0, 10, true); // reward
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (env.GetTargetId() == 278503)
        {
            if (qs == null || qs.IsStartable())
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else
                    return SendQuestStartDialog(env);
            }
            else if (qs.GetStatus() == QuestStatus.REWARD)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1352);
                else
                    return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
