using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Cheatkiller
/// </summary>
public class _2443TaisanMessage : AbstractQuestHandler
{
    public _2443TaisanMessage() : base(2443)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204403).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(790016).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;
        if (env.GetVisibleObject() is Npc)
            targetId = ((Npc)env.GetVisibleObject()).GetNpcId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null || qs.IsStartable())
        {
            if (targetId == 204403)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 4762);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 204403:
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                            {
                                if (qs.GetQuestVarById(0) == 0)
                                    return SendQuestDialog(env, 1003);
                                return false;
                            }
                        case DialogAction.SETPRO1:
                        case DialogAction.QUEST_ACCEPT_1:
                            {
                                player.SetState(CreatureState.FLYING);
                                player.UnsetState(CreatureState.ACTIVE);
                                player.SetFlightTeleportId(30001);
                                PacketSendUtility.SendPacket(player, new SM_EMOTION(player, EmotionType.START_FLYTELEPORT, 30001, 0));
                                return DefaultCloseDialog(env, 0, 1, true, false);
                            }
                        case DialogAction.SELECT1_1_1:
                            {
                                return SendQuestDialog(env, 1013);
                            }
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 790016)
            {
                if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
                {
                    return SendQuestDialog(env, 10002);
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
