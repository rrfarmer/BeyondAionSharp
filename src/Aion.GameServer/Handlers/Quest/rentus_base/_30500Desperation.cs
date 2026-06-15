using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author zhkchi
/// </summary>
public class _30500Desperation : AbstractQuestHandler
{
    public _30500Desperation() : base(30500)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(205842).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(205842).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(804879).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(799670).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 205842)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 1011);
                    default:
                        return SendQuestStartDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 804879)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 1352);
                    case DialogAction.SETPRO1:
                        qs.SetQuestVarById(0, 1);
                        qs.SetStatus(QuestStatus.REWARD);
                        UpdateQuestStatus(env);
                        return CloseDialogWindow(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 799670)
            {
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
