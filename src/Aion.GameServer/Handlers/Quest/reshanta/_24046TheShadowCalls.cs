using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services.Instance;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.World;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Handlers.Quest
{
    /// <summary>
    /// @author Artur, Majka
    /// </summary>
    public class _24046TheShadowCalls : AbstractQuestHandler
    {
        private static readonly int[] npcs = { 798300, 204253, 700369, 204089, 203550 };

        public _24046TheShadowCalls() : base(24046)
        {
        }

        public override void Register()
        {
            qe.RegisterOnQuestCompleted(questId);
            qe.RegisterOnLevelChanged(questId);
            foreach (int npc in npcs)
            {
                qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
            }
            qe.RegisterOnLeaveZone(ZoneName.Get("BALTASAR_HILL_VILLAGE_220050000"), questId);
            qe.RegisterOnDie(questId);
            qe.RegisterOnEnterWorld(questId);
        }

        public override void OnQuestCompletedEvent(QuestEnv env)
        {
            DefaultOnQuestCompletedEvent(env, 24040);
        }

        public override void OnLevelChangedEvent(Player player)
        {
            DefaultOnLevelChangedEvent(player, 24040);
        }

        public override bool OnDialogEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(env.GetQuestId());
            if (qs == null)
                return false;
            Npc target = (Npc)env.GetVisibleObject();
            int targetId = target.GetNpcId();
            int var = qs.GetQuestVarById(0);
            int dialogActionId = env.GetDialogActionId();

            if (qs.GetStatus() == QuestStatus.START)
            {
                switch (targetId)
                {
                    case 798300: // Phyper
                        if (dialogActionId == DialogAction.QUEST_SELECT && var == 0)
                        {
                            return SendQuestDialog(env, 1011);
                        }
                        if (dialogActionId == DialogAction.SETPRO1)
                        {
                            return DefaultCloseDialog(env, 0, 1); // 1
                        }
                        break;
                    case 204253: // Khrudgelmir
                        if (dialogActionId == DialogAction.QUEST_SELECT && var == 2)
                        {
                            return SendQuestDialog(env, 1693);
                        }
                        if (dialogActionId == DialogAction.QUEST_SELECT && var == 6)
                        {
                            return SendQuestDialog(env, 3057);
                        }
                        if (dialogActionId == DialogAction.SETPRO3)
                        {
                            RemoveQuestItem(env, 182205502, 1);
                            return DefaultCloseDialog(env, 2, 3); // 3
                        }
                        if (dialogActionId == DialogAction.SET_SUCCEED)
                        {
                            return DefaultCloseDialog(env, 6, 6, true, false); // reward
                        }
                        break;
                    case 700369: // Underground Arena Exit
                        if (dialogActionId == DialogAction.USE_OBJECT && var == 5)
                        {
                            TeleportService.TeleportTo(player, 120010000, 981.6009f, 1552.97f, 210.46f);
                            ChangeQuestStep(env, 5, 6); // 6
                            return true;
                        }
                        break;
                    case 204089: // Garm
                        if (dialogActionId == DialogAction.QUEST_SELECT && var == 3)
                        {
                            return SendQuestDialog(env, 2034);
                        }
                        if (dialogActionId == DialogAction.SETPRO4)
                        {
                            WorldMapInstance newInstance = InstanceService.GetNextAvailableInstance(WorldMapType.SHADOW_COURT_DUNGEON.GetId(), player);
                            TeleportService.TeleportTo(player, newInstance, 591.47894f, 420.20865f, 202.97754f);
                            PlayQuestMovie(env, 423);
                            ChangeQuestStep(env, 3, 5); // 5
                            return CloseDialogWindow(env);
                        }
                        break;
                }
            }
            else if (qs.GetStatus() == QuestStatus.REWARD)
            {
                if (targetId == 203550) // Munin
                {
                    if (dialogActionId == DialogAction.USE_OBJECT)
                    {
                        return SendQuestDialog(env, 10002);
                    }
                    else
                    {
                        int[] questItems = { 182205502 };
                        return SendQuestEndDialog(env, questItems);
                    }
                }
            }
            return false;
        }

        public override bool OnLeaveZoneEvent(QuestEnv env, ZoneName zoneName)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(env.GetQuestId());
            if (qs != null && qs.GetStatus() == QuestStatus.START)
            {
                int var = qs.GetQuestVarById(0);
                if (zoneName == ZoneName.Get("BALTASAR_HILL_VILLAGE_220050000") && var == 1)
                {
                    GiveQuestItem(env, 182205502, 1);
                    ChangeQuestStep(env, 1, 2); // 2
                    return true;
                }
            }
            return false;
        }

        public override bool OnEnterWorldEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (player.GetWorldId() == 320120000 || qs == null || qs.GetStatus() != QuestStatus.START)
                return false;
            int var = qs.GetQuestVarById(0);
            if (var == 5)
            {
                qs.SetQuestVarById(0, 3); // 3
                UpdateQuestStatus(env);
                return true;
            }
            return false;
        }

        public override bool OnDieEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs == null || qs.GetStatus() != QuestStatus.START)
                return false;
            int var = qs.GetQuestVarById(0);
            if (var == 5)
            {
                qs.SetQuestVarById(0, 3); // 3
                UpdateQuestStatus(env);
                return true;
            }
            return false;
        }
    }
}
