using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils.Stats;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// [Group] Confront Asmodian Officers Fight against Asmodian Officers and win (10).
/// @author vlog
/// </summary>
public class _1719ConfrontAsmodianOfficers : AbstractQuestHandler
{
    private const int _questId = 1719;

    public _1719ConfrontAsmodianOfficers() : base(_questId)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(278501).AddOnTalkEvent(_questId);
        qe.RegisterQuestNpc(278501).AddOnQuestStart(_questId);
        qe.RegisterOnKillRanked(AbyssRankEnum.STAR1_OFFICER, _questId);
    }

    public override bool OnKillRankedEvent(QuestEnv env)
    {
        return DefaultOnKillRankedEvent(env, 0, 10, true); // reward
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(_questId);

        if (env.GetTargetId() == 278501)
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
