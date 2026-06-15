using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Tibald
/// </summary>
public class _28910AStabbingInSauro : AbstractQuestHandler
{
    public _28910AStabbingInSauro() : base(28910)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(801946).AddOnQuestStart(questId); // Sibeldum.
        qe.RegisterQuestNpc(801947).AddOnTalkEvent(questId); // Giriltia.
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();
        if (qs == null || qs.IsStartable())
        {
            if (targetId == 801946) // Sibeldum.
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 1011);
                    case DialogAction.QUEST_ACCEPT_1:
                    case DialogAction.QUEST_ACCEPT_SIMPLE:
                        return SendQuestStartDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 801947: // Giriltia.
                {
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            return SendQuestDialog(env, 2375);
                        case DialogAction.SELECT_QUEST_REWARD:
                            ChangeQuestStep(env, 0, 0, true);
                            return SendQuestEndDialog(env);
                    }
                    break;
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 801947) // Giriltia.
            {
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
