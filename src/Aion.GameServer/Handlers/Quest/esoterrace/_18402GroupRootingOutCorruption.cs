using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/**
 * @author Ritsu
 */
public class _18402GroupRootingOutCorruption : AbstractQuestHandler
{
    public _18402GroupRootingOutCorruption() : base(18402)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(799590).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(799590).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(701015).AddOnKillEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = env.GetTargetId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        if (targetId == 799590)
        {
            if (qs == null || qs.IsStartable())
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 4762);
                else
                    return SendQuestStartDialog(env);
            }
            else if (qs.GetStatus() == QuestStatus.REWARD)
            {
                if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
                    return SendQuestDialog(env, 10002);
                else if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD)
                    return SendQuestDialog(env, 5);
                else
                    return SendQuestEndDialog(env);
            }
        }
        return false;
    }

    public override bool OnKillEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null || qs.GetStatus() != QuestStatus.START)
            return false;

        int targetId = env.GetTargetId();

        switch (targetId)
        {
            case 701015:
                if (qs.GetQuestVarById(1) < 5)
                {
                    qs.SetQuestVarById(1, qs.GetQuestVarById(1) + 1);
                    UpdateQuestStatus(env);
                }
                if (qs.GetQuestVarById(1) >= 5)
                {
                    qs.SetStatus(QuestStatus.REWARD);
                    UpdateQuestStatus(env);
                }
                break;
        }
        return false;
    }
}
