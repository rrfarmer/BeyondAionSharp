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
    public class _20032AllAboutAbnormalAether : AbstractQuestHandler
    {
        public _20032AllAboutAbnormalAether() : base(20032)
        {
        }

        public override void Register()
        {
            // Angrad ID: 799247
            // Eddas ID: 799250
            // Taloc's Guardian ID: 799325
            // Taloc's Mirage ID: 799503
            int[] npcs = { 799247, 799250, 799325 };
            qe.RegisterOnQuestCompleted(questId);
            qe.RegisterOnLevelChanged(questId);
            qe.RegisterOnEnterWorld(questId);
            qe.RegisterOnDie(questId);
            qe.RegisterOnLogOut(questId);
            foreach (int npc in npcs)
            {
                qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
            }
            qe.RegisterQuestNpc(215488).AddOnKillEvent(questId); // Celestius
            qe.RegisterQuestItem(182215592, questId); // quest_20032a
            qe.RegisterQuestItem(182215593, questId); // quest_20032b
        }

        public override bool OnDialogEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs == null)
                return false;
            int var = qs.GetQuestVarById(0);
            int targetId = env.GetTargetId();
            int dialogActionId = env.GetDialogActionId();

            if (qs.GetStatus() == QuestStatus.START)
            {
                if (targetId == 799247)
                { // Angrad
                    if (var == 0)
                    {
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                return SendQuestDialog(env, 1011);
                            case DialogAction.SETPRO1:
                                return DefaultCloseDialog(env, var, var + 1); // 1
                        }
                    }
                }
                else if (targetId == 799250)
                { // Eddas
                    if (var == 1)
                    {
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                return SendQuestDialog(env, 1352);
                            case DialogAction.SETPRO2:
                                return DefaultCloseDialog(env, var, var + 1); // 2
                        }
                    }
                }
                else if (targetId == 799325)
                { // Taloc's Guardian
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 2)
                            {
                                return SendQuestDialog(env, 1693);
                            }
                            break;
                        case DialogAction.SETPRO3:
                            if (GiveQuestItem(env, 182215592, 1) && GiveQuestItem(env, 182215593, 1))
                            {
                                ChangeQuestStep(env, 2, 3); // 3
                                WorldMapInstance newInstance = InstanceService.GetNextAvailableInstance(WorldMapType.TALOCS_HOLLOW.GetId(), player);
                                TeleportService.TeleportTo(player, newInstance, 202.26694f, 226.0532f, 1098.236f, (byte) 30,
                                    TeleportAnimation.FADE_OUT_BEAM);
                                return CloseDialogWindow(env);
                            }
                            else
                            {
                                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_FULL_INVENTORY());
                                return SendQuestSelectionDialog(env);
                            }
                    }
                }
                /*
                 * To check on retail if there is a dedicate portal to exit.
                 * else if (targetId == 799503) { // Taloc's Mirage
                 * switch (dialogActionId) {
                 * case USE_OBJECT:
                 * if (var == 7) {
                 * return sendQuestDialog(env, 3057);
                 * }
                 * break;
                 * case SETPRO7: {
                 * qs.setQuestVar(8); // Reward
                 * qs.setStatus(QuestStatus.REWARD);
                 * updateQuestStatus(env);
                 * TeleportService.teleportTo(env.getPlayer(), 220070000, 1025, 2782, 388, (byte) 60, TeleportAnimation.BEAM_ANIMATION);
                 * return closeDialogWindow(env);
                 * }
                 * }
                 * }
                 */
            }
            else if (qs.GetStatus() == QuestStatus.REWARD)
            {
                if (targetId == 799247)
                { // Angrad
                    if (dialogActionId == DialogAction.USE_OBJECT)
                    {
                        return SendQuestDialog(env, 10002);
                    }
                    return SendQuestEndDialog(env);
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
                int targetId = env.GetTargetId();
                int var = qs.GetQuestVarById(0);

                switch (targetId)
                {
                    case 215488: // Celestius
                        if (var == 6)
                        {
                            PlayQuestMovie(env, 437);
                            return DefaultOnKillEvent(env, 215488, var, var + 1); // 7
                        }
                        break;
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
                if (player.GetWorldId() == 300190000)
                {
                    int itemId = item.GetItemTemplate().GetTemplateId();
                    int var = qs.GetQuestVarById(0);
                    int var1 = qs.GetQuestVarById(1);
                    if (itemId == 182215593)
                    { // quest_20032b
                        ChangeQuestStep(env, 4, 5); // 7
                        return HandlerResult.SUCCESS; // //TODO: Should return FAILED (not removed, but skill still should be used)
                    }
                    else if (itemId == 182215592)
                    { // quest_20032a
                        if (var == 5)
                        {
                            if (var1 >= 0 && var1 < 19)
                            {
                                ChangeQuestStep(env, var1, var1 + 1, false, 1); // 3: 19
                                return HandlerResult.SUCCESS;
                            }
                            else if (var1 == 19)
                            {
                                qs.SetQuestVar(6);
                                UpdateQuestStatus(env);
                                return HandlerResult.SUCCESS;
                            }
                        }
                    }
                }
            }
            return HandlerResult.UNKNOWN;
        }

        public override bool OnEnterWorldEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs != null && qs.GetStatus() == QuestStatus.START)
            {
                if (player.GetWorldId() == 300190000)
                {
                    qs.SetQuestVar(4);
                    UpdateQuestStatus(env);
                    return true;
                }
                else
                {
                    RemoveQuestItem(env, 182215592, 1);
                    RemoveQuestItem(env, 182215593, 1);

                    int var = qs.GetQuestVarById(0);
                    if (var >= 3 && var < 7)
                    {
                        RestoreStep(env);
                        return true;
                    }
                    else if (var == 7)
                    { // Final boss killed
                        RemoveQuestItem(env, 182215618, 1);
                        RemoveQuestItem(env, 182215619, 1);
                        qs.SetQuestVar(8); // Reward
                        qs.SetStatus(QuestStatus.REWARD);
                        UpdateQuestStatus(env);
                        return true;
                    }
                }
            }
            return false;
        }

        public override bool OnDieEvent(QuestEnv env)
        {
            return RestoreStep(env);
        }

        public override bool OnLogOutEvent(QuestEnv env)
        {
            return RestoreStep(env);
        }

        public override void OnQuestCompletedEvent(QuestEnv env)
        {
            DefaultOnQuestCompletedEvent(env, 20031);
        }

        public override void OnLevelChangedEvent(Player player)
        {
            DefaultOnLevelChangedEvent(player, 20031);
        }

        private bool RestoreStep(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs != null && qs.GetStatus() == QuestStatus.START)
            {
                int var = qs.GetQuestVarById(0);
                if (var >= 3 && var <= 7)
                {
                    RemoveQuestItem(env, 182215618, 1);
                    RemoveQuestItem(env, 182215619, 1);
                    qs.SetQuestVar(2);
                    UpdateQuestStatus(env);
                    return true;
                }
            }
            return false;
        }
    }
}
