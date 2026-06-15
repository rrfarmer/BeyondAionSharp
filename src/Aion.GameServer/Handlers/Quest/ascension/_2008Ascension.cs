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

namespace Aion.GameServer.Handlers.Quest
{
    /// <summary>
    /// @author MrPoke
    /// </summary>
    public class _2008Ascension : AbstractQuestHandler
    {
        public _2008Ascension() : base(2008)
        {
        }

        public override void Register()
        {
            if (CustomConfig.ENABLE_SIMPLE_2NDCLASS)
                return;
            qe.RegisterOnLevelChanged(questId);
            qe.RegisterOnQuestCompleted(questId);
            qe.RegisterQuestNpc(203550).AddOnTalkEvent(questId);
            qe.RegisterQuestNpc(790003).AddOnTalkEvent(questId);
            qe.RegisterQuestNpc(790002).AddOnTalkEvent(questId);
            qe.RegisterQuestNpc(203546).AddOnTalkEvent(questId);
            qe.RegisterQuestNpc(205020).AddOnTalkEvent(questId);
            qe.RegisterQuestNpc(205040).AddOnKillEvent(questId);
            qe.RegisterQuestNpc(205041).AddOnKillEvent(questId);
            qe.RegisterOnEnterWorld(questId);
            qe.RegisterOnDie(questId);
        }

        public override bool OnKillEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs == null || qs.GetStatus() != QuestStatus.START)
                return false;

