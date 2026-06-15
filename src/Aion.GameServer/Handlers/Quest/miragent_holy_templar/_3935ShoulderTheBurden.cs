using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

namespace Aion.GameServer.Handlers.Quest
{
    /// <summary>
    /// @author Nanou
    /// </summary>
    public class _3935ShoulderTheBurden : AbstractQuestHandler
    {
        public _3935ShoulderTheBurden() : base(3935)
        {
        }

        public override void Register()
        {
            int[] npcs = { 203316, 203702, 203329, 203752, 203701 };
            qe.RegisterQuestNpc(203701).AddOnQuestStart(questId); // Lavirintos
            foreach (int npc in npcs)
                qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
        }

        public override bool OnDialogEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            int dialogActionId = env.GetDialogActionId();
            int targetId = env.GetTargetId();

            // 0 - Start to Lavirintos
            if (qs == null || qs.IsStartable())
            {
                if (targetId == 203701)
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
                    // 1 - Talk with Ettamirel
                    case 203316:
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                return SendQuestDialog(env, 1011);
                            case DialogAction.SETPRO1:
                                return DefaultCloseDialog(env, 0, 1); // 1
                        }
                        break;
                    // 2 - Talk with Jupion
                    case 203702:
                        if (var == 1)
                        {
                            switch (dialogActionId)
                            {
                                case DialogAction.QUEST_SELECT:
                                    return SendQuestDialog(env, 1352);
                                case DialogAction.SETPRO2:
                                    return DefaultCloseDialog(env, 1, 2); // 2
                            }
                        }
                        break;
                    // 3 - Talk with Elizar
                    case 203329:
                        if (var == 2)
                        {
                            switch (dialogActionId)
                            {
                                case DialogAction.QUEST_SELECT:
                                    return SendQuestDialog(env, 1693);
                                case DialogAction.SETPRO3:
                                    return DefaultCloseDialog(env, 2, 3); // 3
                            }
                        }
                        // 4 - Collect Holy Templar Medal and take them to Elizar
                        if (var == 3)
                        {
                            switch (dialogActionId)
                            {
                                case DialogAction.QUEST_SELECT:
                                    return SendQuestDialog(env, 2034);
                                case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                                    if (QuestService.CollectItemCheck(env, true))
                                    {
                                        ChangeQuestStep(env, 3, 4);
                                        return SendQuestDialog(env, 10000);
                                    }
                                    else
                                        return SendQuestDialog(env, 10001);
                            }
                        }
                        break;
                    // 5 - Report the result to Jucleas with the Oath Stone
                    case 203752:
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                if (var == 4)
                                {
                                    return SendQuestDialog(env, 2375);
                                }
                                return false;
                            case DialogAction.SET_SUCCEED:
                                if (player.GetInventory().GetItemCountByItemId(186000080) >= 1)
                                {
                                    RemoveQuestItem(env, 186000080, 1);
                                    return DefaultCloseDialog(env, 4, 4, true, false);
                                }
                                else
                                {
                                    return SendQuestDialog(env, 2461);
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
                        return SendQuestEndDialog(env);
                    }
                }
            }
            return false;
        }
    }
}
