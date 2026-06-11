using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.QuestEngine.Handlers.Models.XmlQuest.Operations;

/// <summary>Java parity: .../operations/SetQuestStatusOperation.</summary>
[XmlType("SetQuestStatusOperation")]
public class SetQuestStatusOperation : QuestOperation
{
    [XmlAttribute("status")] protected QuestStatus status;

    public override void DoOperate(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int questId = env.GetQuestId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null)
        {
            qs.SetStatus(status);
            PacketSendUtility.SendPacket(player, new SM_QUEST_ACTION(ActionType.UPDATE, qs));
            if (qs.GetStatus() == QuestStatus.COMPLETE)
                player.GetController().UpdateNearbyQuests();
        }
    }
}
