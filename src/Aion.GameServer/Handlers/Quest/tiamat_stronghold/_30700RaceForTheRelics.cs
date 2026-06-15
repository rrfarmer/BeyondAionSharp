using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// Not sure about second to third step "Secure the way to the Dredgion Control Center."
/// For now, we consider Chantra's death, which opens the door leading to the mentioned room, as quest progress trigger.
///
/// @author Estrayl
/// </summary>
public class _30700RaceForTheRelics : AbstractQuestHandler
{
    private const int ENTER_WORLD_ID = 300510000; // Tiamat_Stronghold
    private const int START_NPC_ID = 804868; // Rosalee
    private const int TALK_NPC_ID = 800368; // Frightening Vhasha
    private const int KAHRUN_NPC_ID = 800338; // Kahrun
    private const int SHABOKAN_NPC_ID = 219352; // Invincible Shabokan
    private const int TAHABATA_NPC_ID = 219358; // Brigade General Tahabata
    private const int CHANTRA_NPC_ID = 219353; // Brigade General Chantra

    public _30700RaceForTheRelics() : base(30700)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(START_NPC_ID).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(START_NPC_ID).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(TALK_NPC_ID).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(KAHRUN_NPC_ID).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(SHABOKAN_NPC_ID).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(CHANTRA_NPC_ID).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(TAHABATA_NPC_ID).AddOnKillEvent(questId);
        qe.RegisterOnEnterWorld(questId);
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
            if (targetId == TALK_NPC_ID && qs.GetQuestVarById(0) == 3)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 2034);
                }
                else if (dialogActionId == DialogAction.SETPRO4)
                {
                    ChangeQuestStep(env, 3, 4);
                    return CloseDialogWindow(env);
                }
            }
            else if (targetId == KAHRUN_NPC_ID && qs.GetQuestVarById(0) == 5)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 2716);
                }
                else if (dialogActionId == DialogAction.SET_SUCCEED)
                {
                    ChangeQuestStep(env, 5, 5, true);
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

    public override bool OnEnterWorldEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) == 0 && player.GetWorldId() == ENTER_WORLD_ID)
        {
            ChangeQuestStep(env, 0, 1);
            return true;
        }
        return false;
    }

    public override bool OnKillEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null)
            return false;

        int targetId = env.GetTargetId();
        if (targetId == SHABOKAN_NPC_ID && qs.GetQuestVarById(0) == 1)
        {
            ChangeQuestStep(env, 1, 2);
            return true;
        }
        else if (targetId == CHANTRA_NPC_ID && qs.GetQuestVarById(0) == 2)
        {
            ChangeQuestStep(env, 2, 3);
            return true;
        }
        else if (targetId == TAHABATA_NPC_ID && qs.GetQuestVarById(0) == 4)
        {
            ChangeQuestStep(env, 4, 5);
            return true;
        }
        return false;
    }
}
