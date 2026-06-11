using System;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.Templates.Items.Actions;

/// <summary>Java parity: model/templates/item/actions/ReadAction.</summary>
[XmlType("ReadAction")]
public class ReadAction : AbstractItemAction
{
    public override bool CanAct(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        // TODO: get quest
        return true;
    }

    public override void Act(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        int itemObjId = parentItem.GetObjectId();
        int id = parentItem.GetItemTemplate().GetTemplateId();

        Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SmItemUsageAnimation(player.GetObjectId(), itemObjId, id, 50, 0, 0), true);
        Aion.GameServer.Utils.ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SmItemUsageAnimation(player.GetObjectId(), itemObjId, id, 0, 1, 0), true);
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(50));
    }
}
