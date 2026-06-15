using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Ritsu
/// </summary>
public class _30550MomentOfCrisis : AbstractQuestHandler
{
    public _30550MomentOfCrisis() : base(30550)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(205864).AddOnQuestStart(questId); // Skafir
        qe.RegisterQuestNpc(205864).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(805156).AddOnTalkEvent(questId); // Ekios
        qe.RegisterQuestNpc(799592).AddOnTalkEvent(questId); // Ariana
        qe.RegisterQuestNpc(799666).AddOnTalkEvent(questId); // Ariana
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int targetId = env.GetTargetId();
        int dialogActionId = env.GetDialogActionId();
        if (qs == null || qs.IsStartable())
        {
            if (targetId == 205864)
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
            int var = qs.GetQuestVarById(0);
            switch (targetId)
            {
                case 805156:
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            return SendQuestDialog(env, 1352);
                        case DialogAction.SETPRO1:
                        {
                            return DefaultCloseDialog(env, 0, 1);
                        }
                    }
                    return false;
                case 799592:
                case 799666:
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            return SendQuestDialog(env, 2375);
                        case DialogAction.SELECT_QUEST_REWARD:
                        {
                            if (var == 1)
                            {
                                qs.SetStatus(QuestStatus.REWARD);
                                UpdateQuestStatus(env);
                                return SendQuestDialog(env, 5);
                            }
                            break;
                        }
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            switch (targetId)
            {
                case 799592:
                case 799666:
                    return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
