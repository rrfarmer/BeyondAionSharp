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
    /// @author Artur, Pad
    /// </summary>
    public class _14030RetrievedMemory : AbstractQuestHandler
    {
        private static readonly int[] npcs = { 790001, 700551, 205119, 700552, 203700 };
        private static readonly int[] mobs = { 211043, 214578, 215396, 215397, 215398, 215399, 215400 };

        public _14030RetrievedMemory() : base(14030)
        {
        }

        public override void Register()
        {
            qe.RegisterOnLevelChanged(questId);
            qe.RegisterOnEnterWorld(questId);
            foreach (int npc in npcs)
            {
                qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
            }
            foreach (int mob in mobs)
            {
                qe.RegisterQuestNpc(mob).AddOnKillEvent(questId);
            }
        }

        public override bool OnDialogEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            int dialogActionId = env.GetDialogActionId();
            int targetId = env.GetTargetId();

            if (qs == null)
                return false;
            int var = qs.GetQuestVarById(0);

            if (qs.GetStatus() == QuestStatus.START)
            {
                switch (targetId)
                {
                    case 203700: // Fasimedes
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                if (var == 0)
                                {
                                    return SendQuestDialog(env, 1011);
                                }
                                break;
                            case DialogAction.SETPRO1:
                                return DefaultCloseDialog(env, 0, 1); // 1
                        }
                        break;
                    case 790001: // Pernos
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                if (var == 1)
                                {
                                    return SendQuestDialog(env, 1352);
                                }
                                else if (var == 3)
                                {
                                    return SendQuestDialog(env, 2034);
                                }
                                break;
                            case DialogAction.SETPRO2:
                                TeleportService.TeleportTo(player, 210060000, 2012.37f, 438.231f, 126.020f, (byte)7, TeleportAnimation.FADE_OUT_BEAM);
                                return DefaultCloseDialog(env, 1, 2); // 2
                            case DialogAction.SETPRO4:
                                if (!GiveQuestItem(env, 182215387, 1))
                                    return false;
                                TeleportService.TeleportTo(player, WorldMapType.RESHANTA.GetId(), 2241f, 2191.5f, 2190.1f, (byte)0, TeleportAnimation.FADE_OUT_BEAM);
                                return DefaultCloseDialog(env, 3, 4); // 4
                        }
                        break;
                    case 700551: // Fissure of Destiny
                        if (dialogActionId == DialogAction.USE_OBJECT && var == 4)
                        {
                            WorldMapInstance newInstance = InstanceService.GetNextAvailableInstance(WorldMapType.IDAB_PRO_L3.GetId(), player);
                            TeleportService.TeleportTo(player, newInstance, 52, 174, 229);
                            return true;
                        }
                        break;
                    case 205119: // Hermione
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                if (var == 4)
                                {
                                    return SendQuestDialog(env, 2375);
                                }
                                break;
                            case DialogAction.SETPRO5:
                                if (var == 4)
                                {
                                    Aion.GameServer.SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(18563, player, player);
                                    Aion.GameServer.SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(18564, player, player);
                                    Aion.GameServer.SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(18565, player, player);
                                    Aion.GameServer.SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(18566, player, player);
                                    player.SetState(CreatureState.FLYING);
                                    player.UnsetState(CreatureState.ACTIVE);
                                    player.SetFlightTeleportId(1001);
                                    PacketSendUtility.SendPacket(player, new SM_EMOTION(player, EmotionType.START_FLYTELEPORT, 1001, 0));
                                    ChangeQuestStep(env, 4, 5);
                                    return true;
                                }
                                break;
                        }
                        break;
                    case 700552: // Artifact of Memory
                        if (dialogActionId == DialogAction.USE_OBJECT && var == 56)
                        {
                            qs.SetStatus(QuestStatus.REWARD);
                            UpdateQuestStatus(env);
                            TeleportService.TeleportTo(player, 110010000, 1876.29f, 1511f, 812.675f, (byte)60, TeleportAnimation.FADE_OUT_BEAM);
                            return UseQuestObject(env, 56, 56, false, 0, 0, 0, 182215387, 1, 0, false); // 56
                        }
                        break;
                }
            }
            else if (qs.GetStatus() == QuestStatus.REWARD)
            {
                if (targetId == 203700) // Fasimedes
                {
                    switch (dialogActionId)
                    {
                        case DialogAction.USE_OBJECT:
                            return SendQuestDialog(env, 3739);
                        case DialogAction.SELECT_QUEST_REWARD:
                            return SendQuestDialog(env, 5);
                        default:
                            {
                                return SendQuestEndDialog(env);
                            }
                    }
                }
            }
            return false;
        }

        public override bool OnKillEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);

            if (qs != null && qs.GetStatus() == QuestStatus.START)
            {
                int var = qs.GetQuestVarById(0);
                if (var >= 2 && var < 55)
                {
                    int[] npcIds = { 215396, 215397, 215398, 215399, 211043, 214578 };
                    if (var == 2)
                        return DefaultOnKillEvent(env, 214578, 2, 3); // 3
                    if (var == 54)
                        Spawn(215400, player, 240f, 257f, 208.53946f, (byte)68);
                    return DefaultOnKillEvent(env, npcIds, 2, 55); // 2 - 55
                }
                else if (var == 55)
                {
                    return DefaultOnKillEvent(env, 215400, 55, 56); // 56
                }
            }
            return false;
        }

        public override bool OnEnterWorldEvent(QuestEnv env)
        {
            if (env.GetPlayer().GetWorldId() != WorldMapType.IDAB_PRO_L3.GetId())
            {
                QuestState qs = env.GetPlayer().GetQuestStateList().GetQuestState(questId);

                if (qs != null && qs.GetStatus() == QuestStatus.START)
                {
                    int var0 = qs.GetQuestVarById(0);
                    if (var0 > 4 && var0 <= 56)
                    {
                        qs.SetQuestVarById(0, 4);
                        UpdateQuestStatus(env);
                        return true;
                    }
                }
            }
            return false;
        }

        public override void OnLevelChangedEvent(Player player)
        {
            DefaultOnLevelChangedEvent(player);
        }
    }
}
