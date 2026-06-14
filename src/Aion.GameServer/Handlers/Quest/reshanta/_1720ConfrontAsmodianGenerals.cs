using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils.Stats;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// [Group] Confront Asmodian Generals Defeat Asmodian Generals and win (3). -> Asmodian Army General Report the result to Michalis (278501).
/// minlevel_permitted="40" cannot_share="true" race_permitted="ELYOS"
/// </summary>
public class _1720ConfrontAsmodianGenerals : AbstractQuestHandler
{
    private static readonly AbyssRankEnum _general = AbyssRankEnum.GENERAL;

    public _1720ConfrontAsmodianGenerals() : base(1720)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(278501).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(278501).AddOnQuestStart(questId);
        qe.RegisterOnKillRanked(_general, questId);
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
