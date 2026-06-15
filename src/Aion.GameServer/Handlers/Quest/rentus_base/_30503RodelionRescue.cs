using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author maddison
/// </summary>
public class _30503RodelionRescue : AbstractQuestHandler
{
    public _30503RodelionRescue() : base(30503)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(205438).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(205438).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(799541).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(701097).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 205438)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 4762);
                    default:
                        return SendQuestStartDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 799541)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 1011);
                    case DialogAction.SET_SUCCEED:
                        ChangeQuestStep(env, 0, 0, true);
                        return CloseDialogWindow(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 205438)
            {
                if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
                    return SendQuestDialog(env, 10002);
                else
                    return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
