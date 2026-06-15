using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest
{
    /// <summary>
    /// @author Nanou, vlog
    /// </summary>
    public class _4938WorkOfTheFenrisFangs : AbstractQuestHandler
    {
        public _4938WorkOfTheFenrisFangs() : base(4938)
        {
        }

        public override void Register()
        {
            int[] npcs = { 204053, 798367, 798368, 798369, 798370, 798371, 798372, 798373, 798374, 204075 };
            qe.RegisterQuestNpc(204053).AddOnQuestStart(questId);
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
                if (targetId == 204053) // Kvasir
                {
                    if (dialogActionId == DialogAction.QUEST_SELECT)
                    {
                        return SendQuestDialog(env, 4762);
                    }
                    else
                    {
                        return SendQuestStartDialog(env);
                    }
                }
            }
            else if (qs.GetStatus() == QuestStatus.START)
            {
                int var = qs.GetQuestVarById(0);
                switch (targetId)
                {
                    case 798367: // Riikaard
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                if (var == 0)
                                {
                                    return SendQuestDialog(env, 1011);
                                }
                                return false;
                            case DialogAction.SETPRO1:
                                return DefaultCloseDialog(env, 0, 1); // 1
                        }
                        break;
                    case 798368: // Herosir
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
                    case 798369: // Gellner
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                if (var == 2)
                                {
                                    return SendQuestDialog(env, 1693);
                                }
                                return false;
                            case DialogAction.SETPRO3:
                                return DefaultCloseDialog(env, 2, 3); // 3
                        }
                        break;
                    case 798370: // Natorp
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                if (var == 3)
                                {
                                    return SendQuestDialog(env, 2034);
                                }
                                return false;
                            case DialogAction.SETPRO4:
                                return DefaultCloseDialog(env, 3, 4); // 4
                        }
                        break;
                    case 798371: // Needham
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                if (var == 4)
                                {
                                    return SendQuestDialog(env, 2375);
                                }
                                return false;
                            case DialogAction.SETPRO5:
                                return DefaultCloseDialog(env, 4, 5); // 5
                        }
                        break;
                    case 798372: // Landsberg
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                if (var == 5)
                                {
                                    return SendQuestDialog(env, 2716);
                                }
                                return false;
                            case DialogAction.SETPRO6:
                                return DefaultCloseDialog(env, 5, 6); // 6
                        }
                        break;
                    case 798373: // Levinard
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                if (var == 6)
                                {
                                    return SendQuestDialog(env, 3057);
                                }
                                return false;
                            case DialogAction.SETPRO7:
                                return DefaultCloseDialog(env, 6, 7); // 7
                        }
                        break;
                    case 798374: // Lonergan
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                if (var == 7)
                                {
                                    return SendQuestDialog(env, 3398);
                                }
                                return false;
                            case DialogAction.SETPRO8:
                                return DefaultCloseDialog(env, 7, 8); // 8
                        }
                        break;
                    case 204075: // Balder
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                if (var == 8)
                                {
                                    return SendQuestDialog(env, 3739);
                                }
                                return false;
                            case DialogAction.SET_SUCCEED:
                                if (player.GetInventory().GetItemCountByItemId(186000084) >= 1)
                                {
                                    RemoveQuestItem(env, 186000084, 1);
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
                }
            }
            else if (qs.GetStatus() == QuestStatus.REWARD)
            {
                if (targetId == 204053) // Kvasir
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
