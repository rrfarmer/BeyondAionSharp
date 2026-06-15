using Aion.GameServer.Model;
using Aion.GameServer.Model.Animations;
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
    /// @author Artur
    /// </summary>
    public class _14031AHyperVention : AbstractQuestHandler
    {
        public _14031AHyperVention() : base(14031)
        {
        }

        public override void Register()
        {
            int[] npc_ids = { 203700, 801216, 790001, 203183, 203989, };
            qe.RegisterOnQuestCompleted(questId);
            qe.RegisterOnLevelChanged(questId);
            qe.RegisterOnEnterWorld(questId);
            qe.RegisterQuestItem(182215388, questId);
            qe.RegisterQuestItem(182215389, questId);
            qe.RegisterQuestItem(182215390, questId);
            qe.RegisterQuestNpc(233878).AddOnKillEvent(questId);
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
                    case 203700:// Fasimedes
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                return SendQuestDialog(env, 1011);
                            case DialogAction.SETPRO1:
                                qs.SetQuestVar(1);
                                UpdateQuestStatus(env);
                                return CloseDialogWindow(env);
                        }
                        break;
                    case 801216:// Losthes
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                return SendQuestDialog(env, 1352);
                            case DialogAction.SETPRO2:
                                qs.SetQuestVar(2);
                                UpdateQuestStatus(env);
                                return CloseDialogWindow(env);
                        }
                        break;
                    case 790001:// Pernos
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                return SendQuestDialog(env, 1693);
                            case DialogAction.SETPRO3:
                                if (!GiveQuestItem(env, 182215388, 1))
                                    return true;
                                qs.SetQuestVar(3);
                                UpdateQuestStatus(env);
                                return CloseDialogWindow(env);
                        }
                        break;
                    case 203183:// Khidia
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                return SendQuestDialog(env, 2375);
                            case DialogAction.SETPRO5:
                                if (!GiveQuestItem(env, 182215389, 1))
                                    return true;
                                qs.SetQuestVar(5);
                                UpdateQuestStatus(env);
                                return CloseDialogWindow(env);
                        }
                        break;
                    case 203989:// Tumblusen
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                if (var == 6)
                                {
                                    return SendQuestDialog(env, 3057);
                                }
                                if (var == 8)
                                {
                                    return SendQuestDialog(env, 3739);
                                }
                                return false;
                            case DialogAction.SETPRO7:
                                if (!GiveQuestItem(env, 182215390, 1))
                                    return true;
                                qs.SetQuestVar(7);
                                UpdateQuestStatus(env);
                                return CloseDialogWindow(env);
                            case DialogAction.SETPRO9:
                                qs.SetQuestVar(9);
                                UpdateQuestStatus(env);
                                WorldMapInstance newInstance = InstanceService.GetNextAvailableInstance(WorldMapType.NIDALBER.GetId(), player);
                                TeleportService.TeleportTo(player, newInstance, 274, 167, 204);
                                return CloseDialogWindow(env);
                        }
                        break;
                    case 730888: // Large Teleporter
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                env.GetVisibleObject().GetController().Delete();
                                qs.SetQuestVar(11);
                                UpdateQuestStatus(env);
                                PlayQuestMovie(env, 888);
                                Spawn(730898, player, 257, 257, (float) 226.35, (byte) 95); // Broken Teleporter Device
                                break;
                        }
                        break;
                    case 730898: // Broken Large Teleporter
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                env.GetVisibleObject().GetController().Delete();
                                qs.SetStatus(QuestStatus.REWARD);
                                UpdateQuestStatus(env);
                                TeleportService.TeleportTo(player, 110010000, 1876.29f, 1511f, 812.675f, (byte) 60, TeleportAnimation.FADE_OUT_BEAM);
                                break;
                        }
                        break;
                }
            }
            if (qs.GetStatus() == QuestStatus.REWARD)
            {
                if (targetId == 203700)// Fasimedes
                    switch (dialogActionId)
                    {
                        case DialogAction.USE_OBJECT:
                            return SendQuestDialog(env, 4083);
                        case DialogAction.SELECT_QUEST_REWARD:
                            return SendQuestDialog(env, 5);
                        default:
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
            int var = qs.GetQuestVarById(0);
            if (qs.GetStatus() == QuestStatus.START)
            {
                if (player.IsInsideItemUseZone(ZoneName.Get("LF1_ITEMUSEAREA_Q14031")))
                {
                    if (var == 3)
                    {
                        PlayQuestMovie(env, 21);
                        return HandlerResultExtensions.FromBoolean(UseQuestItem(env, item, 3, 4, false));// 3-4
                    }
                }
                if (player.IsInsideItemUseZone(ZoneName.Get("LF1A_ITEMUSEAREA_Q14031")))
                {
                    if (var == 5)
                    {
                        return HandlerResultExtensions.FromBoolean(UseQuestItem(env, item, 5, 6, false));// 5-6
                    }
                }
                if (player.IsInsideItemUseZone(ZoneName.Get("LF2_ITEMUSEAREA_Q14031")))
                {
                    if (var == 7)
                    {
                        return HandlerResultExtensions.FromBoolean(UseQuestItem(env, item, 7, 8, false));// 7-8
                    }
                }
            }
            return HandlerResult.SUCCESS;
        }

        public override bool OnEnterWorldEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs != null && qs.GetStatus() == QuestStatus.START)
            {
                int var = qs.GetQuestVars().GetQuestVars();
                if (var == 9 && player.GetWorldId() == 320040000)
                {
                    // Shattered Large Teleporter
                    Spawn(730888, player, 257, 257, (float) 226.35, (byte) 95);
                    // Captain Tarbana
                    Spawn(233878, player, (float) 262.9, (float) 224.5, (float) 211.348, (byte) 95);
                    // 5x Baranath Sentinel
                    Spawn(233886, player, (float) 217.015, (float) 221.694, (float) 207.49455, (byte) 97);
                    Spawn(233886, player, (float) 239.732, (float) 211.250, (float) 209.19, (byte) 97);
                    Spawn(233886, player, (float) 257.065, (float) 204.49, (float) 209.094, (byte) 97);
                    Spawn(233886, player, (float) 274.899, (float) 199.398, (float) 208.83487, (byte) 97);
                    Spawn(233886, player, (float) 282.878, (float) 223.742, (float) 208.252, (byte) 97);
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
                if (env.GetTargetId() == 233878)
                    env.GetVisibleObject().GetController().Delete();
                return DefaultOnKillEvent(env, 233878, 9, 10);
            }
            return false;
        }

        public override void OnLevelChangedEvent(Player player)
        {
            DefaultOnLevelChangedEvent(player, 14030);
        }

        public override void OnQuestCompletedEvent(QuestEnv env)
        {
            DefaultOnQuestCompletedEvent(env, 14030);
        }
    }
}
