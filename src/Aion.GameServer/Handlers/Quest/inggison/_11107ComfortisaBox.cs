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
    /// @author Leunam
    /// </summary>
    public class _11107ComfortisaBox : AbstractQuestHandler
    {
        private static readonly int[] npc_ids = { 798963, 296489, 296490, 296491 };

        public _11107ComfortisaBox() : base(11107)
        {
        }

        public override void Register()
        {
            qe.RegisterQuestNpc(798963).AddOnQuestStart(questId);
            foreach (int npc_id in npc_ids)
                qe.RegisterQuestNpc(npc_id).AddOnTalkEvent(questId);
        }

        public override bool OnDialogEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            int targetId = 0;
            if (env.GetVisibleObject() is Npc)
                targetId = ((Npc)env.GetVisibleObject()).GetNpcId();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (targetId == 798963)
            {
                if (qs == null || qs.IsStartable())
                {
                    if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                        return SendQuestDialog(env, 1011);
                    else if (env.GetDialogActionId() == DialogAction.QUEST_ACCEPT_1)
                    {
                        if (GiveQuestItem(env, 182206859, 3))
                            return SendQuestStartDialog(env);
                        else
                            return true;
                    }
                    else
                        return SendQuestStartDialog(env);
                }
            }
            if (qs == null)
                return false;

            int var = qs.GetQuestVarById(0);
            if (qs.GetStatus() == QuestStatus.REWARD)
            {
                if (targetId == 798963)
                {
                    if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
                        return SendQuestDialog(env, 2375);
                    else if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD)
                        return SendQuestDialog(env, 5);
                    else
                        return SendQuestEndDialog(env);
                }
            }
            else if (qs.GetStatus() != QuestStatus.START)
            {
                return false;
            }
            if (targetId == 296489)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 0)
                            return SendQuestDialog(env, 1352);
                        return false;
                    case DialogAction.SETPRO1:
                        if (var == 0)
                        {
                            RemoveQuestItem(env, 182206859, 1);
                            qs.SetQuestVarById(0, var + 1);
                            UpdateQuestStatus(env);
                            PacketSendUtility.SendPacket(player, new SmDialogWindow(env.GetVisibleObject().GetObjectId(), 10));
                            return true;
                        }
                        return false;
                }
            }
            else if (targetId == 296490)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 1)
                            return SendQuestDialog(env, 1693);
                        return false;
                    case DialogAction.SETPRO2:
                        if (var == 1)
                        {
                            RemoveQuestItem(env, 182206859, 1);
                            qs.SetQuestVarById(0, var + 1);
                            UpdateQuestStatus(env);
                            PacketSendUtility.SendPacket(player, new SmDialogWindow(env.GetVisibleObject().GetObjectId(), 10));
                            return true;
                        }
                        return false;
                }
            }
            else if (targetId == 296491)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 2)
                            return SendQuestDialog(env, 2034);
                        return false;
                    case DialogAction.SETPRO3:
                        if (var == 2)
                        {
                            RemoveQuestItem(env, 182206859, 1);
                            qs.SetQuestVarById(0, var + 1);
                            qs.SetStatus(QuestStatus.REWARD);
                            UpdateQuestStatus(env);
                            PacketSendUtility.SendPacket(player, new SmDialogWindow(env.GetVisibleObject().GetObjectId(), 10));
                            return true;
                        }
                        return false;
                }
            }
            return false;
        }
    }
}
