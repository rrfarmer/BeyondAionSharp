using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

namespace Aion.GameServer.Handlers.Quest;

public class _18035ShebasSurveillance : AbstractQuestHandler
{
    private const int TALK_NPC_ID = 801281; // Demades
    private const int END_NPC_ID = 804709; // Brunte
    private const int QUEST_ITEM_ID = 182213483; // Dropped Letter

    public _18035ShebasSurveillance() : base(18035)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestItem(QUEST_ITEM_ID, questId);
        qe.RegisterQuestNpc(TALK_NPC_ID).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(END_NPC_ID).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        QuestState qs = env.GetPlayer().GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 0 && dialogActionId == DialogAction.QUEST_ACCEPT_SIMPLE)
            {
                QuestService.StartQuest(env);
                return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case TALK_NPC_ID:
                    return dialogActionId switch
                    {
                        DialogAction.QUEST_SELECT => SendQuestDialog(env, 1352),
                        DialogAction.SELECT2_1 => SendQuestDialog(env, 1353),
                        DialogAction.SETPRO1 => DefaultCloseDialog(env, 0, 1),
                        DialogAction.FINISH_DIALOG => SendQuestSelectionDialog(env),
                        _ => false,
                    };
                case END_NPC_ID:
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            return SendQuestDialog(env, 2375);
                        case DialogAction.SELECT_QUEST_REWARD:
                            RemoveQuestItem(env, QUEST_ITEM_ID, 1);
                            ChangeQuestStep(env, 1, 1, true);
                            return SendQuestDialog(env, 5);
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD && targetId == END_NPC_ID)
        {
            return SendQuestEndDialog(env);
        }
        return false;
    }

    public override HandlerResult OnItemUseEvent(QuestEnv env, Item item)
    {
        QuestState qs = env.GetPlayer().GetQuestStateList().GetQuestState(questId);
        if (qs == null || qs.IsStartable())
        {
            return HandlerResultExtensions.FromBoolean(SendQuestDialog(env, 1011));
        }
        return HandlerResult.FAILED;
    }
}
