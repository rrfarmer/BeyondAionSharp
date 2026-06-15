using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Ritsu, Majka
/// </summary>
public class _24022SneakBehindtheIceClaw : AbstractQuestHandler
{
    public _24022SneakBehindtheIceClaw() : base(24022)
    {
    }

    public override void Register()
    {
        int[] npcs = { 204329, 204332, 204335, 700246, 204301, 802047 };
        qe.RegisterOnQuestCompleted(questId);
        qe.RegisterOnLevelChanged(questId);
        qe.RegisterQuestNpc(204417).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(212877).AddOnKillEvent(questId);
        qe.RegisterQuestItem(182215364, questId);
        foreach (int npc in npcs)
        {
            qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
        }
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

        if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 204329: // Tofa
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
                            return false;
                        case DialogAction.SETPRO1:
                            return DefaultCloseDialog(env, 0, 1); // 1
                        case DialogAction.SET_SUCCEED:
                            return DefaultCloseDialog(env, 7, 7, true, false); // reward
                    }
                    break;
                case 204335: // Aprily
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
                case 802047: // Landver
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 5)
                            {
                                return SendQuestDialog(env, 2716);
                            }
                            return false;
                        case DialogAction.SETPRO6:
                            player.GetTitleList().AddTitle(58, true, 0);
                            return DefaultCloseDialog(env, 5, 6); // 6
                    }
                    break;
                case 204332: // Jorund
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 2)
                            {
                                return SendQuestDialog(env, 1693);
                            }
                            else if (var == 3)
                            {
                                if (player.GetInventory().GetItemCountByItemId(182215364) == 0)
                                {
                                    return SendQuestDialog(env, 2376);
                                }
                                else
                                    return SendQuestDialog(env, 2375);
                            }
                            return false;
                        case DialogAction.SETPRO3:
                            if (var == 2)
                            {
                                return DefaultCloseDialog(env, 2, 3, 182215364, 1, 0, 0); // 3
                            }
                            break;
                    }
                    break;
                case 700246: // Dead Fire
                    if (dialogActionId == DialogAction.USE_OBJECT)
                    {
                        if (var == 3)
                        {
                            if (player.GetInventory().GetItemCountByItemId(182215365) > 0)
                            {
                                SpawnForFiveMinutes(204417, env.GetVisibleObject().GetPosition());
                                RemoveQuestItem(env, 182215364, 1);
                                RemoveQuestItem(env, 182215365, 1);
                                ChangeQuestStep(env, 3, 4); // 4
                            }
                        }
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 204301)
            { // Aegir
                if (dialogActionId == DialogAction.USE_OBJECT)
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

    public override HandlerResult OnItemUseEvent(QuestEnv env, Item item)
    {
        Player player = env.GetPlayer();
        if (player.IsInsideZone(ZoneName.Get("ALTAR_OF_TRIAL_220020000")))
        {
            return HandlerResultExtensions.FromBoolean(UseQuestItem(env, item, 6, 6, false));
        }
        return HandlerResult.FAILED;
    }

    public override bool OnKillEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            int targetId = env.GetTargetId();
            switch (targetId)
            {
                case 204417:
                    return DefaultOnKillEvent(env, 204417, 4, 5); // 5
                case 212877:
                    return DefaultOnKillEvent(env, 212877, 6, 7); // 7
            }
        }
        return false;
    }

    public override void OnQuestCompletedEvent(QuestEnv env)
    {
        DefaultOnQuestCompletedEvent(env, 24020);
    }

    public override void OnLevelChangedEvent(Player player)
    {
        DefaultOnLevelChangedEvent(player, 24020);
    }
}
