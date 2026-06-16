using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.QuestEngine.Handlers.Models.XmlQuest.Operations;

/// <summary>Java parity: .../operations/TakeItemOperation.</summary>
[XmlType("TakeItemOperation")]
public class TakeItemOperation : QuestOperation
{
    [XmlAttribute("item_id")] public int itemId;
    [XmlAttribute("count")] public int count;

    public override void DoOperate(QuestEnv env)
    {
        env.GetPlayer().GetInventory().DecreaseByItemId(itemId, count);
    }
}
