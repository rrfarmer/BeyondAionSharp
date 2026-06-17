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
    /// @author Balthazar
    /// </summary>
    public class _1691TheLittleLeatherSlipper : AbstractQuestHandler
    {
        public _1691TheLittleLeatherSlipper() : base(1691)
        {
        }

        public override void Register()
        {
            qe.RegisterQuestNpc(798386).AddOnQuestStart(questId);
            qe.RegisterQuestNpc(798386).AddOnTalkEvent(questId);
            qe.RegisterQuestNpc(790005).AddOnTalkEvent(questId);
            qe.RegisterQuestNpc(700563).AddOnTalkEvent(questId);
        }

        public override bool OnDialogEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);

            int targetId = 0;
            if (env.GetVisibleObject() is Npc)
                targetId = ((Npc)env.GetVisibleObject()).GetNpcId();

            if (qs == null || qs.IsStartable())
            {
                if (targetId == 798386)
                {
                    if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    {
                        return SendQuestDialog(env, 1011);
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
                    case 790005:
                        switch (env.GetDialogActionId())
                        {
                            case DialogAction.QUEST_SELECT:
                                {
                                    if (qs.GetQuestVarById(0) == 0)
                                    {
                                        return SendQuestDialog(env, 1352);
                                    }
                                    return false;
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
                    case 798386:
                        switch (env.GetDialogActionId())
                        {
                            case DialogAction.QUEST_SELECT:
                                {
                                    if (qs.GetQuestVarById(0) == 1)
                                    {
                                        return SendQuestDialog(env, 1693);
                                    }
                                    return false;
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
                    case 700563:
                        switch (env.GetDialogActionId())
                        {
                            case DialogAction.USE_OBJECT:
                                {
                                    if (qs.GetQuestVarById(0) == 2)
                                    {
                                        return SendQuestDialog(env, 2034);
                                    }
                                    return false;
                                }
                            case DialogAction.SETPRO3:
                                {
                                    qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                                    qs.SetStatus(QuestStatus.REWARD);
                                    UpdateQuestStatus(env);
                                    PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                                    return true;
                                }
                        }
                        break;
                }
            }
            else if (qs.GetStatus() == QuestStatus.REWARD)
            {
                if (targetId == 798386)
                {
                    if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD)
                        return SendQuestDialog(env, 5);
                    else
                        return SendQuestEndDialog(env);
                }
            }
            return false;
        }
    }
}
