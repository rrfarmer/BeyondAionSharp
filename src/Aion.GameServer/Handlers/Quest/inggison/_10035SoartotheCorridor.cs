using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @Author Majka
/// </summary>
public class _10035SoartotheCorridor : AbstractQuestHandler
{
    private static readonly int[] mobs = { 220021, 220022, 216775 };

    public _10035SoartotheCorridor() : base(10035)
    {
    }

    public override void Register()
    {
        // Yulia ID: 798928
        // Laokon ID: 799025
        // Veille ID: 798958
        // Laetia ID: 798996
        // Corridor Entry Controller ID: 702663
        // Outremus ID: 798926
        int[] npcs = { 798928, 799025, 798958, 798996, 206363, 702663, 798926 };
        qe.RegisterOnLevelChanged(questId);
        qe.RegisterOnQuestCompleted(questId);
        qe.RegisterOnEnterZone(ZoneName.Get("ANGRIEF_GATE_210050000"), questId);
        foreach (int mob in mobs)
            qe.RegisterQuestNpc(mob).AddOnKillEvent(questId);
        foreach (int npc in npcs)
            qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        if (qs == null)
        {
            return false;
        }
        int var = qs.GetQuestVarById(0);
        int targetId = env.GetTargetId();

        if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 798928: // Yulia
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 0)
                            {
                                return SendQuestDialog(env, 1011);
                            }
                            else if (var == 7)
                            {
                                return SendQuestDialog(env, 3398);
                            }
                            break;
                        case DialogAction.SETPRO1:
                            return DefaultCloseDialog(env, 0, 1);
                        case DialogAction.SET_SUCCEED:
                            qs.SetQuestVar(8);
                            qs.SetStatus(QuestStatus.REWARD);
                            UpdateQuestStatus(env);
                            return CloseDialogWindow(env);
                    }
                    break;
                case 799025: // Laokon
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 1)
                            {
                                return SendQuestDialog(env, 1352);
                            }
                            break;
                        case DialogAction.SETPRO2:
                            return DefaultCloseDialog(env, 1, 2);
                    }
                    break;
                case 798958: // Veille
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 2)
                            {
                                return SendQuestDialog(env, 1693);
                            }
                            break;
                        case DialogAction.SETPRO3:
                            return DefaultCloseDialog(env, 2, 3);
                    }
                    break;
                case 798996: // Laetia
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 3)
                            {
                                return SendQuestDialog(env, 2034);
                            }
                            break;
                        case DialogAction.SELECT4_1:
                            PlayQuestMovie(env, 517);
                            return SendQuestDialog(env, 2035);
                        case DialogAction.SETPRO4:
                            GiveQuestItem(env, 182215629, 1);
                            return DefaultCloseDialog(env, 3, 4);
                    }
                    break;
                case 702663: // Corridor Entry Controller
                    switch (dialogActionId)
                    {
                        case DialogAction.USE_OBJECT:
                            if (var == 6)
                            {
                                RemoveQuestItem(env, 182215629, 1);
                                // spawnForFiveMinutes(700641, player, 1377, 2297, 296, (byte) 90); ToDo: to check on retail what's
                                // its function
                                qs.SetQuestVar(7);
                                UpdateQuestStatus(env);
                                return true;
                            }
                            break;
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 798926) // Outremus
            {
                if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
                {
                    return SendQuestDialog(env, 10002);
                }
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }

    public override bool OnKillEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            if (var == 5)
            {
                int var1 = qs.GetQuestVarById(1);
                if (var1 >= 0 && var1 < 9)
                {
                    return DefaultOnKillEvent(env, mobs, var1, var1 + 1, 1);
                }
                else if (var1 == 9)
                {
                    qs.SetQuestVar(6);
                    UpdateQuestStatus(env);
                    return true;
                }
            }
        }
        return false;
    }

    public override bool OnEnterZoneEvent(QuestEnv env, ZoneName zoneName)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            if (zoneName.Equals(ZoneName.Get("ANGRIEF_GATE_210050000")))
            {
                if (var == 4)
                {
                    ChangeQuestStep(env, 4, 5);
                    return true;
                }
            }
        }
        return false;
    }

    public override void OnQuestCompletedEvent(QuestEnv env)
    {
        DefaultOnQuestCompletedEvent(env, 10031, 10032, 10033, 10034);
    }

    public override void OnLevelChangedEvent(Player player)
    {
        DefaultOnLevelChangedEvent(player, 10031, 10032, 10033, 10034);
    }
}
