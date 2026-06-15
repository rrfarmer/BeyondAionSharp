using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

public class _3044RecruitingAnnouncement : AbstractQuestHandler
{
    public _3044RecruitingAnnouncement() : base(3044)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(730145).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(730145).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798206).AddOnTalkEvent(questId);
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
            if (targetId == 730145)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 4762);
                    case DialogAction.SETPRO1:
                        QuestService.StartQuest(env);
                        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(0, 0));
                        return true;
                    default:
                        return SendQuestStartDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 798206)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD)
                    return DefaultCloseDialog(env, 0, 1, true, true);
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 798206)
            {
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
