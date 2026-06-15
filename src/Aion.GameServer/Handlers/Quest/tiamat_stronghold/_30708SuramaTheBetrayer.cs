using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/**
 * 1. Step: Talk to Surama.
 * 2. Step: Rescue 5 Captured Drakan Scientists.
 * 3. Step: Kill Brigade General Laksyaka
 * 4. Step: Talk to Murugan The Loyal.
 *
 * Second step is currently handled by ai.instance.tiamatStrongHold.CapturedDrakanScientistAI.
 *
 * @author Estrayl
 */
public class _30708SuramaTheBetrayer : AbstractQuestHandler
{
    private const int START_NPC_ID = 800369; // Surama
    private const int END_NPC_ID = 800438; // Murugan the Loyal
    private const int TARGET_TO_KILL = 219356; // Brigade General Laksyaka

    public _30708SuramaTheBetrayer() : base(30708)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(TARGET_TO_KILL).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(START_NPC_ID).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(END_NPC_ID).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        QuestState qs = env.GetPlayer().GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == START_NPC_ID)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 4762);
                }
                else
                {
                    return SendQuestStartDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == END_NPC_ID)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 10002);
                }
                else
                {
                    return SendQuestEndDialog(env);
                }
            }
        }
        return false;
    }

    public override bool OnKillEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null)
            return false;

        if (env.GetTargetId() == TARGET_TO_KILL && qs.GetQuestVarById(0) == 5)
        {
            qs.SetStatus(QuestStatus.REWARD);
            UpdateQuestStatus(env);
            return true;
        }
        return false;
    }
}
