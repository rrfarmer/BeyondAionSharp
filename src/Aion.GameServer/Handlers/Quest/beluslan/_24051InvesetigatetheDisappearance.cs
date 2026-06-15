using System.Threading.Tasks;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Handlers.Quest
{
    /// <summary>
    /// @author Ritsu, Majka
    /// </summary>
    public class _24051InvesetigatetheDisappearance : AbstractQuestHandler
    {
        public _24051InvesetigatetheDisappearance() : base(24051)
        {
        }

        public override void Register()
        {
            int[] npcs = { 204707, 204749, 204800, 700359 };
            qe.RegisterOnQuestCompleted(questId);
            qe.RegisterOnLevelChanged(questId);
            qe.RegisterQuestItem(182215375, questId);
            qe.RegisterOnEnterZone(ZoneName.Get("MINE_PORT_220040000"), questId);
            qe.RegisterOnEnterWorld(questId);
            foreach (int npc in npcs)
            {
                qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
            }
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
                if (targetId == 204707) // Mani
                {
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 0)
                            {
                                return SendQuestDialog(env, 1011);
                            }
                            else if (var == 3)
                            {
                                return SendQuestDialog(env, 2034);
                            }
                            return false;
                        case DialogAction.SETPRO1:
                            return DefaultCloseDialog(env, 0, 1); // 1
                        case DialogAction.SETPRO4:
                            RemoveQuestItem(env, 182215375, 1);
                            return DefaultCloseDialog(env, 3, 4); // 4
                    }
                }
                else if (targetId == 204749) // Paeru
                {
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 1)
                            {
                                return SendQuestDialog(env, 1352);
                            }
                            return false;
                        case DialogAction.SETPRO2:
                            return DefaultCloseDialog(env, 1, 2, 182215375, 1, 0, 0); // 2
                    }
                }
                else if (targetId == 204800) // Hammel
                {
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
                }
                else if (targetId == 700359 && var == 5 && player.GetInventory().GetItemCountByItemId(182215377) >= 1) // Secret Port Entrance
                {
                    if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
                    {
                        TeleportService.TeleportTo(player, player.GetWorldId(), player.GetInstanceId(), 1757.82f, 1392.94f, 401.75f, (byte)94);
                        return true;
                    }
                }
            }
            else if (qs.GetStatus() == QuestStatus.REWARD)
            {
                if (targetId == 204707) // Mani
                {
                    if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
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

        public override HandlerResult OnItemUseEvent(QuestEnv env, Item item)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs != null && qs.GetStatus() == QuestStatus.START)
            {
                ChangeQuestStep(env, 2, 3);
                return HandlerResult.SUCCESS; // 3
            }
            return HandlerResult.FAILED;
        }

        public override bool OnEnterZoneEvent(QuestEnv env, ZoneName name)
        {
            Player player = env.GetPlayer();
            if (player == null)
                return false;
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs != null && qs.GetStatus() == QuestStatus.START)
            {
                int var = qs.GetQuestVarById(0);
                if (name == ZoneName.Get("MINE_PORT_220040000"))
                {
                    if (var == 5)
                    {
                        ThreadPoolManager.GetInstance().Schedule(ct =>
                        {
                            PlayQuestMovie(env, 236);
                            return ValueTask.CompletedTask;
                        }, 10000L);
                    }
                }
            }
            return false;
        }

        public override void OnMovieEndEvent(QuestEnv env, int movieId)
        {
            if (movieId == 236)
            {
                ChangeQuestStep(env, 5, 5, true); // reward
                RemoveQuestItem(env, 182215377, 1);
            }
        }

        public override void OnQuestCompletedEvent(QuestEnv env)
        {
            DefaultOnQuestCompletedEvent(env, 24050);
        }

        public override void OnLevelChangedEvent(Player player)
        {
            DefaultOnLevelChangedEvent(player, 24050);
        }
    }
}
