using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Pad
/// </summary>
public class _28809FirstClassorCoach : AbstractQuestHandler
{
    public _28809FirstClassorCoach() : base(28809)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(830169).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(830169).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(830408).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(830417).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 830169)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 1011);
                    case DialogAction.QUEST_ACCEPT_1:
                    case DialogAction.QUEST_ACCEPT_SIMPLE:
                        GiveQuestItem(env, 190100013, 1);
                        return SendQuestStartDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 830408:
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            return SendQuestDialog(env, 1352);
                        case DialogAction.SETPRO1:
                            return DefaultCloseDialog(env, 0, 1);
                    }
                    return false;
                case 830417:
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            return SendQuestDialog(env, 1693);
                        case DialogAction.SETPRO2:
                            return DefaultCloseDialog(env, 1, 2);
                    }
                    return false;
                case 830169:
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            return SendQuestDialog(env, 2375);
                        case DialogAction.SELECT_QUEST_REWARD:
                            ChangeQuestStep(env, 2, 2, true);
                            return SendQuestEndDialog(env);
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 830169)
            {
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
