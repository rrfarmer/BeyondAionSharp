using System.Linq;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Team.Group;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Cheatkiller
/// </summary>
public class _37107CoolBlueWater : AbstractQuestHandler
{
    public _37107CoolBlueWater() : base(37107)
    {
    }

    public override void Register()
    {
        qe.AddHandlerSideQuestDrop(questId, 700967, 182210040, 5, 100);
        qe.RegisterQuestNpc(700967).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(799901).AddOnTalkEvent(questId);
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
            if (targetId == 700967)
            {
                if (player.IsInGroup())
                {
                    PlayerGroup group = player.GetPlayerGroup();
                    if (group.GetMembers().Any(member => member.IsMentor() && PositionUtil.IsInRange(player, member, GroupConfig.GROUP_MAX_DISTANCE)))
                        return true;
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_DailyQuest_Ask_Mentee());
                }
            }
            if (targetId == 799901)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (qs.GetQuestVarById(0) == 0)
                    {
                        return SendQuestDialog(env, 2375);
                    }
                }
                else if (dialogActionId == DialogAction.CHECK_USER_HAS_QUEST_ITEM)
                {
                    return CheckQuestItems(env, 0, 1, true, 5, 2716);
                }
            }
        }
        else if (qs != null && qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 799901)
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
