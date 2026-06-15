using System.Threading.Tasks;
using Aion.GameServer.Model;
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
    /// @author MrPoke, vlog, Majka
    /// </summary>
    public class _1002RequestoftheElim : AbstractQuestHandler
    {
        public _1002RequestoftheElim() : base(1002)
        {
        }

        public override void Register()
        {
            int[] npcs = { 203076, 730007, 730010, 730008, 205000, 203067 };
            qe.RegisterOnQuestCompleted(questId);
            qe.RegisterOnLevelChanged(questId);
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
            int dialogActionId = env.GetDialogActionId();
            if (qs == null)
                return false;
            int var = qs.GetQuestVarById(0);
            int targetId = env.GetTargetId();

            if (qs.GetStatus() == QuestStatus.START)
            {
                switch (targetId)
                {
                    case 203076: // Ampeis
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
                    case 730007: // Forest Protector Noah
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                if (var == 1)
                                {
                                    return SendQuestDialog(env, 1352);
                                }
                                else if (var == 5)
                                {
                                    return SendQuestDialog(env, 1693);
                                }
                                else if (var == 6)
                                {
                                    return SendQuestDialog(env, 2034);
                                }
                                else if (var == 12)
                                {
                                    return SendQuestDialog(env, 2120);
                                }
                                return false;
                            case DialogAction.SELECT2_1:
                                if (var == 1)
                                {
                                    PlayQuestMovie(env, 20);
                                    return SendQuestDialog(env, 1353);
                                }
                                return false;
                            case DialogAction.SETPRO2:
                                return DefaultCloseDialog(env, 1, 2, 182200002, 1, 0, 0); // 2
                            case DialogAction.SETPRO3:
                                return DefaultCloseDialog(env, 5, 6, 0, 0, 182200002, 1); // 6
                            case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                                if (var == 6)
                                {
                                    return CheckQuestItems(env, 6, 12, false, 2120, 2205); // 12
                                }
                                else if (var == 12)
                                {
                                    return SendQuestDialog(env, 2120);
                                }
                                return false;
                            case DialogAction.SETPRO4:
                                return DefaultCloseDialog(env, 12, 13); // 13
                            case DialogAction.FINISH_DIALOG:
                                return SendQuestSelectionDialog(env);
                        }
                        break;
                    case 730010: // Sleeping Elder
                        if (dialogActionId == DialogAction.USE_OBJECT)
                        {
                            if (player.GetInventory().GetItemCountByItemId(182200002) == 1)
                            {
                                if (var == 2)
                                {
                                    env.GetVisibleObject().GetController().DeleteAndScheduleRespawn();
                                    return UseQuestObject(env, 2, 4, false, false); // 4
                                }
                                else if (var == 4)
                                {
                                    env.GetVisibleObject().GetController().DeleteAndScheduleRespawn();
                                    return UseQuestObject(env, 4, 5, false, false); // 5
                                }
                            }
                        }
                        break;
                    case 730008: // Daminu
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                if (var == 13)
                                {
                                    return SendQuestDialog(env, 2375);
                                }
                                else if (var == 14)
                                {
                                    return SendQuestDialog(env, 2461);
                                }
                                return false;
                            case DialogAction.SETPRO5:
                                WorldMapInstance newInstance = InstanceService.GetNextAvailableInstance(WorldMapType.KARAMATIS.GetId(), player);
                                TeleportService.TeleportTo(player, newInstance, 52, 174, 229);
                                ChangeQuestStep(env, 13, 20); // 20
                                return CloseDialogWindow(env);
                            case DialogAction.SETPRO6:
                                return DefaultCloseDialog(env, 14, 14, true, false); // reward
                        }
                        break;
                    case 205000: // Belpartan
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                if (var == 20)
                                {
                                    player.SetState(CreatureState.FLYING);
                                    player.UnsetState(CreatureState.ACTIVE);
                                    player.SetFlightTeleportId(1001);
                                    PacketSendUtility.SendPacket(player, new SM_EMOTION(player, EmotionType.START_FLYTELEPORT, 1001, 0));
                                    ThreadPoolManager.GetInstance().Schedule(ct =>
                                    {
                                        ChangeQuestStep(env, 20, 14); // 14
                                        TeleportService.TeleportTo(player, 210010000, 1, 603, 1537, 116, (byte)20);
                                        return ValueTask.CompletedTask;
                                    }, 43000L);
                                    return true;
                                }
                                break;
                        }
                        break;
                }
            }
            else if (qs.GetStatus() == QuestStatus.REWARD)
            {
                if (targetId == 203067) // Kalio
                {
                    if (dialogActionId == DialogAction.USE_OBJECT)
                    {
                        return SendQuestDialog(env, 2716);
                    }
                    else
                    {
                        return SendQuestEndDialog(env);
                    }
                }
            }
            return false;
        }

        public override bool OnEnterWorldEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs != null && qs.GetStatus() == QuestStatus.START)
            {
                if (player.GetWorldId() == 310010000)
                {
                    PacketSendUtility.SendPacket(player, new SM_ASCENSION_MORPH(1));
                    return true;
                }
                else
                {
                    int var = qs.GetQuestVarById(0);
                    if (var == 20)
                    {
                        ChangeQuestStep(env, 20, 13); // 13
                    }
                }
            }
            return false;
        }

        public override bool OnCanAct(QuestEnv env, QuestActionType questEventType, params object[] objects)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(env.GetQuestId());
            int targetId = env.GetTargetId();
            if (targetId == 730010)
            {
                if (qs == null || qs.GetStatus() != QuestStatus.START || (qs.GetQuestVarById(0) != 2 && qs.GetQuestVarById(0) != 4))
                {
                    return false;
                }
            }
            return true;
        }

        public override void OnQuestCompletedEvent(QuestEnv env)
        {
            DefaultOnQuestCompletedEvent(env, 1100);
        }

        public override void OnLevelChangedEvent(Player player)
        {
            DefaultOnLevelChangedEvent(player, 1100);
        }
    }
}
