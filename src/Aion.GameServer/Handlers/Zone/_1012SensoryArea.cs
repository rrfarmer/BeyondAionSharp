using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Zone;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Zone.Handler;

namespace Aion.GameServer.Handlers.Zone;

/// <summary>Java parity: zone/_1012SensoryArea (Rolandas) : QuestZoneHandler. @ZoneNameAnnotation(value=3 scouting zones, questId=1012); anonymous AbstractQuestZoneObserver{onMoved}→nested private Observer overriding OnMoved; getQuestVars().getQuestVars()!=1→GetQuestVars().GetQuestVars()!=1; SM_QUEST_ACTION(ActionType.UPDATE, qs) 1:1.</summary>
[ZoneNameAnnotation(
    "LF1A_SENSORYAREA_Q1012_2_206005_4_210030000 LF1A_SENSORYAREA_Q1012_3_206006_6_210030000 LF1A_SENSORYAREA_Q1012_1_206004_8_210030000",
    QuestId = 1012)]
public class _1012SensoryArea : QuestZoneHandler
{
    public override AbstractQuestZoneObserver CreateObserver(Player player, ZoneTemplate zoneTemplate)
    {
        return new Observer(player, zoneTemplate, questId);
    }

    private sealed class Observer : AbstractQuestZoneObserver
    {
        private readonly int questId;

        public Observer(Player player, ZoneTemplate zoneTemplate, int questId) : base(player, zoneTemplate)
        {
            this.questId = questId;
        }

        public override void OnMoved(float distanceScouted, float distanceToCenter, int steps, long timeSpent)
        {
            // Another way to do the same is like this:
            //
            // QuestEngine.getInstance().onEnterZone(new QuestEnv(null, player, questId), observedZone.getName());
            //
            // In this case, it will be handled inside the quest script. But you need to register
            // each zone in the quest script. For scouting you may save distance, time and send the event back
            // to the QuestEngine when the criteria are met. It will work like a delayed onEnterZone event.
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs == null || qs.GetQuestVars().GetQuestVars() != 1)
                return;
            qs.SetQuestVarById(0, 2); // 2
            PacketSendUtility.SendPacket(player, new SM_QUEST_ACTION(SM_QUEST_ACTION.ActionType.UPDATE, qs));
        }
    }
}
