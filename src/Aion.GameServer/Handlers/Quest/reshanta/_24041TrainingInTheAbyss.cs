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
    public class _24041TrainingInTheAbyss : AbstractQuestHandler
    {
        private static readonly int[] npc_ids = { 278126, 278127, 278128, 278129, 278130, 278131, 278136, 278054 };

        public _24041TrainingInTheAbyss() : base(24041)
        {
        }

        public override void Register()
        {
            qe.RegisterOnQuestCompleted(questId);
            qe.RegisterOnLevelChanged(questId);
            foreach (int npc_id in npc_ids)
                qe.RegisterQuestNpc(npc_id).AddOnTalkEvent(questId);
        }

        public override void OnQuestCompletedEvent(QuestEnv env)
        {
            DefaultOnQuestCompletedEvent(env, 24040);
        }

        public override void OnLevelChangedEvent(Player player)
        {
            DefaultOnLevelChangedEvent(player, 24040);
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
                if (targetId == 278054)
                {
                    if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
                        return SendQuestDialog(env, 10002);
                    else if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD)
                        return SendQuestDialog(env, 5);
                    else
                        return SendQuestEndDialog(env);
                }
                return false;
            }
            else if (qs.GetStatus() != QuestStatus.START)
            {
                return false;
            }
            if (targetId == 278126)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 0)
                            return SendQuestDialog(env, 1011);
                        return false;
                    case DialogAction.SELECT1_1_1:
                        PlayQuestMovie(env, 282);
                        break;
                    case DialogAction.SETPRO1:
                        if (var == 0)
                        {
                            qs.SetQuestVarById(0, var + 1);
                            UpdateQuestStatus(env);
                            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                            return true;
                        }
                        return false;
                }
            }
            else if (targetId == 278127)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 1)
                            return SendQuestDialog(env, 1352);
                        return false;
                    case DialogAction.SELECT2_1:
                        PlayQuestMovie(env, 283);
                        break;
                    case DialogAction.SETPRO2:
                        if (var == 1)
                        {
                            qs.SetQuestVarById(0, var + 1);
                            UpdateQuestStatus(env);
                            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                            return true;
                        }
                        return false;
                }
            }
            else if (targetId == 278128)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 2)
                            return SendQuestDialog(env, 1693);
                        return false;
                    case DialogAction.SELECT3_1:
                        PlayQuestMovie(env, 284);
                        break;
                    case DialogAction.SETPRO3:
                        if (var == 2)
                        {
                            qs.SetQuestVarById(0, var + 1);
                            UpdateQuestStatus(env);
                            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                            return true;
                        }
                        return false;
                }
            }
            else if (targetId == 278129)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 3)
                            return SendQuestDialog(env, 2034);
                        return false;
                    case DialogAction.SELECT4_1:
                        PlayQuestMovie(env, 285);
                        break;
                    case DialogAction.SETPRO4:
                        if (var == 3)
                        {
                            qs.SetQuestVarById(0, var + 1);
                            UpdateQuestStatus(env);
                            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                            return true;
                        }
                        return false;
                }
            }
            else if (targetId == 278130)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 4)
                            return SendQuestDialog(env, 2375);
                        return false;
                    case DialogAction.SELECT5_1:
                        PlayQuestMovie(env, 286);
                        break;
                    case DialogAction.SETPRO5:
                        if (var == 4)
                        {
                            qs.SetQuestVarById(0, var + 1);
                            UpdateQuestStatus(env);
                            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                            return true;
                        }
                        return false;
                }
            }
            else if (targetId == 278131)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 5)
                            return SendQuestDialog(env, 2716);
                        return false;
                    case DialogAction.SELECT6_1:
                        PlayQuestMovie(env, 287);
                        break;
                    case DialogAction.SETPRO6:
                        if (var == 5)
                        {
                            qs.SetQuestVarById(0, var + 1);
                            UpdateQuestStatus(env);
                            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                            return true;
                        }
                        return false;
                }
            }
            else if (targetId == 278136)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 6)
                            return SendQuestDialog(env, 3057);
                        return false;
                    case DialogAction.SELECT7_1:
                        PlayQuestMovie(env, 288);
                        break;
                    case DialogAction.SET_SUCCEED:
                        if (var == 6)
                        {
                            qs.SetStatus(QuestStatus.REWARD);
                            UpdateQuestStatus(env);
                            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                            return true;
                        }
                        return false;
                }
            }
            return false;
        }
    }
}
