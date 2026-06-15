using Aion.GameServer.Model;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
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
    /// @author Enomine
    /// </summary>
    public class _24030ShowdownWithDestiny : AbstractQuestHandler
    {
        private static readonly int[] mobs = { 214591, 798346, 798344, 798342, 798345, 798343 };

        public _24030ShowdownWithDestiny() : base(24030)
        {
        }

        public override void Register()
        {
            int[] npc_ids = { 204206, 204207, 203550, 700551, 205020, 204052 };
            qe.RegisterOnEnterWorld(questId);
            qe.RegisterOnLevelChanged(questId);
            foreach (int npc in mobs)
                qe.RegisterQuestNpc(npc).AddOnKillEvent(questId);
            foreach (int npc_id in npc_ids)
                qe.RegisterQuestNpc(npc_id).AddOnTalkEvent(questId);
        }

        public override bool OnDialogEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            int targetId = env.GetTargetId();
            int dialogActionId = env.GetDialogActionId();
            if (qs == null)
                return false;
            if (qs.GetStatus() == QuestStatus.START)
            {
                int var = qs.GetQuestVarById(0);
                switch (targetId)
                {
                    case 204206:// Cavalorn
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                if (var == 0)
                                    return SendQuestDialog(env, 1011);
                                return false;
                            case DialogAction.SETPRO1:
                                return DefaultCloseDialog(env, 0, 1); // 1
                        }
                        break;
                    case 204207:// Kasir
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                if (var == 1)
                                    return SendQuestDialog(env, 1352);
                                return false;
                            case DialogAction.SETPRO2:
                                TeleportService.TeleportTo(player, WorldMapType.ISHALGEN.GetId(), 386f, 1895.4f, 327.62f, (byte) 60, TeleportAnimation.FADE_OUT_BEAM);
                                return DefaultCloseDialog(env, 1, 2);
                        }
                        break;
                    case 203550:// Munin
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                if (var == 2)
                                    return SendQuestDialog(env, 1693);
                                if (var == 3)
                                    return SendQuestDialog(env, 2034);
                                if (var == 4)
                                    return SendQuestDialog(env, 2375);
                                if (var == 8)
                                    return SendQuestDialog(env, 3739);
                                return false;
                            case DialogAction.SETPRO3:
                                return DefaultCloseDialog(env, 2, 3);
                            case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                                if (var == 3 && player.GetInventory().GetItemCountByItemId(182215391) == 1)
                                {
                                    RemoveQuestItem(env, 182215391, 1);
                                    ChangeQuestStep(env, var, 4);
                                    return SendQuestDialog(env, 10000);
                                }
                                return SendQuestDialog(env, 10001);
                            case DialogAction.SETPRO5:
                                GiveQuestItem(env, workItems[0].GetItemId(), workItems[0].GetCount());
                                GiveQuestItem(env, workItems[1].GetItemId(), workItems[1].GetCount());
                                TeleportService.TeleportTo(player, WorldMapType.RESHANTA.GetId(), 2241f, 2191.5f, 2190.1f, (byte) 0, TeleportAnimation.FADE_OUT_BEAM);
                                return DefaultCloseDialog(env, 4, 5);
                            case DialogAction.SET_SUCCEED:
                                if (var == 8)
                                {
                                    ChangeQuestStep(env, var, var, true);
                                    return SendQuestSelectionDialog(env);
                                }
                                break;
                        }
                        break;
                    case 700551: // Fissure of Destiny
                        if (dialogActionId == DialogAction.USE_OBJECT && var == 5)
                        {
                            WorldMapInstance newInstance = InstanceService.GetNextAvailableInstance(WorldMapType.IDAB_PRO_D3.GetId(), player);
                            TeleportService.TeleportTo(player, newInstance, 52, 174, 229);
                            return true;
                        }
                        break;
                    case 205020:// Hagen
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                return SendQuestDialog(env, 2716);
                            case DialogAction.SETPRO6:
                                Aion.GameServer.SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(18567, player, player);
                                Aion.GameServer.SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(18568, player, player);
                                Aion.GameServer.SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(18569, player, player);
                                Aion.GameServer.SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(18570, player, player);
                                player.SetState(CreatureState.FLYING);
                                player.UnsetState(CreatureState.ACTIVE);
                                player.SetFlightTeleportId(1001);
                                PacketSendUtility.SendPacket(player, new SM_EMOTION(player, EmotionType.START_FLYTELEPORT, 1001, 0));
                                return DefaultCloseDialog(env, 5, 6);
                        }
                        break;
                }
            }
            if (targetId == 204052 && qs.GetStatus() == QuestStatus.REWARD)
            {// Vidar
                switch (dialogActionId)
                {
                    case DialogAction.USE_OBJECT:
                        return SendQuestDialog(env, 10002);
                    default:
                        {
                            return SendQuestEndDialog(env);
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
                int var1 = qs.GetQuestVarById(1);
                if (var == 6)
                {
                    if (var1 != 49)
                        return DefaultOnKillEvent(env, mobs, 0, 49, 1);
                    else if (var1 == 49)
                    {
                        ChangeQuestStep(env, var, 7);
                        Npc mob = (Npc) SpawnInFrontOf(798346, player);
                        mob.GetAggroList().AddHate(player, 100);
                        return true;
                    }
                }
                if (env.GetTargetId() == 798346)
                {
                    qs.SetQuestVar(8);
                    UpdateQuestStatus(env);
                    TeleportService.TeleportTo(player, 220010000, (float) 385, (float) 1895, (float) 327, (byte) 58);
                    return true;
                }
            }
            return false;
        }

        public override bool OnEnterWorldEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            if (player.GetWorldId() != WorldMapType.IDAB_PRO_D3.GetId())
            {
                QuestState qs = player.GetQuestStateList().GetQuestState(questId);
                if (qs != null && qs.GetStatus() == QuestStatus.START)
                {
                    int var = qs.GetQuestVarById(0);
                    if (var == 6 || var == 7)
                    {
                        qs.SetQuestVar(5);
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
