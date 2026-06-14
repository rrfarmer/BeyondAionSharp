using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author vlog
/// </summary>
public class _1169LightningfootTuka : AbstractQuestHandler
{
    public _1169LightningfootTuka() : base(1169)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203126).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(203126).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(210317).AddOnKillEvent(questId);
    }

    public override bool OnKillEvent(QuestEnv env)
    {
        return DefaultOnKillEvent(env, 210317, 0, true);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        if (qs == null || qs.IsStartable())
        {
            if (env.GetTargetId() == 203126)
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
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (env.GetTargetId() == 203126)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 1352);
                }
                else
                {
                    return SendQuestEndDialog(env);
                }
            }
        }
        return false;
    }
}
