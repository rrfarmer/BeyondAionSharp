using System.Collections.Generic;
using System.Threading.Tasks;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Instance;
using Aion.GameServer.Services.Reward;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Handlers.Quest
{
    /// <summary>
    /// Talk with Pernos (790001). Go to the island at the center of Cliona Lake (CLIONA_LAKE_210010000) and fill up the bottle Pernos gave you
    /// (182200007). Meet Daminu (730008) and obtain Daminu's Essence (182200009). Talk with Pernos. Explore your lost past (310020000, 52, 174, 229).
    /// Advance on Karamatis (Belpartan, 205000). Defeat Raiders (211042) (4). Defeat Orissan (211043). Talk with Pernos and choose the path you will take.
    ///
    /// @author MrPoke, vlog
    /// </summary>
    public class _1006Ascension : AbstractQuestHandler
    {
        public _1006Ascension() : base(1006)
        {
        }

        public override void Register()
        {
            if (CustomConfig.ENABLE_SIMPLE_2NDCLASS)
                return;
            int[] mobs = { 211042, 211043 };
            int[] npcs = { 790001, 730008, 205000 };
            qe.RegisterOnLevelChanged(questId);
            qe.RegisterOnQuestCompleted(questId);
            foreach (int mob in mobs)
            {
                qe.RegisterQuestNpc(mob).AddOnKillEvent(questId);
            }
            foreach (int npc in npcs)
            {
                qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
            }
            qe.RegisterQuestItem(182200007, questId);
            qe.RegisterOnEnterWorld(questId);
            qe.RegisterOnDie(questId);
        }

        public override bool OnDialogEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs == null)
                return false;
            int dialogActionId = env.GetDialogActionId();
            int var = qs.GetQuestVarById(0);
            int targetId = 0;
            if (env.GetVisibleObject() is Npc)
                targetId = ((Npc)env.GetVisibleObject()).GetNpcId();

            if (qs.GetStatus() == QuestStatus.START)
            {
                switch (targetId)
                {
                    case 790001: // Pernos
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                if (var == 0)
                                    return SendQuestDialog(env, 1011);
                                else if (var == 3)
                                    return SendQuestDialog(env, 1693);
                                else if (var == 5)
                                    return SendQuestDialog(env, 2034);
                                return false;
                            case DialogAction.SETPRO1:
                                if (player.GetInventory().GetItemCountByItemId(182200007) == 0)
                                    GiveQuestItem(env, 182200007, 1);
                                qs.SetQuestVar(1);
                                UpdateQuestStatus(env);
                                TeleportService.TeleportTo(player, 210010000, 657f, 1071f, 99.375f, (byte)72, TeleportAnimation.FADE_OUT_BEAM);
                                return true;
                            case DialogAction.SETPRO3:
                                WorldMapInstance newInstance = InstanceService.GetNextAvailableInstance(WorldMapType.KARAMATIS_B.GetId(), player);
                                TeleportService.TeleportTo(player, newInstance, 52, 174, 229, (byte)10, TeleportAnimation.NONE);
                                qs.SetQuestVar(99); // 99
                                UpdateQuestStatus(env);
                                RemoveQuestItem(env, 182200009, 1);
                                return CloseDialogWindow(env);
                            case DialogAction.SETPRO4:
                                int dialogPageId = ClassChangeService.GetClassSelectionDialogPageId(player.GetRace(), player.GetPlayerClass());
                                if (var == 5 && dialogPageId != 0)
                                    return SendQuestDialog(env, dialogPageId);
                                return false;
                            case DialogAction.SETPRO5:
                                return var == 5 && SetPlayerClass(env, qs, PlayerClass.GLADIATOR);
                            case DialogAction.SETPRO6:
                                return var == 5 && SetPlayerClass(env, qs, PlayerClass.TEMPLAR);
                            case DialogAction.SETPRO7:
                                return var == 5 && SetPlayerClass(env, qs, PlayerClass.ASSASSIN);
                            case DialogAction.SETPRO8:
                                return var == 5 && SetPlayerClass(env, qs, PlayerClass.RANGER);
                            case DialogAction.SETPRO9:
                                return var == 5 && SetPlayerClass(env, qs, PlayerClass.SORCERER);
                            case DialogAction.SETPRO10:
                                return var == 5 && SetPlayerClass(env, qs, PlayerClass.SPIRIT_MASTER);
                            case DialogAction.SETPRO11:
                                return var == 5 && SetPlayerClass(env, qs, PlayerClass.CLERIC);
                            case DialogAction.SETPRO12:
                                return var == 5 && SetPlayerClass(env, qs, PlayerClass.CHANTER);
                            case DialogAction.SETPRO13:
                                return var == 5 && SetPlayerClass(env, qs, PlayerClass.GUNNER);
                            case DialogAction.SETPRO14:
                                return var == 5 && SetPlayerClass(env, qs, PlayerClass.BARD);
                            case DialogAction.SETPRO15:
                                return var == 5 && SetPlayerClass(env, qs, PlayerClass.RIDER);
                        }
                        break;
                    case 730008: // Daminu
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                if (var == 2 && player.GetInventory().GetItemCountByItemId(182200008) >= 1)
                                    return SendQuestDialog(env, 1352);
                                return false;
                            case DialogAction.SELECT2_1:
                                PlayQuestMovie(env, 14);
                                return SendQuestDialog(env, 1353);
                            case DialogAction.SETPRO2:
                                if (var == 2)
                                {
                                    RemoveQuestItem(env, 182200008, 1);
                                    GiveQuestItem(env, 182200009, 1);
                                    qs.SetQuestVar(3);
                                    UpdateQuestStatus(env);
                                    TeleportService.TeleportTo(player, 210010000, 246f, 1639f, 100.316f, (byte)56, TeleportAnimation.FADE_OUT_BEAM);
                                    return true;
                                }
                                break;
                        }
                        break;
                    case 205000: // Belpartan
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                if (qs.GetQuestVars().GetQuestVars() == 99)
                                {
                                    Aion.GameServer.SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(281, player, player);
                                    player.SetState(CreatureState.FLYING);
                                    player.UnsetState(CreatureState.ACTIVE);
                                    player.SetFlightTeleportId(1001);
                                    PacketSendUtility.SendPacket(player, new SM_EMOTION(player, EmotionType.START_FLYTELEPORT, 1001, 0));
                                    qs.SetQuestVar(50); // 50
                                    UpdateQuestStatus(env);
                                    ThreadPoolManager.GetInstance().Schedule(ct =>
                                    {
                                        qs.SetQuestVar(51);
                                        UpdateQuestStatus(env);
                                        List<Npc> mobs = new List<Npc>();
                                        mobs.Add((Npc)Spawn(211042, player, 224.073f, 239.1f, 206.7f, (byte)0));
                                        mobs.Add((Npc)Spawn(211042, player, 233.5f, 241.04f, 206.365f, (byte)0));
                                        mobs.Add((Npc)Spawn(211042, player, 229.6f, 265.7f, 205.7f, (byte)0));
                                        mobs.Add((Npc)Spawn(211042, player, 222.8f, 262.5f, 205.7f, (byte)0));
                                        foreach (Npc mob in mobs)
                                        {
                                            mob.GetAggroList().AddHate(player, 1000);
                                        }
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
                if (targetId == 790001) // Pernos
                {
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.SELECTED_QUEST_NOREWARD:
                            if (player.GetWorldId() == 310020000)
                                TeleportService.TeleportTo(player, 210010000, 245.14868f, 1639.1372f, 100.35713f, (byte)60, TeleportAnimation.FADE_OUT_BEAM);
                            break;
                    }
                    return SendQuestEndDialog(env); // finishes quest or shows reward selection
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
                if (player.IsInsideItemUseZone(ZoneName.Get("LF1_ITEMUSEAREA_Q1006")))
                {
                    int var = qs.GetQuestVarById(0);
                    if (var == 1)
                    {
                        return HandlerResultExtensions.FromBoolean(UseQuestItem(env, item, 1, 2, false, 182200008, 1, 0)); // 2
                    }
                }
            }
            return HandlerResult.SUCCESS; // ??
        }

        public override bool OnKillEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs != null && qs.GetStatus() == QuestStatus.START)
            {
                int var = qs.GetQuestVarById(0);
                int targetId = env.GetTargetId();
                if (targetId == 211042)
                {
                    env.GetVisibleObject().GetController().Delete();
                    if (var >= 51 && var < 54)
                    {
                        return DefaultOnKillEvent(env, 211042, 51, 54); // 52 - 54
                    }
                    else if (var == 54)
                    {
                        qs.SetQuestVar(4); // 4
                        UpdateQuestStatus(env);
                        Npc mob = (Npc)Spawn(211043, player, 226.7f, 251.5f, 205.5f, (byte)0);
                        mob.GetAggroList().AddHate(player, 1000);
                        return true;
                    }
                }
                else if (targetId == 211043 && var == 4)
                {
                    PlayQuestMovie(env, 151);
                    player.GetWorldMapInstance().ForEachNpc(npc => npc.GetController().Delete());
                    Spawn(790001, player, 220.6f, 247.8f, 206.0f, (byte)0);
                    qs.SetQuestVar(5); // 5
                    UpdateQuestStatus(env);
                }
            }
            return false;
        }

        private bool SetPlayerClass(QuestEnv env, QuestState qs, PlayerClass playerClass)
        {
            if (ClassChangeService.SetClass(env.GetPlayer(), playerClass))
            {
                ChangeQuestStep(env, 5, 5, true); // reward
                return SendQuestDialog(env, 5);
            }
            return false;
        }

        public override bool OnDieEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs != null && qs.GetStatus() == QuestStatus.START && player.GetWorldId() == 310020000)
            {
                int var = qs.GetQuestVars().GetQuestVars();
                if (var > 3)
                    ChangeQuestStep(env, var, 3);
            }
            return false;
        }

        public override bool OnEnterWorldEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs != null && qs.GetStatus() == QuestStatus.START)
            {
                int var = qs.GetQuestVars().GetQuestVars();
                if (player.GetWorldId() == 310020000)
                    PacketSendUtility.SendPacket(player, new SM_ASCENSION_MORPH(1));
                else if (var > 3 && var != 5) // 5 is class selection, quest should not reset anymore after you killed orissan
                    ChangeQuestStep(env, var, 3);
            }
            return false;
        }

        public override void OnLevelChangedEvent(Player player)
        {
            DefaultOnLevelChangedEvent(player);
        }

        public override void OnQuestCompletedEvent(QuestEnv env)
        {
            if (env.GetQuestId() == questId)
            {
                Player player = env.GetPlayer();
                player.GetCommonData().UpdateDaeva();
                if (WebRewardService.MaxLevelReward.IsPendingAscension(player))
                    WebRewardService.MaxLevelReward.Reward(player);
            }
        }
    }
}
