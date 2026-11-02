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
            // D29, adopted by upstream db5ae0453: the teleport devices 730888 and 730898 carry the var 10 -> 11 -> REWARD steps.
            int[] npc_ids = { 203700, 801216, 790001, 203183, 203989, 730888, 730898 };
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
                                return DefaultCloseDialog(env, 0, 1);
                        }
                        break;
                    case 801216:// Losthes
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                return SendQuestDialog(env, 1352);
                            case DialogAction.SETPRO2:
                                return DefaultCloseDialog(env, 1, 2);
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
                                return DefaultCloseDialog(env, 2, 3);
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
                                return DefaultCloseDialog(env, 4, 5);
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
                                return DefaultCloseDialog(env, 6, 7);
                            case DialogAction.SETPRO9:
                                WorldMapInstance newInstance = InstanceService.GetNextAvailableInstance(WorldMapType.NIDALBER.GetId(), player);
                                TeleportService.TeleportTo(player, newInstance, 274.6711f, 168.1845f, 204.3471f, (byte) 35, TeleportAnimation.FADE_OUT_BEAM);
                                return DefaultCloseDialog(env, 8, 9);
                        }
                        break;
                    case 730888: // Large Teleporter
                        if (dialogActionId == DialogAction.QUEST_SELECT && ChangeQuestStep(env, 10, 11))
                        {
                            PlayQuestMovie(env, 888);
                            env.GetVisibleObject().GetController().Delete();
                            Spawn(730898, player, 257, 257, 226.35f, (byte) 95); // Broken Large Teleporter
                            return true;
                        }
                        break;
                    case 730898: // Broken Large Teleporter
                        if (dialogActionId == DialogAction.QUEST_SELECT && DefaultCloseDialog(env, 11, 11, true, false))
                        {
                            TeleportService.TeleportTo(player, 110010000, 1876.29f, 1511f, 812.675f, (byte) 0, TeleportAnimation.FADE_OUT_BEAM);
                            return true;
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
                if (player.IsInsideItemUseZone("LF1_ITEMUSEAREA_Q14031"))
                {
                    if (var == 3)
                    {
                        PlayQuestMovie(env, 21);
                        return HandlerResultExtensions.FromBoolean(UseQuestItem(env, item, 3, 4, false));// 3-4
                    }
                }
                if (player.IsInsideItemUseZone("LF1A_ITEMUSEAREA_Q14031"))
                {
                    if (var == 5)
                    {
                        return HandlerResultExtensions.FromBoolean(UseQuestItem(env, item, 5, 6, false));// 5-6
                    }
                }
                if (player.IsInsideItemUseZone("LF2_ITEMUSEAREA_Q14031"))
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
            if (player.GetWorldId() != 320040000 || player.GetWorldMapInstance().GetNpcs(730888, 730898).Count > 0)
                return false;
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs == null || qs.GetStatus() != QuestStatus.START)
                return false;
            int var = qs.GetQuestVars().GetQuestVars();
            if (var == 9 || var == 10)
                Spawn(730888, player, 257, 257, 226.35f, (byte) 95); // Large Teleporter
            else if (var == 11)
                Spawn(730898, player, 257, 257, 226.35f, (byte) 95); // Broken Large Teleporter
            if (var == 9)
            {
                // Captain Tarbana
                Spawn(233878, player, 262.9f, 224.5f, 211.348f, (byte) 95);
                // 5x Baranath Sentinel
                Spawn(233886, player, 217.015f, 221.694f, 207.49455f, (byte) 97);
                Spawn(233886, player, 239.732f, 211.250f, 209.19f, (byte) 97);
                Spawn(233886, player, 257.065f, 204.49f, 209.094f, (byte) 97);
                Spawn(233886, player, 274.899f, 199.398f, 208.83487f, (byte) 97);
                Spawn(233886, player, 282.878f, 223.742f, 208.252f, (byte) 97);
            }
            return true;
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
