using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest
{
    /// <summary>
    /// @author Nanou, vlog
    /// </summary>
    public class _4937RecognitionOfThePreceptors : AbstractQuestHandler
    {
        public _4937RecognitionOfThePreceptors() : base(4937)
        {
        }

        public override void Register()
        {
            int[] npcs = { 204053, 204059, 204058, 204057, 204056, 204075, 801222, 801223 };
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

            if (qs == null || qs.IsStartable())
            {
                if (targetId == 204053) // Kvasir
                {
                    if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    {
                        return SendQuestDialog(env, 4762);
                    }
                    else
                    {
                        return SendQuestStartDialog(env, 182207112, 1);
                    }
                }
            }
            else if (qs.GetStatus() == QuestStatus.START)
            {
                int var = qs.GetQuestVarById(0);
                switch (targetId)
                {
                    case 204059: // Freyr
                        switch (env.GetDialogActionId())
                        {
                            case DialogAction.QUEST_SELECT:
                                return SendQuestDialog(env, 1011);
                            case DialogAction.SETPRO1:
                                return DefaultCloseDialog(env, 0, 1); // 1
                        }
                        break;
                    case 204058: // Sif
                        switch (env.GetDialogActionId())
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
                    case 204057: // Sigyn
                        switch (env.GetDialogActionId())
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
                    case 204056: // Traufnir
                        switch (env.GetDialogActionId())
                        {
                            case DialogAction.QUEST_SELECT:
                                if (var == 3)
                                {
                                    return SendQuestDialog(env, 2034);
                                }
                                return false;
                            case DialogAction.SETPRO4:
                                return DefaultCloseDialog(env, 3, 4, 182207113, 1, 182207112, 1); // 4
                        }
                        break;
                    case 801222: // Hadubrant
                        switch (env.GetDialogActionId())
                        {
                            case DialogAction.QUEST_SELECT:
                                if (var == 4)
                                {
                                    return SendQuestDialog(env, 2375);
                                }
                                return false;
                            case DialogAction.SETPRO5:
                                return DefaultCloseDialog(env, 4, 5);
                        }
                        break;
                    case 801223: // Brynhilde
                        switch (env.GetDialogActionId())
                        {
                            case DialogAction.QUEST_SELECT:
                                if (var == 5)
                                {
                                    return SendQuestDialog(env, 2716);
                                }
                                return false;
                            case DialogAction.SETPRO6:
                                return DefaultCloseDialog(env, 5, 6);
                        }
                        break;
                    case 204075: // Balder
                        switch (env.GetDialogActionId())
                        {
                            case DialogAction.QUEST_SELECT:
                                if (var == 6 && CheckItemExistence(env, 182207113, 1, false))
                                {
                                    return SendQuestDialog(env, 3057);
                                }
                                return false;
                            case DialogAction.FINISH_DIALOG:
                                return DefaultCloseDialog(env, var, var);
                            case DialogAction.SET_SUCCEED:
                                return CheckItemExistence(env, 6, 6, true, 186000084, 1, true, 0, 3143, 0, 0); // reward
                        }
                        break;
                }
            }
            else if (qs.GetStatus() == QuestStatus.REWARD)
            {
                if (targetId == 204053) // Kvasir
                {
                    if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
                    {
                        return SendQuestDialog(env, 10002);
                    }
                    else if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD)
                    {
                        return SendQuestDialog(env, 5);
                    }
                    else
                    {
                        if (CheckItemExistence(env, 182207113, 1, true))
                        {
                            return SendQuestEndDialog(env);
                        }
                        else
                        {
                            return SendQuestSelectionDialog(env);
                        }
                    }
                }
            }
            return false;
        }
    }
}
