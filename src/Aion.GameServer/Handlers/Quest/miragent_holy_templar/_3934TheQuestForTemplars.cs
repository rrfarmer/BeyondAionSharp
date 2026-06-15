using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest
{
    /// <summary>
    /// @author Nanou
    /// </summary>
    public class _3934TheQuestForTemplars : AbstractQuestHandler
    {
        public _3934TheQuestForTemplars() : base(3934)
        {
        }

        public override void Register()
        {
            int[] npcs = { 798359, 798360, 798361, 798362, 798363, 798364, 798365, 798366, 203752, 203701 };
            qe.RegisterQuestNpc(203701).AddOnQuestStart(questId); // Lavirintos
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

            // 0 - Start to Lavirintos
            if (qs == null || qs.IsStartable())
            {
                if (targetId == 203701)
                {
                    if (dialogActionId == DialogAction.QUEST_SELECT)
                        return SendQuestDialog(env, 4762);
                    else
                        return SendQuestStartDialog(env, 182206088, 1);
                }
            }

            if (qs == null)
                return false;

            int var = qs.GetQuestVarById(0);

            if (qs.GetStatus() == QuestStatus.START)
            {
                switch (targetId)
                {
                    // 1 - Talk with Nianalo
                    case 798359:
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                return SendQuestDialog(env, 1011);
                            case DialogAction.SETPRO1:
                                return DefaultCloseDialog(env, 0, 1); // 1
                        }
                        break;
                    // 2 - Talk with Navid
                    case 798360:
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
                    // 3 - Talk with Pavel
                    case 798361:
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
                        break;
                    // 4 - Talk with Pendaon
                    case 798362:
                        if (var == 3)
                        {
                            switch (dialogActionId)
                            {
                                case DialogAction.QUEST_SELECT:
                                    return SendQuestDialog(env, 2034);
                                case DialogAction.SETPRO4:
                                    return DefaultCloseDialog(env, 3, 4); // 4
                            }
                        }
                        break;
                    // 5 - Talk with Poevius
                    case 798363:
                        if (var == 4)
                        {
                            switch (dialogActionId)
                            {
                                case DialogAction.QUEST_SELECT:
                                    return SendQuestDialog(env, 2375);
                                case DialogAction.SETPRO5:
                                    return DefaultCloseDialog(env, 4, 5); // 5
                            }
                        }
                        break;
                    // 6 - Talk with Belicanon
                    case 798364:
                        if (var == 5)
                        {
                            switch (dialogActionId)
                            {
                                case DialogAction.QUEST_SELECT:
                                    return SendQuestDialog(env, 2716);
                                case DialogAction.SETPRO6:
                                    return DefaultCloseDialog(env, 5, 6); // 6
                            }
                        }
                        break;
                    // 7 - Talk with Mahelnu
                    case 798365:
                        if (var == 6)
                        {
                            switch (dialogActionId)
                            {
                                case DialogAction.QUEST_SELECT:
                                    return SendQuestDialog(env, 3057);
                                case DialogAction.SETPRO7:
                                    return DefaultCloseDialog(env, 6, 7); // 7
                            }
                        }
                        break;
                    // 8 - Talk with Pater
                    case 798366:
                        if (var == 7)
                        {
                            switch (dialogActionId)
                            {
                                case DialogAction.QUEST_SELECT:
                                    return SendQuestDialog(env, 3398);
                                case DialogAction.SETPRO8:
                                    return DefaultCloseDialog(env, 7, 8, 182206089, 1, 182206088, 1); // 8
                            }
                        }
                        break;
                    // 9 - Report the result to Jucleas with the Oath Stone
                    case 203752:
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                if (var == 8)
                                {
                                    return SendQuestDialog(env, 3739);
                                }
                                return false;
                            case DialogAction.SET_SUCCEED:
                                if (player.GetInventory().GetItemCountByItemId(186000080) >= 1)
                                {
                                    RemoveQuestItem(env, 186000080, 1);
                                    return DefaultCloseDialog(env, 8, 8, true, false);
                                }
                                else
                                {
                                    return SendQuestDialog(env, 3825);
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
                        return SendQuestEndDialog(env, new int[] { 182206089 });
                    }
                }
            }
            return false;
        }
    }
}
