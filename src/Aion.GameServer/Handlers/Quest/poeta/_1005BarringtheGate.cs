using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest
{
    /// <summary>
    /// @author MrPoke, Majka
    /// </summary>
    public class _1005BarringtheGate : AbstractQuestHandler
    {
        public _1005BarringtheGate() : base(1005)
        {
        }

        public override void Register()
        {
            int[] talkNpcs = { 203067, 203081, 790001, 203085, 203086, 700080, 700081, 700082, 700083 };
            qe.RegisterOnQuestCompleted(questId);
            qe.RegisterOnLevelChanged(questId);
            foreach (int id in talkNpcs)
                qe.RegisterQuestNpc(id).AddOnTalkEvent(questId);
        }

        public override bool OnDialogEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs == null)
                return false;

            int var = qs.GetQuestVarById(0);
            int targetId = 0;
            if (env.GetVisibleObject() is Npc)
                targetId = ((Npc)env.GetVisibleObject()).GetNpcId();

            if (qs.GetStatus() == QuestStatus.START)
            {
                if (targetId == 203067)
                {
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 0)
                                return SendQuestDialog(env, 1011);
                            return false;
                        case DialogAction.SETPRO1:
                            if (var == 0)
                            {
                                qs.SetQuestVarById(0, var + 1);
                                UpdateQuestStatus(env);
                                SendQuestSelectionDialog(env);
                                return true;
                            }
                            break;
                    }
                }
                else if (targetId == 203081)
                {
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 1)
                                return SendQuestDialog(env, 1352);
                            return false;
                        case DialogAction.SETPRO2:
                            if (var == 1)
                            {
                                qs.SetQuestVarById(0, var + 1);
                                UpdateQuestStatus(env);
                                SendQuestSelectionDialog(env);
                                return true;
                            }
                            break;
                    }
                }
                else if (targetId == 790001)
                {
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 2)
                                return SendQuestDialog(env, 1693);
                            return false;
                        case DialogAction.SETPRO3:
                            if (var == 2)
                            {
                                qs.SetQuestVarById(0, var + 1);
                                UpdateQuestStatus(env);
                                SendQuestSelectionDialog(env);
                                return true;
                            }
                            break;
                    }
                }
                else if (targetId == 203085)
                {
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 3)
                                return SendQuestDialog(env, 2034);
                            return false;
                        case DialogAction.SETPRO4:
                            if (var == 3)
                            {
                                qs.SetQuestVarById(0, var + 1);
                                UpdateQuestStatus(env);
                                SendQuestSelectionDialog(env);
                                return true;
                            }
                            break;
                    }
                }
                else if (targetId == 203086)
                {
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 4)
                                return SendQuestDialog(env, 2375);
                            return false;
                        case DialogAction.SETPRO5:
                            if (var == 4)
                            {
                                qs.SetQuestVarById(0, var + 1);
                                UpdateQuestStatus(env);
                                SendQuestSelectionDialog(env);
                                return true;
                            }
                            break;
                    }
                }
                else if (targetId == 700081)
                {
                    if (var == 5)
                    {
                        Destroy(6, env);
                        return false;
                    }
                }
                else if (targetId == 700082)
                {
                    if (var == 6)
                    {
                        Destroy(7, env);
                        return false;
                    }
                }
                else if (targetId == 700083)
                {
                    if (var == 7)
                    {
                        Destroy(8, env);
                        return false;
                    }
                }
                else if (targetId == 700080)
                {
                    if (var == 8)
                    {
                        Destroy(-1, env);
                        return false;
                    }
                }
            }
            else if (qs.GetStatus() == QuestStatus.REWARD)
            {
                if (targetId == 203067)
                {
                    if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
                    {
                        PlayQuestMovie(env, 171);
                        return SendQuestDialog(env, 2716);
                    }
                    else
                        return SendQuestEndDialog(env);
                }
            }
            return false;
        }

        public override void OnQuestCompletedEvent(QuestEnv env)
        {
            int[] quests = { 1100, 1004, 1003, 1002, 1001 };
            DefaultOnQuestCompletedEvent(env, quests);
        }

        public override void OnLevelChangedEvent(Player player)
        {
            int[] quests = { 1100, 1004, 1003, 1002, 1001 };
            DefaultOnLevelChangedEvent(player, quests);
        }

        private void Destroy(int var, QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (var != -1)
                qs.SetQuestVarById(0, var);
            else
            {
                PlayQuestMovie(env, 21);
                qs.SetStatus(QuestStatus.REWARD);
            }
            UpdateQuestStatus(env);
        }
    }
}
