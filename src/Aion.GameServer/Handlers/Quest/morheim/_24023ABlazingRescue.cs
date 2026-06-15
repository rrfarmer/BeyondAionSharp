using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest
{
    /// <summary>
    /// @author Ritsu
    /// </summary>
    public class _24023ABlazingRescue : AbstractQuestHandler
    {
        public _24023ABlazingRescue() : base(24023)
        {
        }

        public override void Register()
        {
            int[] npc_ids = { 204317, 204372, 204408 };
            qe.RegisterOnQuestCompleted(questId);
            qe.RegisterOnLevelChanged(questId);
            qe.RegisterOnEnterWorld(questId);
            foreach (int npc_id in npc_ids)
                qe.RegisterQuestNpc(npc_id).AddOnTalkEvent(questId);
        }

        public override void OnQuestCompletedEvent(QuestEnv env)
        {
            DefaultOnQuestCompletedEvent(env, 24020);
        }

        public override void OnLevelChangedEvent(Player player)
        {
            DefaultOnLevelChangedEvent(player, 24020);
        }

        public override bool OnEnterWorldEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs != null && qs.GetStatus() == QuestStatus.START)
            {
                if (player.GetWorldId() == 320050000 && qs.GetQuestVarById(0) == 2)
                {
                    qs.SetQuestVar(3);
                    UpdateQuestStatus(env);
                }
            }
            return false;
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
                switch (targetId)
                {
                    case 204317:
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                if (var == 0)
                                    return SendQuestDialog(env, 1011);
                                return false;
                            case DialogAction.SETPRO1:
                                if (var == 0)
                                {
                                    return DefaultCloseDialog(env, 0, 1); // 1
                                }
                                break;
                        }
                        break;
                    case 204408:
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                if (var == 1)
                                    return SendQuestDialog(env, 1352);
                                else if (var == 3)
                                    return SendQuestDialog(env, 2034);
                                return false;
                            case DialogAction.SELECT2_1_1:
                                PlayQuestMovie(env, 78);
                                break;
                            case DialogAction.SETPRO2:
                                if (var == 1)
                                {
                                    if (!GiveQuestItem(env, 182215369, 1))
                                        return true;
                                    qs.SetQuestVarById(0, 2);
                                    UpdateQuestStatus(env);
                                    PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                                    return true;
                                }
                                return false;
                            case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                                if (var == 3)
                                {
                                    if (QuestService.CollectItemCheck(env, true))
                                    {
                                        RemoveQuestItem(env, 182215369, 1);
                                        qs.SetStatus(QuestStatus.REWARD);
                                        UpdateQuestStatus(env);
                                        return SendQuestDialog(env, 10000);
                                    }
                                    else
                                        return SendQuestDialog(env, 10001);
                                }
                                break;
                        }
                        break;
                }
            }
            else if (qs.GetStatus() == QuestStatus.REWARD)
            {
                if (targetId == 204372)
                {
                    if (dialogActionId == DialogAction.USE_OBJECT)
                        return SendQuestDialog(env, 10002);
                    else
                        return SendQuestEndDialog(env);
                }
            }
            return false;
        }
    }
}
