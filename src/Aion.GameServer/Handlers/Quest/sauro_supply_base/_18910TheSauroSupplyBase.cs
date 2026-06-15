using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Tibald
/// </summary>
public class _18910TheSauroSupplyBase : AbstractQuestHandler
{
    public _18910TheSauroSupplyBase() : base(18910)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(801944).AddOnQuestStart(questId); // Amalde.
        qe.RegisterQuestNpc(801945).AddOnTalkEvent(questId); // Kanix.
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();
        if (qs == null || qs.IsStartable())
        {
            if (targetId == 801944) // Amalde.
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
                case 801945: // Kanix.
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
            if (targetId == 801945) // Kanix.
            {
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
