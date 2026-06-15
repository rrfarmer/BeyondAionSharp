using Aion.GameServer.Model;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services.Teleport;

namespace Aion.GameServer.Handlers.Quest
{
    /// <summary>
    /// @author Artur, Majka
    /// </summary>
    public class _24016AStrangeNewThread : AbstractQuestHandler
    {
        public _24016AStrangeNewThread() : base(24016)
        {
        }

        public override void Register()
        {
            int[] npcs = { 203557, 700140, 700141 };
            qe.RegisterOnQuestCompleted(questId);
            qe.RegisterOnLevelChanged(questId);
            qe.RegisterOnDie(questId);
            qe.RegisterOnEnterWorld(questId);
            foreach (int npc in npcs)
                qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
            qe.RegisterQuestNpc(233876).AddOnKillEvent(questId);
            qe.RegisterQuestNpc(210753).AddOnKillEvent(questId);
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
                    case 203557: // Suthran
                        if (var == 0)
                        {
                            if (player.GetPlayerClass() == PlayerClass.RIDER) // Path for Rider
                            {
                                if (dialogActionId == DialogAction.QUEST_SELECT)
                                {
                                    return SendQuestDialog(env, 1011);
                                }
                                else if (dialogActionId == DialogAction.SELECT1_1_1)
                                {
                                    PlayQuestMovie(env, 219);
                                    return SendQuestDialog(env, 1013);
                                }
                            }
                            else // Path for other classes
                            {
                                if (dialogActionId == DialogAction.QUEST_SELECT)
                                {
                                    return SendQuestDialog(env, 1693);
                                }
                                else if (dialogActionId == DialogAction.SELECT3_1_1)
                                {
                                    PlayQuestMovie(env, 66);
                                    return SendQuestDialog(env, 1695);
                                }
                            }

                            if (dialogActionId == DialogAction.SETPRO1)
                            {
                                TeleportService.TeleportTo(player, 220030000, 2467.6052f, 2548.0076f, 316.12375f, (byte)63, TeleportAnimation.FADE_OUT_BEAM);
                                ChangeQuestStep(env, 0, 1); // 1
                                return CloseDialogWindow(env);
                            }
                        }
                        break;
                    case 700140: // Gate Guardian Stone
                        if (var == 2)
                        {
                            if (dialogActionId == DialogAction.USE_OBJECT)
                            {
                                if (player.GetPlayerClass() == PlayerClass.RIDER) // Spawn for Rider
                                {
                                    Spawn(233876, player, 260.12f, 234.93f, 216.00f, (byte)90); // Officer Tavasha
                                    return UseQuestObject(env, 2, 3, false, false); // 3
                                }
                                else // Spawn for other classes
                                {
                                    Spawn(210753, player, 260.12f, 234.93f, 216.00f, (byte)90); // Kuninasha
                                    return UseQuestObject(env, 2, 13, false, false); // 13
                                }
                            }
                        }
                        break;
                    case 700141: // Abyss Gate
                        if (var == 4 || var == 14)
                        {
                            ChangeQuestStep(env, var, var, true);
                            return PlayQuestMovie(env, 154);
                        }
                        break;
                }
            }
            else if (qs.GetStatus() == QuestStatus.REWARD)
            {
                if (targetId == 203557) // Suthran
                {
                    if (dialogActionId == DialogAction.USE_OBJECT)
                    {
                        if (player.GetPlayerClass() == PlayerClass.RIDER) // Reward for Rider
                        {
                            return SendQuestDialog(env, 2034);
                        }
                        else // Reward for other classes
                        {
                            return SendQuestDialog(env, 1352);
                        }
                    }
                    return SendQuestEndDialog(env);
                }
            }
            return false;
        }

        public override bool OnEnterWorldEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs == null)
                return false;
            if (qs.GetStatus() == QuestStatus.START)
            {
                int var = qs.GetQuestVars().GetQuestVars();
                if (var >= 2 && player.GetWorldId() != 320030000)
                {
                    ChangeQuestStep(env, var, 1);
                    return true;
                }
                else if (var == 1 && player.GetWorldId() == 320030000)
                {
                    ChangeQuestStep(env, 1, 2); // 2
                    return true;
                }
            }
            return false;
        }

        public override bool OnKillEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            if (player.GetPlayerClass() == PlayerClass.RIDER) // Kill for Rider
            {
                return DefaultOnKillEvent(env, 233876, 3, 4); // 4
            }
            else // Kill for other classes
            {
                return DefaultOnKillEvent(env, 210753, 13, 14); // 4
            }
        }

        public override bool OnDieEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs != null && qs.GetStatus() == QuestStatus.START)
            {
                int var = qs.GetQuestVars().GetQuestVars();
                if (var >= 2)
                {
                    qs.SetQuestVar(1);
                    UpdateQuestStatus(env);
                    return true;
                }
            }
            return false;
        }

        public override void OnMovieEndEvent(QuestEnv env, int movieId)
        {
            if (movieId == 154)
                TeleportService.TeleportTo(env.GetPlayer(), 220030000, 1683.2405f, 1757.608f, 259.44543f, (byte)64, TeleportAnimation.FADE_OUT_BEAM);
        }

        public override void OnQuestCompletedEvent(QuestEnv env)
        {
            int[] quests = { 24015, 24014, 24013, 24012, 24011 };
            DefaultOnQuestCompletedEvent(env, quests);
        }

        public override void OnLevelChangedEvent(Player player)
        {
            int[] quests = { 24015, 24014, 24013, 24012, 24011 };
            DefaultOnLevelChangedEvent(player, quests);
        }
    }
}
