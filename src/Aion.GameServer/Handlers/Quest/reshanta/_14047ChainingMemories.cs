using Aion.GameServer.Model;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services.Instance;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.Quest
{
    /// <summary>
    /// @author Artur, Majka, Pad
    /// </summary>
    public class _14047ChainingMemories : AbstractQuestHandler
    {
        private static readonly int icaronixNormalId = 233877; // elite version is not for this quest
        private static readonly int[] npc_ids = { 203704, 798154, 204574, 802051, 802052, 278500 };

        public _14047ChainingMemories() : base(14047)
        {
        }

        public override void Register()
        {
            qe.RegisterOnQuestCompleted(questId);
            qe.RegisterOnLevelChanged(questId);
            qe.RegisterOnEnterWorld(questId);
            qe.RegisterQuestNpc(icaronixNormalId).AddOnKillEvent(questId);
            foreach (int npc_id in npc_ids)
                qe.RegisterQuestNpc(npc_id).AddOnTalkEvent(questId);
        }

        public override void OnQuestCompletedEvent(QuestEnv env)
        {
            DefaultOnQuestCompletedEvent(env, 14040);
        }

        public override void OnLevelChangedEvent(Player player)
        {
            DefaultOnLevelChangedEvent(player, 14040);
        }

        public override bool OnDialogEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            int dialogActionId = env.GetDialogActionId();
            int targetId = env.GetTargetId();

            if (qs == null || qs.IsStartable())
            {
                return false;
            }
            else if (qs.GetStatus() == QuestStatus.START)
            {
                int var = qs.GetQuestVarById(0);
                if (targetId == npc_ids[0])
                {
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 0)
                            {
                                return SendQuestDialog(env, 1011);
                            }
                            break;
                        case DialogAction.SETPRO1:
                            if (var == 0)
                            {
                                ChangeQuestStep(env, 0, 1);
                                TeleportService.TeleportTo(player, 210060000, 2278.23f, 2217.8f, 59.27f, (byte)12, TeleportAnimation.FADE_OUT_BEAM);
                                return true;
                            }
                            break;
                    }
                }
                else if (targetId == npc_ids[1])
                {
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 1)
                            {
                                return SendQuestDialog(env, 1352);
                            }
                            break;
                        case DialogAction.SETPRO2:
                            if (var == 1)
                            {
                                ChangeQuestStep(env, 1, 2);
                                TeleportService.TeleportTo(player, 210040000, 713.6f, 625.36f, 129.75f, (byte)0, TeleportAnimation.FADE_OUT_BEAM);
                                return true;
                            }
                            break;
                    }
                }
                else if (targetId == npc_ids[2])
                {
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 2)
                            {
                                return SendQuestDialog(env, 1693);
                            }
                            break;
                        case DialogAction.SETPRO3:
                            ChangeQuestStep(env, 2, 3);
                            WorldMapInstance instance = InstanceService.GetNextAvailableInstance(WorldMapType.AZOTURAN_FORTRESS.GetId(), player);
                            Spawn(icaronixNormalId, instance, 478.8f, 431.1f, 1062.0067f, (byte)58);
                            TeleportService.TeleportTo(player, instance, 305.8f, 334.46f, 1019.69f, (byte)27, TeleportAnimation.FADE_OUT_BEAM);
                            return true;
                    }
                }
                else if (targetId == npc_ids[3])
                {
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 6)
                            {
                                return SendQuestDialog(env, 3057);
                            }
                            else if (var >= 3)
                            {
                                return SendQuestDialog(env, 2034);
                            }
                            break;
                        case DialogAction.SETPRO10:
                            if (var == 3)
                            {
                                ChangeQuestStep(env, 3, 4);
                            }
                            CloseDialogWindow(env);
                            player.SetState(CreatureState.FLYING);
                            player.UnsetState(CreatureState.ACTIVE);
                            player.SetFlightTeleportId(71001);
                            PacketSendUtility.SendPacket(player, new SM_EMOTION(player, EmotionType.START_FLYTELEPORT, 71001, 0));
                            return true;
                        case DialogAction.SET_SUCCEED:
                            if (var == 6)
                                return DefaultCloseDialog(env, 6, 6, true, false);
                            break;
                    }
                }
                else if (targetId == npc_ids[4])
                {
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var >= 4)
                            {
                                return SendQuestDialog(env, 2375);
                            }
                            break;
                        case DialogAction.SELECT5_1:
                            if (var == 4)
                            {
                                PlayQuestMovie(env, 421);
                            }
                            break;
                        case DialogAction.SETPRO11:
                            if (var == 4)
                            {
                                ChangeQuestStep(env, 4, 5);
                            }
                            CloseDialogWindow(env);
                            player.SetState(CreatureState.FLYING);
                            player.UnsetState(CreatureState.ACTIVE);
                            player.SetFlightTeleportId(72001);
                            PacketSendUtility.SendPacket(player, new SM_EMOTION(player, EmotionType.START_FLYTELEPORT, 72001, 0));
                            return true;
                    }
                }
            }
            else if (qs.GetStatus() == QuestStatus.REWARD)
            {
                if (targetId == npc_ids[5])
                {
                    if (dialogActionId == DialogAction.USE_OBJECT)
                        return SendQuestDialog(env, 3398);
                    else if (dialogActionId == DialogAction.SELECT_QUEST_REWARD)
                        return SendQuestDialog(env, 5);
                    else
                        return SendQuestEndDialog(env);
                }
            }
            return false;
        }

        public override bool OnKillEvent(QuestEnv env)
        {
            return DefaultOnKillEvent(env, icaronixNormalId, 5, 6);
        }

        public override bool OnEnterWorldEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);

            if (qs != null && qs.GetStatus() == QuestStatus.START)
            {
                if (player.GetWorldId() != WorldMapType.AZOTURAN_FORTRESS.GetId())
                {
                    int var = qs.GetQuestVarById(0);
                    if (var >= 3 && var <= 6)
                    {
                        ChangeQuestStep(env, var, 2);
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
