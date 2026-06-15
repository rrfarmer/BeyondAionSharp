using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Handlers.Quest;

public class _1393NewFlightPath : AbstractQuestHandler
{
    public _1393NewFlightPath() : base(1393)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204041).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(204041).AddOnTalkEvent(questId);
        qe.RegisterOnEnterZone(ZoneName.Get("LEPHARIST_BASTION_210020000"), questId);
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
            if (targetId == 204041)
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
            if (targetId == 204041)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                    case DialogAction.QUEST_ACCEPT_1:
                        return SendQuestDialog(env, 1003);
                    case DialogAction.SETPRO1:
                        player.SetState(CreatureState.FLYING);
                        player.UnsetState(CreatureState.ACTIVE);
                        player.SetFlightTeleportId(17001);
                        PacketSendUtility.SendPacket(player, new SM_EMOTION(player, EmotionType.START_FLYTELEPORT, 17001, 0));
                        return CloseDialogWindow(env);
                }
            }
        }
        if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 204041)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.USE_OBJECT:
                        return SendQuestDialog(env, 1352);
                    default:
                        return SendQuestEndDialog(env);
                }
            }
        }
        return false;
    }

    public override bool OnEnterZoneEvent(QuestEnv env, ZoneName zoneName)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            ChangeQuestStep(env, 0, 0, true);
            return true;
        }
        return false;
    }
}
