using Aion.GameServer.Model;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _18036BastionsAreEternal : AbstractQuestHandler
{
    private const int START_NPC_ID = 801281; // Demades
    private const int TALK_NPC_ID = 802008; // Kvash

    public _18036BastionsAreEternal() : base(18036)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(START_NPC_ID).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(START_NPC_ID).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(TALK_NPC_ID).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        int targetId = env.GetTargetId();
        QuestState qs = env.GetPlayer().GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == START_NPC_ID)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 4762);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == TALK_NPC_ID)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);

                if (dialogActionId == DialogAction.SETPRO1)
                {
                    ChangeQuestStep(env, 0, 0, true);
                    return CloseDialogWindow(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == START_NPC_ID)
                return SendQuestEndDialog(env);
        }
        return false;
    }
}
