using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author keqi
/// </summary>
public class _21054MissionofDestiny : AbstractQuestHandler
{
    public _21054MissionofDestiny() : base(21054)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(799318).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(799318).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 799318)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 1011);
                    case DialogAction.SELECT1_1:
                        return SendQuestDialog(env, 1012);
                    case DialogAction.ASK_QUEST_ACCEPT:
                        return SendQuestDialog(env, 4);
                    case DialogAction.QUEST_ACCEPT_1:
                        return SendQuestStartDialog(env);
                    case DialogAction.QUEST_REFUSE_1:
                        return SendQuestDialog(env, 1004);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 799318)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 2375);
                    default:
                        return SendQuestStartDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 799318)
            {
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
