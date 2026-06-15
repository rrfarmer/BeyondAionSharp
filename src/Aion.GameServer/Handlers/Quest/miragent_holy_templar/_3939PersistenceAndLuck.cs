using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// Talk with Cornelius (203780). Talk with Sabotes (203781). Collect Tear of Luck (182206098) (20) and take them to Cornelius. Take the Oath Stone
/// (186000080) to High Priest Jucleas (203752) and ask him to perform the ritual of affirmation. Talk with Lavirintos (203701).
/// @author Nanou, vlog, Gigi
/// </summary>
public class _3939PersistenceAndLuck : AbstractQuestHandler
{
    public _3939PersistenceAndLuck() : base(3939)
    {
    }

    public override void Register()
    {
        int[] npcs = { 203701, 203780, 203781, 203752, 700537 };
        qe.RegisterQuestNpc(203701).AddOnQuestStart(questId);
        foreach (int npc in npcs)
        {
            qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
        }
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = env.GetTargetId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 203701)
            { // Lavirintos
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
                case 203780: // Cornelius
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 0)
                                return SendQuestDialog(env, 1011);
                            if (var == 2)
                                return SendQuestDialog(env, 1693);
                            return false;
                        case DialogAction.SETPRO1:
                            return DefaultCloseDialog(env, 0, 1); // 1
                        case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                            return CheckQuestItems(env, 2, 3, false, 10000, 10001, 182206099, 1); // 3
                        case DialogAction.FINISH_DIALOG:
                            return DefaultCloseDialog(env, var, var);
                    }
                    break;
                case 203781: // Sabotes
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 1)
                                return SendQuestDialog(env, 1352);
                            return false;
                        case DialogAction.SELECT2_1_1:
                            if (var == 1 && player.GetInventory().TryDecreaseKinah(3400000))
                            {
                                return DefaultCloseDialog(env, 1, 2, 122001274, 1, 0, 0); // 2
                            }
                            else
                            {
                                return SendQuestDialog(env, 1438);
                            }
                        case DialogAction.FINISH_DIALOG:
                            return DefaultCloseDialog(env, 1, 1);
                    }
                    break;
                case 700537:
                    if (dialogActionId == DialogAction.USE_OBJECT && var == 2)
                    {
                        return UseQuestObject(env, 2, 2, false, 0);
                    }
                    break;
                case 203752: // Jucleas
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 3)
                            {
                                return SendQuestDialog(env, 2034);
                            }
                            return false;
                        case DialogAction.SET_SUCCEED:
                            if (player.GetInventory().GetItemCountByItemId(186000080) >= 1)
                            {
                                RemoveQuestItem(env, 186000080, 1);
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
            if (targetId == 203701)
            { // Lavirintos
                if (dialogActionId == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 10002);
                else
                    return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
