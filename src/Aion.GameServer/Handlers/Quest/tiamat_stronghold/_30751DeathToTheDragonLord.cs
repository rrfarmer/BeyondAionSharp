using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Estrayl
/// </summary>
public class _30751DeathToTheDragonLord : AbstractQuestHandler
{
    private const int START_NPC_ID = 804869; // Ginnie
    private const int KAHRUN_NPC_ID = 800430; // Kahrun
    private const int MARCHUTAN_NPC_ID = 800356; // Marchutan
    private const int TIAMAT_NPC_ID = 219362; // Tiamat

    public _30751DeathToTheDragonLord() : base(30751)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(START_NPC_ID).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(START_NPC_ID).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(KAHRUN_NPC_ID).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(MARCHUTAN_NPC_ID).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(TIAMAT_NPC_ID).AddOnKillEvent(questId);
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
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == KAHRUN_NPC_ID && qs.GetQuestVarById(0) == 1)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 1352);
                }
                else if (dialogActionId == DialogAction.SETPRO2)
                {
                    ChangeQuestStep(env, 1, 2);
                    return CloseDialogWindow(env);
                }
            }
            else if (targetId == MARCHUTAN_NPC_ID && qs.GetQuestVarById(0) == 2)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 1693);
                }
                else if (dialogActionId == DialogAction.SET_SUCCEED)
                {
                    ChangeQuestStep(env, 2, 2, true);
                    return CloseDialogWindow(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == START_NPC_ID)
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

        if (env.GetTargetId() == TIAMAT_NPC_ID && qs.GetQuestVarById(0) == 0)
        {
            ChangeQuestStep(env, 0, 1);
            return true;
        }
        return false;
    }
}
