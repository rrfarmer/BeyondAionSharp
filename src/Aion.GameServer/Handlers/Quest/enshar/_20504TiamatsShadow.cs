using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @Author Majka
/// </summary>
public class _20504TiamatsShadow : AbstractQuestHandler
{
    public _20504TiamatsShadow() : base(20504)
    {
    }

    public override void Register()
    {
        // Xeikun 804730
        // Jalturan 804731
        // Gilgamesh 804742
        int[] npcs = { 804730, 804731, 804742 };
        foreach (int npc in npcs)
            qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);

        int[] mobs = { 219943, 219944, 219945, 219946, 219947, 219948, 219949 };
        foreach (int mob in mobs)
            qe.RegisterQuestNpc(mob).AddOnKillEvent(questId);
        qe.RegisterOnQuestCompleted(questId);
        qe.RegisterOnLevelChanged(questId);
        qe.RegisterOnInvisibleTimerEnd(questId);
        qe.RegisterOnLogOut(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null)
            return false;

        int var = qs.GetQuestVarById(0);
        int targetId = env.GetTargetId();
        int dialogActionId = env.GetDialogActionId();

        switch (targetId)
        {
            case 804730: // Xeikun
                if (qs.GetStatus() == QuestStatus.START)
                {
                    if (var == 0) // Step 0: Talk with Xeikun.
                    {
                        if (dialogActionId == DialogAction.QUEST_SELECT)
                            return SendQuestDialog(env, 1011);

                        if (dialogActionId == DialogAction.SETPRO1)
                            return DefaultCloseDialog(env, var, var + 1);
                    }

                    if (var == 2) // Step 2: Talk with Xeikun.
                    {
                        if (dialogActionId == DialogAction.QUEST_SELECT)
                            return SendQuestDialog(env, 1693);

                        if (dialogActionId == DialogAction.SETPRO3)
                            return DefaultCloseDialog(env, var, var + 1);
                    }
                }

                if (qs.GetStatus() == QuestStatus.REWARD)
                {
                    if (dialogActionId == DialogAction.USE_OBJECT)
                    {
                        return SendQuestDialog(env, 10002);
                    }

                    return SendQuestEndDialog(env);
                }
                break;
            case 804742: // Gilgamesh
                if (qs.GetStatus() == QuestStatus.START)
                {
                    if (var == 5) // Step 5: Talk with Gilgamesh.
                    {
                        if (dialogActionId == DialogAction.QUEST_SELECT)
                            return SendQuestDialog(env, 2716);

                        if (dialogActionId == DialogAction.SETPRO6)
                            return DefaultCloseDialog(env, var, var + 1);
                    }
                }
                break;
            case 804731: // Jalturan
                if (qs.GetStatus() == QuestStatus.START)
                {
                    if (var == 6) // Step 6: Talk with Jalturan somewhere near Cet Siege Territory.
                    {
                        if (dialogActionId == DialogAction.QUEST_SELECT)
                            return SendQuestDialog(env, 3057);

                        if (dialogActionId == DialogAction.SET_SUCCEED)
                        {
                            qs.SetStatus(QuestStatus.REWARD);
                            qs.SetQuestVar(var + 1);
                            UpdateQuestStatus(env);
                            return CloseDialogWindow(env);
                        }
                    }
                }
                break;
        }
        return false;
    }

    public override bool OnKillEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null)
            return false;

        int var = qs.GetQuestVarById(0);
        int targetId = env.GetTargetId();

        switch (targetId)
        {
            case 219943:
            case 219944:
            case 219945:
                if (var == 1) // Step 1: Investigate the Beritra troops (5) in The Beritra Defense Line.
                {
                    int varKill = qs.GetQuestVarById(1); // Kill counter
                    qs.SetQuestVarById(1, varKill + 1); // 1-5 with varNum 1
                    if (varKill >= 4)
                    {
                        qs.SetQuestVar(var + 1);
                    }
                    UpdateQuestStatus(env);
                    return true;
                }
                break;
            case 219946:
            case 219947:
            case 219948:
                if (var == 3) // Step 3: Investigate Vengeful Aetheric Guard Dominators (5) in the Wretched Plot.
                {
                    int varKill = qs.GetQuestVarById(1); // Kill counter
                    qs.SetQuestVarById(1, varKill + 1); // 1-5 with varNum 1
                    if (varKill >= 4)
                    {
                        qs.SetQuestVar(var + 1);
                    }
                    UpdateQuestStatus(env);
                    return true;
                }
                break;
            case 219949:
                if (var == 4) // Step 4: Take down Cursed Gilgamesh. (1)
                {
                    qs.SetQuestVar(var + 1);
                    UpdateQuestStatus(env);
                    SpawnForFiveMinutes(804742, env.GetVisibleObject().GetPosition());
                    QuestService.InvisibleTimerStart(env, 300);
                    return true;
                }
                break;
        }
        return false;
    }

    public override bool OnLogOutEvent(QuestEnv env)
    {
        return RestoreQuestStep(env, 5, 4);
    }

    public override bool OnInvisibleTimerEndEvent(QuestEnv env)
    {
        return RestoreQuestStep(env, 5, 4);
    }

    private bool RestoreQuestStep(QuestEnv env, int step, int oldStep)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        if (qs == null)
            return false;

        int var = qs.GetQuestVarById(0);
        if (var == step)
        {
            qs.SetQuestVar(oldStep);
            UpdateQuestStatus(env);
        }
        return true;
    }

    public override void OnQuestCompletedEvent(QuestEnv env)
    {
        DefaultOnQuestCompletedEvent(env, 20500);
    }

    public override void OnLevelChangedEvent(Player player)
    {
        DefaultOnLevelChangedEvent(player, 20500);
    }
}
