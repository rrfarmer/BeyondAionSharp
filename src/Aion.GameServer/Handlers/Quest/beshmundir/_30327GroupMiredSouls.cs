using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

namespace Aion.GameServer.Handlers.Quest
{
    /// <summary>
    /// @author Gigi
    /// </summary>
    public class _30327GroupMiredSouls : AbstractQuestHandler
    {
        public _30327GroupMiredSouls() : base(30327)
        {
        }

        public override void Register()
        {
            int[] mobs = { 216586, 216735, 216734, 216737, 216245 };
            qe.RegisterQuestNpc(799244).AddOnQuestStart(questId);
            qe.RegisterQuestNpc(799244).AddOnTalkEvent(questId);
            qe.RegisterQuestNpc(799521).AddOnTalkEvent(questId);
            qe.RegisterQuestNpc(799517).AddOnTalkEvent(questId);
            foreach (int mob in mobs)
                qe.RegisterQuestNpc(mob).AddOnKillEvent(questId);
            qe.RegisterOnQuestTimerEnd(questId);
        }

        public override bool OnDialogEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            int targetId = env.GetTargetId();
            int dialogActionId = env.GetDialogActionId();

            if (qs == null || qs.IsStartable())
            {
                if (targetId == 799244)
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

            if (qs == null)
                return false;

            if (qs.GetStatus() == QuestStatus.START)
            {
                switch (targetId)
                {
                    case 799521:
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                            {
                                if (qs.GetQuestVarById(0) == 0)
                                {
                                    return SendQuestDialog(env, 1011);
                                }
                                return false;
                            }
                            case DialogAction.SETPRO1:
                                return DefaultCloseDialog(env, 0, 1);
                        }
                        return false;
                    case 799517:
                        switch (dialogActionId)
                        {
                            case DialogAction.SETPRO1:
                            {
                                QuestService.QuestTimerStart(env, 300);
                                return true;
                            }
                        }
                        break;
                }
            }
            else if (qs.GetStatus() == QuestStatus.REWARD)
            {
                if (targetId == 799244)
                {
                    switch (dialogActionId)
                    {
                        case DialogAction.USE_OBJECT:
                            return SendQuestDialog(env, 10002);
                        case DialogAction.SELECT_QUEST_REWARD:
                            return SendQuestDialog(env, 5);
                        default:
                            return SendQuestEndDialog(env);
                    }
                }
            }
            return false;
        }

        public override bool OnQuestTimerEndEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);

            if (qs == null)
                return false;

            int var = qs.GetQuestVarById(0);

            if (qs.GetStatus() == QuestStatus.START)
            {
                if (var == 1)
                {
                    qs.SetQuestVarById(0, 0);
                    UpdateQuestStatus(env);
                    return true;
                }
            }
            return false;
        }

        public override bool OnKillEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);

            if (qs == null || qs.GetStatus() != QuestStatus.START)
            {
                return false;
            }

            int targetId = 0;
            if (env.GetVisibleObject() is Npc)
                targetId = ((Npc)env.GetVisibleObject()).GetNpcId();

            switch (targetId)
            {
                case 216586:
                    if (qs.GetQuestVarById(0) == 1)
                    {
                        QuestService.QuestTimerEnd(env);
                        qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                        UpdateQuestStatus(env);
                        PlayQuestMovie(env, 445);
                        return true;
                    }
                    break;
                case 216735:
                case 216734:
                case 216737:
                case 216245:
                    if (qs.GetQuestVarById(0) == 2)
                    {
                        qs.SetStatus(QuestStatus.REWARD);
                        UpdateQuestStatus(env);
                        return true;
                    }
                    break;
            }
            return false;
        }
    }
}
