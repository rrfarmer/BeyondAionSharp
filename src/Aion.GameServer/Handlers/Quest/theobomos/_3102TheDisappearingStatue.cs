using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

public class _3102TheDisappearingStatue : AbstractQuestHandler
{
    public _3102TheDisappearingStatue() : base(3102)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(798206).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(798206).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798167).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798177).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798225).AddOnTalkEvent(questId);
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
            if (targetId == 798206)
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
                case 798167:
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
                            PacketSendUtility.SendPacket(player, new SmDialogWindow(env.GetVisibleObject().GetObjectId(), 0));
                            return true;
                        }
                    }
                    return false;
                case 798177:
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                        {
                            return SendQuestDialog(env, 1693);
                        }
                        case DialogAction.SETPRO2:
                        {
                            qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                            qs.SetStatus(QuestStatus.REWARD);
                            UpdateQuestStatus(env);
                            PacketSendUtility.SendPacket(player, new SmDialogWindow(env.GetVisibleObject().GetObjectId(), 0));
                            return true;
                        }
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 798225)
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
