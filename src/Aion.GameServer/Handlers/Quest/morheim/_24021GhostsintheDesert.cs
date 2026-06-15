using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Ritsu
/// </summary>
public class _24021GhostsintheDesert : AbstractQuestHandler
{
    private int itemId = 182215363;

    public _24021GhostsintheDesert() : base(24021)
    {
    }

    public override void Register()
    {
        qe.RegisterOnQuestCompleted(questId);
        qe.RegisterOnLevelChanged(questId);
        qe.RegisterQuestItem(itemId, questId);
        qe.RegisterQuestNpc(204302).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(204329).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(802046).AddOnTalkEvent(questId);
    }

    public override void OnQuestCompletedEvent(QuestEnv env)
    {
        DefaultOnQuestCompletedEvent(env, 24020);
    }

    public override void OnLevelChangedEvent(Player player)
    {
        DefaultOnLevelChangedEvent(player, 24020);
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
                case 204302: // Bragi
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            return SendQuestDialog(env, 1011);
                        case DialogAction.SETPRO1:
                            return DefaultCloseDialog(env, 0, 1); // 1
                    }
                    break;
                case 204329: // Tofa
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            switch (var)
                            {
                                case 1:
                                    {
                                        return SendQuestDialog(env, 1352);
                                    }
                                case 2:
                                    {
                                        return SendQuestDialog(env, 1693);
                                    }
                                case 3:
                                    {
                                        return SendQuestDialog(env, 10000);
                                    }
                            }
                            return false;
                        case DialogAction.SELECT2_1:
                            if (var == 1)
                            {
                                PlayQuestMovie(env, 73);
                                return SendQuestDialog(env, 1353);
                            }
                            return false;
                        case DialogAction.SETPRO2:
                            return DefaultCloseDialog(env, 1, 2); // 2
                    }
                    return false;
                case 802046: // Tofynir
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 2)
                            {
                                return SendQuestDialog(env, 1693);
                            }
                            if (var == 3)
                            {
                                return SendQuestDialog(env, 2034);
                            }
                            return false;
                        case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                            return CheckQuestItems(env, 2, 3, false, 10000, 10001); // 3
                        case DialogAction.SETPRO4:
                            if (!player.GetInventory().IsFullSpecialCube())
                            {
                                return DefaultCloseDialog(env, 3, 4, 182215363, 1, 0, 0); // 4
                            }
                            else
                            {
                                return SendQuestSelectionDialog(env);
                            }
                        case DialogAction.FINISH_DIALOG:
                            return SendQuestSelectionDialog(env);
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 204329)
            { // Tofa
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
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            if (item.GetItemTemplate().GetTemplateId() == 182215363 && player.IsInsideItemUseZone(ZoneName.Get("DF2_ITEMUSEAREA_Q2032")))
            {
                return HandlerResultExtensions.FromBoolean(UseQuestItem(env, item, 4, 4, true, 88)); // reward
            }
        }
        return HandlerResult.FAILED;
    }
}
