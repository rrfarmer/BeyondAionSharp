using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Questengine.Model;

namespace Aion.GameServer.Questengine.Handlers.Models.XmlQuest.Operations;

/// <summary>Java parity: .../operations/TakeItemOperation.</summary>
[XmlType("TakeItemOperation")]
public class TakeItemOperation : QuestOperation
{
    [XmlAttribute("item_id")] protected int itemId;
    [XmlAttribute("count")] protected int count;

    public override void DoOperate(QuestEnv env)
    {
        env.GetPlayer().GetInventory().DecreaseByItemId(itemId, count);
    }
}
