using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest
{
    /// <summary>
    /// @author Artur, Majka
    /// </summary>
    public class _14045RumorsOnWings : AbstractQuestHandler
    {
        public _14045RumorsOnWings() : base(14045)
        {
        }

        public override void Register()
        {
            int[] npcs = { 278506, 279023, 278643 };
            qe.RegisterOnQuestCompleted(questId);
            qe.RegisterOnLevelChanged(questId);
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
            {
                return false;
            }
            int var = qs.GetQuestVarById(0);
            int targetId = env.GetTargetId();
            int dialogActionId = env.GetDialogActionId();

            if (qs.GetStatus() == QuestStatus.START)
            {
                switch (targetId)
                {
                    case 278506: // Tellus
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                if (var == 0)
                                {
                                    return SendQuestDialog(env, 1011);
                                }
                                return false;
                            case DialogAction.SELECT1_1_1:
                                PlayQuestMovie(env, 272);
                                break;
                            case DialogAction.SETPRO1:
                                return DefaultCloseDialog(env, 0, 1); // 1
                        }
                        break;
                    case 279023: // Agemonerk
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                if (var == 1)
                                {
                                    return SendQuestDialog(env, 1352);
                                }
                                return false;
                            case DialogAction.SETPRO2:
                                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 0));
                                player.SetState(CreatureState.FLYING);
                                player.UnsetState(CreatureState.ACTIVE);
                                player.SetFlightTeleportId(57001);
                                PacketSendUtility.BroadcastPacketAndReceive(player, new SM_EMOTION(player, EmotionType.START_FLYTELEPORT, 57001, 0));
                                return DefaultCloseDialog(env, 1, 2); // 2
                        }
                        break;
                    case 278643: // Raithor
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                if (var == 2)
                                {
                                    return SendQuestDialog(env, 1693);
                                }
                                else if (var == 3)
                                {
                                    return SendQuestDialog(env, 2034);
                                }
                                return false;
                            case DialogAction.SETPRO3:
                                if (var == 2)
                                {
                                    Npc raithor = (Npc)env.GetVisibleObject();
                                    Npc attacker1 = (Npc)SpawnForFiveMinutes(214102, raithor.GetWorldMapInstance(), 2344.32f, 1789.96f, 2258.88f, (byte)86);
                                    Npc attacker2 = (Npc)SpawnForFiveMinutes(214102, raithor.GetWorldMapInstance(), 2344.51f, 1786.01f, 2258.88f, (byte)52);
                                    attacker1.GetAggroList().AddHate(raithor, 1);
                                    raithor.GetAggroList().AddHate(attacker1, 1);
                                    attacker2.GetAggroList().AddHate(raithor, 1);
                                    raithor.GetAggroList().AddHate(attacker2, 1);
                                    qs.SetQuestVarById(0, 3); // 3
                                    return CloseDialogWindow(env);
                                }
                                return false;
                            case DialogAction.SETPRO4:
                                if (var == 3)
                                {
                                    qs.SetQuestVarById(0, 12);
                                    qs.SetStatus(QuestStatus.REWARD);
                                    UpdateQuestStatus(env);
                                    PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 0));
                                    player.SetState(CreatureState.FLYING);
                                    player.UnsetState(CreatureState.ACTIVE);
                                    player.SetFlightTeleportId(58001);
                                    PacketSendUtility.BroadcastPacketAndReceive(player, new SM_EMOTION(player, EmotionType.START_FLYTELEPORT, 58001, 0));
                                    return SendQuestSelectionDialog(env);
                                }
                                break;
                        }
                        break;
                }
            }
            else if (qs.GetStatus() == QuestStatus.REWARD)
            {
                if (targetId == 279023) // Agemonerk
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

        public override void OnQuestCompletedEvent(QuestEnv env)
        {
            int[] quests = { 14040, 14041 };
            DefaultOnQuestCompletedEvent(env, quests);
        }

        public override void OnLevelChangedEvent(Player player)
        {
            int[] quests = { 14040, 14041 };
            DefaultOnLevelChangedEvent(player, quests);
        }
    }
}
