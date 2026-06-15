using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @Author Majka
/// </summary>
public class _20035SilenteraSupport : AbstractQuestHandler
{
    private static readonly int[] mobs = { 216101, 216104, 216107, 216108, 216109, 216112, 216448, 216449, 216450, 216451 };

    public _20035SilenteraSupport() : base(20035)
    {
    }

    public override void Register()
    {
        // Richelle ID: 799225
        // Valetta ID: 799226
        // Jecasti ID: 799283
        // Arango ID: 799309
        // Mastarius ID: 799323
        // Notud ID: 799329
        int[] npcs = { 799225, 799226, 799283, 799309, 799323, 799329 };
        qe.RegisterOnLevelChanged(questId);
        qe.RegisterOnQuestCompleted(questId);
        qe.RegisterQuestItem(182215659, questId);
        qe.RegisterQuestItem(182215660, questId);
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
                case 799226: // Valetta
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
                            return DefaultCloseDialog(env, 0, 1); // 1
                        case DialogAction.SET_SUCCEED:
                            RemoveQuestItem(env, 182215660, 1);
                            qs.SetStatus(QuestStatus.REWARD);
                            return DefaultCloseDialog(env, 7, 8); // reward
                    }
                    break;
                case 799329: // Notud
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 1)
                            {
                                return SendQuestDialog(env, 1352);
                            }
                            return false;
                        case DialogAction.SETPRO2:
                            return DefaultCloseDialog(env, 1, 2); // 2
                    }
                    break;
                case 799323: // Mastarius
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 2)
                            {
                                return SendQuestDialog(env, 1693);
                            }
                            return false;
                        case DialogAction.SETPRO3:
                            GiveQuestItem(env, 182215596, 1);
                            GiveQuestItem(env, 182215597, 1);
                            return DefaultCloseDialog(env, 2, 3); // 3
                    }
                    break;
                case 799283: // Jecasti
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 3)
                            {
                                return SendQuestDialog(env, 2034);
                            }
                            return false;
                        case DialogAction.SETPRO4:
                            RemoveQuestItem(env, 182215596, 1);
                            return DefaultCloseDialog(env, 3, 4); // 4
                    }
                    break;
                case 799309: // Arango
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 4)
                            {
                                return SendQuestDialog(env, 2375);
                            }
                            return false;
                        case DialogAction.SELECT5_1:
                            PlayQuestMovie(env, 567);
                            return SendQuestDialog(env, 2376);
                        case DialogAction.SETPRO5:
                            RemoveQuestItem(env, 182215597, 1);
                            GiveQuestItem(env, 182215659, 1);
                            return DefaultCloseDialog(env, 4, 5); // 5
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 799225) // Richelle
            {
                if (dialogActionId == DialogAction.USE_OBJECT)
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
                    qs.SetQuestVar(6); // 6
                    UpdateQuestStatus(env);
                    return true;
                }
            }
        }
        return false;
    }

    public override HandlerResult OnItemUseEvent(QuestEnv env, Item item)
    {
        Player player = env.GetPlayer();
        if (player.IsInsideZone(ZoneName.Get("DF4_ITEMUSEAREA_Q20035_220070000")))
        {
            return HandlerResultExtensions.FromBoolean(UseQuestItem(env, item, 6, 7, false, 182215660, 1)); // 7
        }
        return HandlerResult.FAILED;
    }

    public override void OnQuestCompletedEvent(QuestEnv env)
    {
        DefaultOnQuestCompletedEvent(env, 20031, 20032, 20033, 20034);
    }

    public override void OnLevelChangedEvent(Player player)
    {
        DefaultOnLevelChangedEvent(player, 20031, 20032, 20033, 20034);
    }
}
