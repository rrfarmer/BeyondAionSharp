using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

public class _3006TheShugoFugitive : AbstractQuestHandler
{
    public _3006TheShugoFugitive() : base(3006)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(798132).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(798132).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798146).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(700339).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        int targetId = 0;
        if (env.GetVisibleObject() is Npc npc)
            targetId = npc.GetNpcId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 798132)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 1011);
                    default:
                        return SendQuestStartDialog(env);
                }
            }
        }

        if (qs == null)
            return false;

        if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 798146:
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
                case 700339:
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.USE_OBJECT:
                        {
                            if (qs.GetQuestVarById(0) == 1)
                            {
                                return SendQuestDialog(env, 1693);
                            }
                            return false;
                        }
                        case DialogAction.SELECT3_1:
                        {
                            if (qs.GetQuestVarById(0) == 1)
                            {
                                PlayQuestMovie(env, 361);
                                return SendQuestDialog(env, 1694);
                            }
                            return false;
                        }
                        case DialogAction.SETPRO2:
                        {
                            GiveQuestItem(env, 182208003, 1);
                            qs.SetQuestVarById(0, 2);
                            qs.SetStatus(QuestStatus.REWARD);
                            UpdateQuestStatus(env);
                            return CloseDialogWindow(env);
                        }
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 798132)
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
