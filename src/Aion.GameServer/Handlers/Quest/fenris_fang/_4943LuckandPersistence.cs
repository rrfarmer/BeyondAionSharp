using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Nanou, Gigi
/// </summary>
public class _4943LuckandPersistence : AbstractQuestHandler
{
    public _4943LuckandPersistence() : base(4943)
    {
    }

    public override void Register()
    {
        int[] npcs = { 204096, 204097, 204075, 204053, 700538 };
        qe.RegisterQuestNpc(204053).AddOnQuestStart(questId); // Kvasir
        foreach (int npc in npcs)
            qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = env.GetTargetId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();

        // 0 - Start to Kvasir
        if (qs == null || qs.IsStartable())
        {
            if (targetId == 204053)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 4762);
                else
                    return SendQuestStartDialog(env);
            }
        }

        if (qs == null)
            return false;

        int var = qs.GetQuestVarById(0);

        if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                // 1 - Talk with Latatusk
                case 204096:
                    if (var == 0)
                    {
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                return SendQuestDialog(env, 1011);
                            case DialogAction.SETPRO1:
                                return DefaultCloseDialog(env, 0, 1);
                        }
                    }
                    if (var == 2)
                    {
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                return SendQuestDialog(env, 1693);
                            case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                                if (QuestService.CollectItemCheck(env, true))
                                {
                                    RemoveQuestItem(env, 122001275, 1);
                                    ChangeQuestStep(env, 2, 3);
                                    return SendQuestDialog(env, 10000);
                                }
                                else
                                    return SendQuestDialog(env, 10001);
                        }
                    }
                    break;
                // 2 - Talk with Relir.
                case 204097:
                    if (var == 1)
                    {
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                return SendQuestDialog(env, 1352);
                            case DialogAction.SELECT2_1_1:
                                if (player.GetInventory().TryDecreaseKinah(3400000))
                                {
                                    if (player.GetInventory().GetItemCountByItemId(122001275) == 0)
                                    {
                                        if (!GiveQuestItem(env, 122001275, 1))
                                            return true;
                                    }
                                    ChangeQuestStep(env, 1, 2);
                                    return SendQuestDialog(env, 1354);
                                }
                                else
                                {
                                    return SendQuestDialog(env, 1438);
                                }
                        }
                    }
                    break;
                case 700538:
                    if (dialogActionId == DialogAction.USE_OBJECT && var == 2)
                    {
                        return UseQuestObject(env, 2, 2, false, 0);
                    }
                    break;
                // 4 - Better purify yourself! Take Glossy Holy Water and visit High Priest Balder
                case 204075:
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 3)
                            {
                                return SendQuestDialog(env, 2034);
                            }
                            return false;
                        case DialogAction.SET_SUCCEED:
                            if (player.GetInventory().GetItemCountByItemId(186000084) >= 1)
                            {
                                RemoveQuestItem(env, 186000084, 1);
                                return DefaultCloseDialog(env, 3, 3, true, false);
                            }
                            else
                            {
                                return SendQuestDialog(env, 2120);
                            }
                        case DialogAction.FINISH_DIALOG:
                            return SendQuestSelectionDialog(env);
                    }
                    break;
                // No match
                default:
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            // 5 - Talk with Kvasir
            if (targetId == 204053)
            {
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
}
