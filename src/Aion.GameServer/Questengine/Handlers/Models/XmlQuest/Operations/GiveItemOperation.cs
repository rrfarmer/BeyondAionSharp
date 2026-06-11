using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.QuestEngine.Handlers.Models.XmlQuest.Operations;

/// <summary>Java parity: .../operations/GiveItemOperation.</summary>
[XmlType("GiveItemOperation")]
public class GiveItemOperation : QuestOperation
{
    [XmlAttribute("item_id")] protected int itemId;
    [XmlAttribute("count")] protected int count;

    public override void DoOperate(QuestEnv env)
    {
        ItemService.AddItem(env.GetPlayer(), itemId, count, true);
    }
}
