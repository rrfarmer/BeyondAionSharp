using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author zhkchi
/// </summary>
public class _2106VanarsFlattery : AbstractQuestHandler
{
    public _2106VanarsFlattery() : base(2106)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203502).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(203502).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203517).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = env.GetTargetId();
        int dialogActionId = env.GetDialogActionId();
        QuestState qs = player.GetQuestStateList().GetQuestState(GetQuestId());

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 203502)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 4762);
                    case DialogAction.ASK_QUEST_ACCEPT:
                        return SendQuestDialog(env, 4);
                    case DialogAction.QUEST_ACCEPT_1:
                        if (GiveQuestItem(env, 182203106, 1))
                        {
                            return SendQuestStartDialog(env);
                        }
                        return false;
                    case DialogAction.QUEST_REFUSE_1:
                        return SendQuestDialog(env, 1004);
                    case DialogAction.FINISH_DIALOG:
                        return CloseDialogWindow(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 203502)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 1003);
                    case DialogAction.USE_OBJECT:
                        return SendQuestDialog(env, 1011);
                    case DialogAction.SETPRO1:
                        ChangeQuestStep(env, 0, 0, true);
                        return CloseDialogWindow(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203517)
            {
                if (dialogActionId == DialogAction.USE_OBJECT)
                    return SendQuestDialog(env, 10002);
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
