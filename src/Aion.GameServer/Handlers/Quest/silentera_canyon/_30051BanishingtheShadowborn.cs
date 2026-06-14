using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author zhkchi
/// </summary>
public class _30051BanishingtheShadowborn : AbstractQuestHandler
{
    public _30051BanishingtheShadowborn() : base(30051)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(799381).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(799381).AddOnTalkEvent(questId);
        qe.RegisterOnKillInWorld(600010000, questId);
    }

    public override bool OnKillInWorldEvent(QuestEnv env)
    {
        if (env.GetVisibleObject() is Player killed)
        {
            if ((killed.GetLevel() + 9) >= env.GetPlayer().GetLevel() || (killed.GetLevel() - 5) <= env.GetPlayer().GetLevel())
            {
                return DefaultOnKillRankedEvent(env, 0, 7, true);
            }
        }
        return false;
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (env.GetTargetId() == 799381)
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
