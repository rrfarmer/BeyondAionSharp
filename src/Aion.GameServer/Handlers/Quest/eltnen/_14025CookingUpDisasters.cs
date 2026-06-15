using Aion.GameServer.Model;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Teleport;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Artur, Majka, Sykra
/// </summary>
public class _14025CookingUpDisasters : AbstractQuestHandler
{
    private static readonly int[] mobs = { 211017, 217090, 232133 };

    public _14025CookingUpDisasters() : base(14025)
    {
    }

    public override void Register()
    {
        int[] npcs = { 203989, 203901, 204020 };
        qe.RegisterOnQuestCompleted(questId);
        qe.RegisterOnLevelChanged(questId);
        qe.RegisterOnDie(questId);
        foreach (int npc in npcs)
            qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
        foreach (int mob in mobs)
            qe.RegisterQuestNpc(mob).AddOnKillEvent(questId);
    }

    public override void OnQuestCompletedEvent(QuestEnv env)
    {
        DefaultOnQuestCompletedEvent(env, 14020);
    }

    public override void OnLevelChangedEvent(Player player)
    {
        DefaultOnLevelChangedEvent(player, 14020);
    }

    public override bool OnKillEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null || qs.GetStatus() != QuestStatus.START)
            return false;

        int var = qs.GetQuestVarById(0);
        int targetId = env.GetTargetId();

        switch (targetId)
        {
            case 211017:
            case 232133:
                int killVar1 = qs.GetQuestVarById(1);
                if (var == 5 && killVar1 < 4)
                {
                    qs.SetQuestVarById(1, killVar1 + 1);
                    UpdateQuestStatus(env);
                    return true;
                }
                break;
            case 217090:
                int killVar2 = qs.GetQuestVarById(2);
                if (var == 5 && killVar2 < 1)
                {
                    qs.SetQuestVarById(2, killVar2 + 1);
                    UpdateQuestStatus(env);
                    return true;
                }
                break;
        }
        return false;
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

        if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203901) // Telemachus
                if (dialogActionId == DialogAction.USE_OBJECT)
                {
                    return SendQuestDialog(env, 3057);
                }
            return SendQuestEndDialog(env);
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 203989) // Tumblusen
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 0)
                            return SendQuestDialog(env, 1011);
                        else if (var == 1)
                            return SendQuestDialog(env, 1352);
                        else if (var == 4)
                            return SendQuestDialog(env, 2034);
                        else if (var == 5)
                            return SendQuestDialog(env, 2716);
                        break;
                    case DialogAction.SELECT1_1_1:
                        if (var == 0)
                            PlayQuestMovie(env, 183);
                        break;
                    case DialogAction.SETPRO1:
                        if (var == 0)
                            return DefaultCloseDialog(env, var, var + 1); // 1
                        else
                            return SendQuestDialog(env, 1352);
                    case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                        if (var == 1)
                        {
                            if (QuestService.CollectItemCheck(env, true))
                            {
                                ChangeQuestStep(env, var, var + 1, false); // 2
                                return SendQuestDialog(env, 1438);
                            }
                            else
                                return SendQuestDialog(env, 1353);
                        }
                        return false;
                    case DialogAction.SETPRO2:
                        if (var == 2)
                            return DefaultCloseDialog(env, var, var + 1); // 3
                        return false;
                    case DialogAction.SETPRO4:
                        if (var == 4)
                            return DefaultCloseDialog(env, var, var + 1); // 5
                        break;
                    case DialogAction.SETPRO6:
                        if (var == 5)
                        {
                            ChangeQuestStep(env, 5, 6, true);
                            return CloseDialogWindow(env);
                        }
                        break;
                }
            }
            else if (targetId == 204020) // Mabangtah
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 3)
                            return SendQuestDialog(env, 1693);
                        if (var == 6)
                            return SendQuestDialog(env, 2034);
                        else if (var == 10)
                            return SendQuestDialog(env, 3057);
                        break;
                    case DialogAction.SETPRO3:
                        if (var == 3)
                        {
                            TeleportService.TeleportTo(player, 210020000, 1761.0742f, 907.1806f, 427.8147f, (byte)47, TeleportAnimation.FADE_OUT_BEAM);
                            qs.SetQuestVar(4);
                            UpdateQuestStatus(env);
                            return true;
                        }
                        break;
                    case DialogAction.SETPRO7:
                        return DefaultCloseDialog(env, 10, 10, true, false); // reward
                }
            }
        }
        return false;
    }

    public override bool OnDieEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null)
            return false;

        int var = qs.GetQuestVarById(0);

        if (qs.GetStatus() == QuestStatus.START)
        {
            if (var >= 7 && var <= 10)
            {
                qs.SetQuestVarById(0, 6); // 6
                UpdateQuestStatus(env);
                return true;
            }
        }
        return false;
    }
}
