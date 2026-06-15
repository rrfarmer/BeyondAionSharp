using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Nanou
/// </summary>
public class _3933ClassPreceptorConsent : AbstractQuestHandler
{
    public _3933ClassPreceptorConsent() : base(3933)
    {
    }

    public override void Register()
    {
        int[] npcs = { 203704, 203705, 203706, 203707, 203752, 203701, 801214, 801215 };
        qe.RegisterQuestNpc(203701).AddOnQuestStart(questId); // Lavirintos
        foreach (int npc in npcs)
            qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = env.GetTargetId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();

        // 0 - Start to Lavirintos
        if (qs == null || qs.IsStartable())
        {
            if (targetId == 203701)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 4762);
                else
                    return SendQuestStartDialog(env, 182206086, 1);
            }
        }

        if (qs == null)
            return false;

        int var = qs.GetQuestVarById(0);

        if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 203704: // 1 - Receive the signature of Boreas on the recommendation letter.
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            return SendQuestDialog(env, 1011);
                        case DialogAction.SETPRO1:
                            return DefaultCloseDialog(env, 0, 1);
                    }
                    break;
                case 203705: // 2 - Receive the signature of Jumentis on the recommendation letter.
                    if (var == 1)
                    {
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                return SendQuestDialog(env, 1352);
                            case DialogAction.SETPRO2:
                                return DefaultCloseDialog(env, 1, 2);
                        }
                    }
                    break;
                case 203706: // 3 - Receive the signature of Charna on the recommendation letter.
                    if (var == 2)
                    {
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                return SendQuestDialog(env, 1693);
                            case DialogAction.SETPRO3:
                                return DefaultCloseDialog(env, 2, 3);
                        }
                    }
                    break;
                case 203707: // 4 - Receive the signature of Thrasymedes on the recommendation letter.
                    if (var == 3)
                    {
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                return SendQuestDialog(env, 2034);
                            case DialogAction.SETPRO4:
                                return DefaultCloseDialog(env, 3, 4);
                        }
                    }
                    break;
                case 801214: // 5 - Receive the signature of Oakley on the recommendation letter.
                    if (var == 4)
                    {
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                return SendQuestDialog(env, 2375);
                            case DialogAction.SETPRO5:
                                return DefaultCloseDialog(env, 4, 5);
                        }
                    }
                    break;
                case 801215: // 6 - Receive the signature of Dion on the recommendation letter.
                    if (var == 5)
                    {
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                return SendQuestDialog(env, 2716);
                            case DialogAction.SETPRO6:
                                return DefaultCloseDialog(env, 5, 6, 182206087, 1, 182206086, 1);
                        }
                    }
                    break;
                case 203752: // 7 - Report the result to Lavirintos with the Oath Stone
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 6)
                            {
                                return SendQuestDialog(env, 3057);
                            }
                            return false;
                        case DialogAction.SET_SUCCEED:
                            if (player.GetInventory().GetItemCountByItemId(186000080) >= 1)
                            {
                                RemoveQuestItem(env, 186000080, 1);
                                return DefaultCloseDialog(env, 6, 6, true, false);
                            }
                            else
                            {
                                return SendQuestDialog(env, 3143);
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
            {
                if (dialogActionId == DialogAction.USE_OBJECT)
                {
                    return SendQuestDialog(env, 10002);
                }
                else
                {
                    return SendQuestEndDialog(env, new int[] { 182206087 });
                }
            }
        }
        return false;
    }
}
