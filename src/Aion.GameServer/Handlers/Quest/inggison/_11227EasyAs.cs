using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _11227EasyAs : AbstractQuestHandler
{
    public _11227EasyAs() : base(11227)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(799076).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(799076).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(217071).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(217070).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(217069).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(217068).AddOnKillEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 799076)
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
            if (targetId == 799076)
            {
                if (dialogActionId == DialogAction.USE_OBJECT)
                {
                    return SendQuestDialog(env, 10002);
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
        return DefaultOnKillEvent(env, 217071, 0, false) || DefaultOnKillEvent(env, 217070, 1, false) || DefaultOnKillEvent(env, 217069, 2, false)
            || DefaultOnKillEvent(env, 217068, 3, true);
    }
}
