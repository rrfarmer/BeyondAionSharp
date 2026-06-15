using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest
{
    /// <summary>
    /// @author Artur, Majka
    /// </summary>
    public class _14052RestlessSouls : AbstractQuestHandler
    {
        public _14052RestlessSouls() : base(14052)
        {
        }

        public override void Register()
        {
            int[] npc_ids = { 204629, 204625, 204628, 204627, 204626, 204622, 700270 };
            qe.RegisterOnQuestCompleted(questId);
            qe.RegisterOnLevelChanged(questId);
            foreach (int npc_id in npc_ids)
                qe.RegisterQuestNpc(npc_id).AddOnTalkEvent(questId);
        }

        public override void OnQuestCompletedEvent(QuestEnv env)
        {
            DefaultOnQuestCompletedEvent(env, 14050);
        }

        public override void OnLevelChangedEvent(Player player)
        {
            DefaultOnLevelChangedEvent(player, 14050);
        }

        public override bool OnDialogEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs == null)
                return false;

            int var = qs.GetQuestVarById(0);
            int targetId = 0;
            if (env.GetVisibleObject() is Npc npcObj)
                targetId = npcObj.GetNpcId();

            if (qs.GetStatus() == QuestStatus.REWARD)
            {
                if (targetId == 204629)
                    return SendQuestEndDialog(env);
            }
            else if (qs.GetStatus() != QuestStatus.START)
            {
                return false;
            }
            if (targetId == 204629)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 0)
                            return SendQuestDialog(env, 1011);
                        else if (var == 2)
                            return SendQuestDialog(env, 1693);
                        return false;
                    case DialogAction.SETPRO1:
                        if (var == 0)
                        {
                            return DefaultCloseDialog(env, 0, 1); // 1
                        }
                        break;
                }
            }
            else if (targetId == 204625)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 1)
                            return SendQuestDialog(env, 1352);
                        else if (var == 2)
                            return SendQuestDialog(env, 1693);
                        else if (var == 4)
                            return SendQuestDialog(env, 2375);
                        return false;
                    case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                        return CheckQuestItems(env, 2, 3, false, 10000, 10001, 182215344, 1); // 3
                    case DialogAction.SETPRO2:
                        if (var == 1)
                        {
                            return DefaultCloseDialog(env, 1, 2); // 2
                        }
                        return false;
                    case DialogAction.SET_SUCCEED:
                        if (var == 4)
                        {
                            return DefaultCloseDialog(env, 4, 4, true, false); // 4
                        }
                        return false;
                }
            }
            else if (targetId == 204628)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 2)
                            return SendQuestDialog(env, 1694);
                        return false;
                    case DialogAction.SETPRO3:
                        if (var == 2)
                        {
                            if (player.GetInventory().GetItemCountByItemId(182215340) == 0)
                            {
                                if (!GiveQuestItem(env, 182215340, 1))
                                    return true;
                            }
                            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                            return true;
                        }
                        return false;
                }
            }
            else if (targetId == 204627)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 2)
                            return SendQuestDialog(env, 1781);
                        return false;
                    case DialogAction.SETPRO3:
                        if (var == 2)
                        {
                            if (player.GetInventory().GetItemCountByItemId(182215341) == 0)
                            {
                                if (!GiveQuestItem(env, 182215341, 1))
                                    return true;
                            }
                            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                            return true;
                        }
                        return false;
                }
            }
            else if (targetId == 204626)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 2)
                            return SendQuestDialog(env, 1864);
                        return false;
                    case DialogAction.SETPRO3:
                        if (var == 2)
                        {
                            if (player.GetInventory().GetItemCountByItemId(182215342) == 0)
                            {
                                if (!GiveQuestItem(env, 182215342, 1))
                                    return true;
                            }
                            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                            return true;
                        }
                        return false;
                }
            }
            else if (targetId == 204622)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 2)
                            return SendQuestDialog(env, 1949);
                        return false;
                    case DialogAction.SETPRO3:
                        if (var == 2)
                        {
                            if (player.GetInventory().GetItemCountByItemId(182215343) == 0)
                            {
                                if (!GiveQuestItem(env, 182215343, 1))
                                    return true;
                            }
                            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                            return true;
                        }
                        return false;
                }
            }
            else if (targetId == 700270)
            {
                if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
                {
                    return UseQuestObject(env, 3, 4, false, 0, 0, 1, 182215344, 1); // 4
                }
            }
            return false;
        }
    }
}
