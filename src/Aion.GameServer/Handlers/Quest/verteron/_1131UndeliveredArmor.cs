using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author MrPoke
/// </summary>
public class _1131UndeliveredArmor : AbstractQuestHandler
{
    public _1131UndeliveredArmor() : base(1131)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203097).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(203097).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(799093).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203101).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = env.GetTargetId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 203097)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 799093)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 1352);
                    case DialogAction.SETPRO1:
                        qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                        UpdateQuestStatus(env);
                        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                        return true;
                    default:
                        return SendQuestStartDialog(env);
                }
            }
            else if (targetId == 203101)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 2375);
                    case DialogAction.SELECT_QUEST_REWARD:
                        qs.SetQuestVar(2);
                        qs.SetStatus(QuestStatus.REWARD);
                        UpdateQuestStatus(env);
                        return SendQuestEndDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203101)
            {
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