            int var = qs.GetQuestVarById(0);
            int targetId = env.GetTargetId();
            if (targetId == 205040) // Guardian Assassin
            {
                env.GetVisibleObject().GetController().Delete();
                if (var >= 51 && var <= 53)
                {
                    qs.SetQuestVar(qs.GetQuestVars().GetQuestVars() + 1);
                    UpdateQuestStatus(env);
                    return true;
                }
                else if (var == 54)
                {
                    qs.SetQuestVar(5);
                    UpdateQuestStatus(env);
                    Npc mob = (Npc)Spawn(205041, player, 301f, 259f, 205.5f, (byte)0);
                    mob.GetAggroList().AddHate(player, 1000);
                    return true;
                }
            }
            else if (targetId == 205041 && var == 5)
            {
                PlayQuestMovie(env, 152);
                player.GetWorldMapInstance().ForEachNpc(npc => npc.GetController().Delete());
                Spawn(203550, player, 301.92999f, 274.26001f, 205.7f, (byte)0);
                qs.SetQuestVar(6);
                UpdateQuestStatus(env);
                return true;
            }
            return false;
        }

        public override bool OnDialogEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs == null)
                return false;

            int var = qs.GetQuestVars().GetQuestVars();
            int targetId = env.GetTargetId();

            if (qs.GetStatus() == QuestStatus.START)
            {
                if (targetId == 203550)
                {
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 0)
                                return SendQuestDialog(env, 1011);
                            else if (var == 4)
                                return SendQuestDialog(env, 2375);
                            else if (var == 6)
                                return SendQuestDialog(env, 2716);
                            return false;
                        case DialogAction.SELECT5_1:
                            if (var == 4)
                            {
                                PlayQuestMovie(env, 57);
                                RemoveQuestItem(env, 182203009, 1);
                                RemoveQuestItem(env, 182203010, 1);
                                RemoveQuestItem(env, 182203011, 1);
                            }
                            return false;
                        case DialogAction.SETPRO1:
                            qs.SetQuestVar(1);
                            UpdateQuestStatus(env);
                            TeleportService.TeleportTo(player, 220010000, 585.5074f, 2416.0312f, 278.625f, (byte)102, TeleportAnimation.FADE_OUT_BEAM);
                            return true;
                        case DialogAction.SETPRO5:
                            if (var == 4)
                            {
                                qs.SetQuestVar(99);
                                UpdateQuestStatus(env);
                                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 0));
                                // Create instance
                                WorldMapInstance newInstance = InstanceService.GetNextAvailableInstance(WorldMapType.ATAXIAR_B.GetId(), player);
                                TeleportService.TeleportTo(player, newInstance, 457.65f, 426.8f, 230.4f);
                                return true;
                            }
                            return false;
                        case DialogAction.SETPRO6:
                            int dialogPageId = ClassChangeService.GetClassSelectionDialogPageId(player.GetRace(), player.GetPlayerClass());
                            if (var == 6 && dialogPageId != 0)
                                return SendQuestDialog(env, dialogPageId);
                            return false;
                        case DialogAction.SETPRO7:
                            return var == 6 && SetPlayerClass(env, qs, PlayerClass.GLADIATOR);
                        case DialogAction.SETPRO8:
                            return var == 6 && SetPlayerClass(env, qs, PlayerClass.TEMPLAR);
                        case DialogAction.SETPRO9:
                            return var == 6 && SetPlayerClass(env, qs, PlayerClass.ASSASSIN);
                        case DialogAction.SETPRO10:
                            return var == 6 && SetPlayerClass(env, qs, PlayerClass.RANGER);
                        case DialogAction.SETPRO11:
                            return var == 6 && SetPlayerClass(env, qs, PlayerClass.SORCERER);
                        case DialogAction.SETPRO12:
                            return var == 6 && SetPlayerClass(env, qs, PlayerClass.SPIRIT_MASTER);
                        case DialogAction.SETPRO13:
                            return var == 6 && SetPlayerClass(env, qs, PlayerClass.CHANTER);
                        case DialogAction.SETPRO14:
                            return var == 6 && SetPlayerClass(env, qs, PlayerClass.CLERIC);
                        case DialogAction.SETPRO15:
                            return var == 6 && SetPlayerClass(env, qs, PlayerClass.GUNNER);
                        case DialogAction.SETPRO16:
                            return var == 6 && SetPlayerClass(env, qs, PlayerClass.BARD);
                        case DialogAction.SETPRO17:
                            return var == 6 && SetPlayerClass(env, qs, PlayerClass.RIDER);
                    }
                }
                else if (targetId == 790003)
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
                                if (player.GetInventory().GetItemCountByItemId(182203009) == 0)
                                    GiveQuestItem(env, 182203009, 1);
                                qs.SetQuestVar(2);
                                UpdateQuestStatus(env);
                                TeleportService.TeleportTo(player, 220010000, 940.74475f, 2295.5305f, 265.65674f, (byte)46, TeleportAnimation.FADE_OUT_BEAM);
                                return true;
                            }
                            return false;
                    }
                }
                else if (targetId == 790002)
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
                                if (player.GetInventory().GetItemCountByItemId(182203010) == 0)
                                    GiveQuestItem(env, 182203010, 1);
                                qs.SetQuestVar(3);
                                UpdateQuestStatus(env);
                                TeleportService.TeleportTo(player, 220010000, 1111.5637f, 1719.2745f, 270.114256f, (byte)114, TeleportAnimation.FADE_OUT_BEAM);
                                return true;
                            }
                            return false;
                    }
                }
                else if (targetId == 203546)
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
                                if (player.GetInventory().GetItemCountByItemId(182203011) == 0)
                                    GiveQuestItem(env, 182203011, 1);
                                qs.SetQuestVar(4);
                                UpdateQuestStatus(env);
                                TeleportService.TeleportTo(player, 220010000, 383.10248f, 1895.3093f, 327.625f, (byte)59, TeleportAnimation.FADE_OUT_BEAM);
                                return true;
                            }
                            return false;
                    }
                }
                else if (targetId == 205020)
                {
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 99)
                            {
                                Aion.GameServer.SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(257, player, player);
                                player.SetState(CreatureState.FLYING);
                                player.UnsetState(CreatureState.ACTIVE);
                                player.SetFlightTeleportId(3001);
                                PacketSendUtility.SendPacket(player, new SM_EMOTION(player, EmotionType.START_FLYTELEPORT, 3001, 0));
                                qs.SetQuestVar(50);
                                UpdateQuestStatus(env);
                                ThreadPoolManager.GetInstance().Schedule(ct =>
                                {
                                    qs.SetQuestVar(51);
                                    UpdateQuestStatus(env);
                                    List<Npc> mobs = new List<Npc>();
                                    mobs.Add((Npc)Spawn(205040, player, 294f, 277f, 207f, (byte)0));
                                    mobs.Add((Npc)Spawn(205040, player, 305f, 279f, 206.5f, (byte)0));
                                    mobs.Add((Npc)Spawn(205040, player, 298f, 253f, 205.7f, (byte)0));
                                    mobs.Add((Npc)Spawn(205040, player, 306f, 251f, 206f, (byte)0));
                                    foreach (Npc mob in mobs)
                                    {
                                        mob.GetAggroList().AddHate(player, 1000);
                                    }
                                    return ValueTask.CompletedTask;
                                }, 43000L);
                                return true;
                            }
                            return false;
                    }
                }
            }
            else if (qs.GetStatus() == QuestStatus.REWARD)
            {
                if (targetId == 203550)
                {
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.SELECTED_QUEST_NOREWARD:
                            if (player.GetWorldId() == 320020000)
                            {
                                TeleportService.TeleportTo(player, 220010000, 386.03476f, 1893.9309f, 327.62283f, (byte)59, TeleportAnimation.FADE_OUT_BEAM);
                            }
                            break;
                    }
                    return SendQuestEndDialog(env); // finishes quest or shows reward selection
                }
            }
            return false;
        }

        public override void OnLevelChangedEvent(Player player)
        {
            DefaultOnLevelChangedEvent(player);
        }

        public override bool OnEnterWorldEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs != null && qs.GetStatus() == QuestStatus.START)
            {
                int var = qs.GetQuestVars().GetQuestVars();
                if (player.GetWorldId() == 320020000)
                    PacketSendUtility.SendPacket(player, new SM_ASCENSION_MORPH(1));
                else if (var > 4 && var != 6) // 6 is class selection, quest should not reset anymore after you killed hellion
                    ChangeQuestStep(env, var, 4);
            }
            return false;
        }

        private bool SetPlayerClass(QuestEnv env, QuestState qs, PlayerClass playerClass)
        {
            if (ClassChangeService.SetClass(env.GetPlayer(), playerClass))
            {
                ChangeQuestStep(env, 6, 6, true); // reward
                return SendQuestDialog(env, 5);
            }
            return false;
        }

        public override bool OnDieEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs != null && qs.GetStatus() == QuestStatus.START && player.GetWorldId() == 320020000)
            {
                int var = qs.GetQuestVars().GetQuestVars();
                if (var > 4)
                    ChangeQuestStep(env, var, 4);
            }
            return false;
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
