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
    /// @author dta3000
    /// </summary>
    public class _11001KindMeira : AbstractQuestHandler
    {
        public _11001KindMeira() : base(11001)
        {
        }

        public override void Register()
        {
            qe.RegisterQuestNpc(798945).AddOnQuestStart(questId);
            qe.RegisterQuestNpc(798945).AddOnTalkEvent(questId);
            qe.RegisterQuestNpc(798918).AddOnTalkEvent(questId);
            qe.RegisterQuestNpc(798946).AddOnTalkEvent(questId);
            qe.RegisterQuestNpc(798943).AddOnTalkEvent(questId);
        }

        public override bool OnDialogEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);

            int targetId = 0;
            if (env.GetVisibleObject() is Npc)
                targetId = ((Npc)env.GetVisibleObject()).GetNpcId();

            if (targetId == 798945)
            {
                if (qs == null)
                {
                    if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                        return SendQuestDialog(env, 1011);
                    else if (env.GetDialogActionId() == DialogAction.QUEST_ACCEPT_1)
                    {
                        if (GiveQuestItem(env, 182206700, 1))
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

            if (qs.GetStatus() == QuestStatus.START)
            {
                switch (targetId)
                {
                    case 798918:
                        switch (env.GetDialogActionId())
                        {
                            case DialogAction.QUEST_SELECT:
                                {
                                    return SendQuestDialog(env, 1352);
                                }
                            case DialogAction.SETPRO1:
                                {
                                    qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                                    UpdateQuestStatus(env);
                                    PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                                    return true;
                                }
                        }
                        return false;
                    case 798946:
                        switch (env.GetDialogActionId())
                        {
                            case DialogAction.QUEST_SELECT:
                                {
                                    return SendQuestDialog(env, 1693);
                                }
                            case DialogAction.SETPRO2:
                                {
                                    qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                                    UpdateQuestStatus(env);
                                    PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                                    return true;
                                }
                        }
                        return false;
                    case 798943:
                        switch (env.GetDialogActionId())
                        {
                            case DialogAction.QUEST_SELECT:
                                {
                                    return SendQuestDialog(env, 2034);
                                }
                            case DialogAction.SETPRO3:
                                {
                                    qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                                    UpdateQuestStatus(env);
                                    PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                                    return true;
                                }
                        }
                        return false;
                    case 798945:
                        switch (env.GetDialogActionId())
                        {
                            case DialogAction.QUEST_SELECT:
                                {
                                    return SendQuestDialog(env, 2375);
                                }
                            case DialogAction.SELECT_QUEST_REWARD:
                                {
                                    qs.SetQuestVar(4);
                                    qs.SetStatus(QuestStatus.REWARD);
                                    UpdateQuestStatus(env);
                                    return SendQuestEndDialog(env);
                                }
                            default:
                                return SendQuestEndDialog(env);
                        }
                }
            }
            else if (qs.GetStatus() == QuestStatus.REWARD)
            {
                if (targetId == 798945)
                {
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.SELECT_QUEST_REWARD:
                            return SendQuestDialog(env, 5);
                        default:
                            return SendQuestEndDialog(env);
                    }
                }
            }
            return false;
        }

        public override void OnLevelChangedEvent(Player player)
        {
            DefaultOnLevelChangedEvent(player, 11001);
        }
    }
}
