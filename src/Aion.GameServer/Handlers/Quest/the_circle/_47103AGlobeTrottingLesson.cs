using System.Linq;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Team.Group;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

public class _47103AGlobeTrottingLesson : AbstractQuestHandler
{
    public _47103AGlobeTrottingLesson() : base(47103)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(700971).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(799921).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(217173).AddOnKillEvent(questId);
    }

    public override bool OnKillEvent(QuestEnv env)
    {
        return DefaultOnKillEvent(env, 217173, 0, 5);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 0)
            {
                if (dialogActionId == DialogAction.QUEST_ACCEPT_1)
                {
                    QuestService.StartQuest(env);
                    return CloseDialogWindow(env);
                }
            }
        }

        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 700971)
            {
                if (player.IsInGroup())
                {
                    PlayerGroup group = player.GetPlayerGroup();
                    if (group.GetMembers().Any(member => member.IsMentor() && PositionUtil.IsInRange(player, member, GroupConfig.GROUP_MAX_DISTANCE)))
                    {
                        Npc npc = (Npc)env.GetVisibleObject();
                        npc.GetController().DeleteAndScheduleRespawn();
                        SpawnForFiveMinutes(217173, npc.GetPosition());
                        return true;
                    }
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_DailyQuest_Ask_Mentee());
                }
            }
            if (targetId == 799921)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (qs.GetQuestVarById(0) == 5)
                    {
                        return SendQuestDialog(env, 1352);
                    }
                }
                else if (dialogActionId == DialogAction.SELECT_QUEST_REWARD)
                {
                    return DefaultCloseDialog(env, 5, 5, true, true);
                }
            }
        }
        else if (qs != null && qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 799921)
            {
                if (dialogActionId == DialogAction.USE_OBJECT)
                {
                    return SendQuestDialog(env, 5);
                }
                else
                {
                    return SendQuestEndDialog(env);
                }
            }
        }
        return false;
    }
}
