using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/**
 * @author Gigi
 */
public class _30313GroupOpeningthePrison : AbstractQuestHandler
{
    public _30313GroupOpeningthePrison() : base(30313)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(799322).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(799322).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(799225).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(730275).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int targetId = env.GetTargetId();
        int dialogActionId = env.GetDialogActionId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 799322)
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

        if (qs == null)
            return false;

        if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 730275:
                    switch (dialogActionId)
                    {
                        case DialogAction.SETPRO1:
                            {
                                RemoveQuestItem(env, 182209717, 1);
                                qs.SetStatus(QuestStatus.REWARD);
                                UpdateQuestStatus(env);
                                return true;
                            }
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 799225)
            {
                switch (dialogActionId)
                {
                    case DialogAction.USE_OBJECT:
                        return SendQuestDialog(env, 10002);
                    case DialogAction.SELECT_QUEST_REWARD:
                        return SendQuestDialog(env, 5);
                    default:
                        return SendQuestEndDialog(env);
                }
            }
        }
        return false;
    }
}
