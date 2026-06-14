using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils.Stats;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Luzien
/// </summary>
public class _1854GeneralPurge : AbstractQuestHandler
{
    public _1854GeneralPurge() : base(1854)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(278501).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(278501).AddOnTalkEvent(questId);
        qe.RegisterOnKillRanked(AbyssRankEnum.GENERAL, questId);
    }

    public override bool OnKillRankedEvent(QuestEnv env)
    {
        return DefaultOnKillRankedEvent(env, 0, 3, true); // reward
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (env.GetTargetId() == 278501)
        {
            if (qs == null || qs.IsStartable())
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
            else if (qs.GetStatus() == QuestStatus.REWARD)
            {
                if (env.GetTargetId() == 278501)
                {
                    if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
                    {
                        return SendQuestDialog(env, 1352);
                    }
                    else
                    {
                        return SendQuestEndDialog(env);
                    }
                }
            }
        }
        return false;
    }
}
