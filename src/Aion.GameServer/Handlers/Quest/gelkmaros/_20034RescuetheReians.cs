using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
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
    /// @author Majka
    /// </summary>
    public class _20034RescuetheReians : AbstractQuestHandler
    {
        public _20034RescuetheReians() : base(20034)
        {
        }

        public override void Register()
        {
            // Rebirthing Chamber Exit ID: 700706
            // Destroyed Gargoyle ID: 730243
            // Ortiz ID: 799295
            // Fjoelnir ID: 799297
            // Anilmo ID: 799341
            // Brainwashed Reian ID: 799513 (var1)
            // Brainwashed Reian ID: 799514 (var2)
            // Brainwashed Reian ID: 799515 (var3)
            // Brainwashed Reian ID: 799516 (var4)
            int[] npcs = { 730243, 799295, 799297, 799341, 799513, 799514, 799515, 799516 };
            qe.RegisterOnDie(questId);
            qe.RegisterOnLogOut(questId);
            qe.RegisterOnLevelChanged(questId);
            qe.RegisterQuestNpc(216592).AddOnKillEvent(questId);
            qe.RegisterOnEnterWorld(questId);
            qe.RegisterOnQuestCompleted(questId);
            foreach (int npc in npcs)
            {
                qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
            }
        }

        public override bool OnEnterWorldEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs != null && qs.GetStatus() == QuestStatus.START)
            {
                int var = qs.GetQuestVarById(0);
                if (var == 2)
                {
                    if (player.GetWorldId() == 300150000)
                    {
                        qs.SetQuestVar(3);
                        UpdateQuestStatus(env);
                    }
                }
                else if (var > 2 && var < 6)
                {
                    if (player.GetWorldId() != 300150000)
                    {
                        PacketSendUtility.SendPacket(player,
                            SM_SYSTEM_MESSAGE.STR_QUEST_SYSTEMMSG_GIVEUP_QUEST(DataManager.QUEST_DATA.GetQuestById(questId).GetName()));
                        qs.SetQuestVar(2);
                        UpdateQuestStatus(env);
                    }
                }
                else if (var == 6)
                {
                    if (player.GetWorldId() == 220070000)
                    {
                        qs.SetQuestVar(7);
                        qs.SetStatus(QuestStatus.REWARD);
                        UpdateQuestStatus(env);
                    }
                }
            }
            return false;
        }

        public override bool OnDieEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs == null || qs.GetStatus() != QuestStatus.START)
            {
                return false;
            }
            int var = qs.GetQuestVars().GetQuestVars();
            if (var >= 3 && var < 6)
            {
                qs.SetQuestVar(2);
                UpdateQuestStatus(env);
                PacketSendUtility.SendPacket(player,
                    SM_SYSTEM_MESSAGE.STR_QUEST_SYSTEMMSG_GIVEUP_QUEST(DataManager.QUEST_DATA.GetQuestById(questId).GetName()));
            }

            return false;
        }

        public override bool OnLogOutEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs != null && qs.GetStatus() == QuestStatus.START)
            {
                int var = qs.GetQuestVarById(0);
                if (var >= 3 && var < 6)
                {
                    PacketSendUtility.SendPacket(player,
                        SM_SYSTEM_MESSAGE.STR_QUEST_SYSTEMMSG_GIVEUP_QUEST(DataManager.QUEST_DATA.GetQuestById(questId).GetName()));
                }
            }
            return false;
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
            // int var1 = qs.getQuestVarById(1);
            int targetId = env.GetTargetId();
            int dialogActionId = env.GetDialogActionId();

            Npc npc = (Npc) player.GetTarget();
            if (qs.GetStatus() == QuestStatus.REWARD)
            {
                if (targetId == 799295)
                { // Ortiz
                    if (dialogActionId == DialogAction.USE_OBJECT)
                    {
                        return SendQuestDialog(env, 10002);
                    }
                    return SendQuestEndDialog(env);
                }
                return false;
            }
            else if (qs.GetStatus() != QuestStatus.START)
            {
                return false;
            }
            if (targetId == 799297)
            { // Fjoelnir
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 0)
                        {
                            return SendQuestDialog(env, 1011);
                        }
                        return false;
                    case DialogAction.SETPRO1:
                        GiveQuestItem(env, 182215630, 1);
                        return DefaultCloseDialog(env, 0, 1); // 1
                }
            }
            else if (targetId == 799295)
            { // Ortiz
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 1)
                        {
                            return SendQuestDialog(env, 1352);
                        }
                        return false;
                    case DialogAction.SETPRO2:
                        RemoveQuestItem(env, 182215630, 1);
                        GiveQuestItem(env, 182215595, 1);
                        return DefaultCloseDialog(env, 1, 2); // 2
                }
            }
            else if (targetId == 730243)
            { // Destroyed Gargoyle
                switch (dialogActionId)
                {
                    case DialogAction.USE_OBJECT:
                        if (var == 2)
                        {
                            return SendQuestDialog(env, 1693);
                        }
                        return false;
                    case DialogAction.SETPRO3:
                        if (var == 2)
                        {
                            RemoveQuestItem(env, 182215595, 1);
                            WorldMapInstance newInstance = InstanceService.GetNextAvailableInstance(WorldMapType.UDAS_TEMPLE.GetId(), player);
                            TeleportService.TeleportTo(player, newInstance, 561.8651f, 221.91483f, 134.53333f, (byte) 90,
                                TeleportAnimation.FADE_OUT_BEAM);
                            return true;
                        }
                        break;
                }
            }
            else if (targetId == 799513 || targetId == 799514 || targetId == 799515 || targetId == 799516)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 3)
                        {
                            int var1 = qs.GetQuestVarById(1);
                            int var2 = qs.GetQuestVarById(2);
                            int var3 = qs.GetQuestVarById(3);
                            int var4 = qs.GetQuestVarById(4);

                            if (var1 < 1 && targetId == 799513)
                            {
                                qs.SetQuestVarById(1, 1);
                                var1 = qs.GetQuestVarById(1);
                            }
                            else if (var2 < 1 && targetId == 799514)
                            {
                                qs.SetQuestVarById(2, 1);
                                var2 = qs.GetQuestVarById(2);
                            }
                            else if (var3 < 1 && targetId == 799515)
                            {
                                qs.SetQuestVarById(3, 1);
                                var3 = qs.GetQuestVarById(3);
                            }
                            else if (var4 < 1 && targetId == 799516)
                            {
                                qs.SetQuestVarById(4, 1);
                                var4 = qs.GetQuestVarById(4);
                            }
                            UpdateQuestStatus(env);

                            if (var1 * var2 * var3 * var4 == 1)
                            { // All Reians are free
                                PlayQuestMovie(env, 442);
                                qs.SetQuestVar(4);
                                UpdateQuestStatus(env);
                            }
                            npc.GetController().Delete();
                            return CloseDialogWindow(env);
                        }
                        break;
                }
                return SendQuestSelectionDialog(env);
            }
            else if (targetId == 799341)
            { // Anilmo
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 5)
                        {
                            return SendQuestDialog(env, 2716);
                        }
                        break;
                    case DialogAction.SETPRO6:
                        ChangeQuestStep(env, 5, 6);// 6
                        npc.GetController().Delete();
                        return CloseDialogWindow(env);
                }
            }
            return false;
        }

        public override bool OnKillEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs == null || qs.GetStatus() != QuestStatus.START)
            {
                return false;
            }
            int targetId = env.GetTargetId();
            int var = qs.GetQuestVarById(0);
            if (targetId == 216592)
            {
                if (var == 4)
                {
                    SpawnForFiveMinutes(799341, player.GetWorldMapInstance(), 561.8763f, 192.25128f, 135.88919f, (byte) 30);
                    qs.SetQuestVarById(0, var + 1);
                    UpdateQuestStatus(env);
                    return true;
                }
            }
            return false;
        }

        public override void OnMovieEndEvent(QuestEnv env, int movieId)
        {
            if (movieId == 442)
                SpawnForFiveMinutes(216592, env.GetPlayer().GetWorldMapInstance(), (float) 561.8763, (float) 192.25128, (float) 135.88919, (byte) 30);
        }

        public override void OnQuestCompletedEvent(QuestEnv env)
        {
            DefaultOnQuestCompletedEvent(env, 20031);
        }

        public override void OnLevelChangedEvent(Player player)
        {
            DefaultOnLevelChangedEvent(player, 20031);
        }
    }
}
