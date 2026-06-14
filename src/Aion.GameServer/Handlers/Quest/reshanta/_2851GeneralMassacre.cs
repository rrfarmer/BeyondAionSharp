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
public class _2851GeneralMassacre : AbstractQuestHandler
{
    public _2851GeneralMassacre() : base(2851)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(278001).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(278001).AddOnTalkEvent(questId);
        qe.RegisterOnKillRanked(AbyssRankEnum.GENERAL, questId);
    }

    public override bool OnKillRankedEvent(QuestEnv env)
    {
        return DefaultOnKillRankedEvent(env, 0, 3, true);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (env.GetTargetId() == 278001)
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
                if (env.GetTargetId() == 278001)
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
