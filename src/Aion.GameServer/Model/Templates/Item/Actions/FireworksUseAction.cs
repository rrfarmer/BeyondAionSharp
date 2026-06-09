using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.Templates.Item.Actions;

/// <summary>Java parity: model/templates/item/actions/FireworksUseAction.</summary>
[XmlType("FireworksUseAction")]
public class FireworksUseAction : AbstractItemAction
{
    public override bool CanAct(Aion.GameServer.Model.GameObjects.Player.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        return true;
    }

    public override void Act(Aion.GameServer.Model.GameObjects.Player.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        if (parentItem.GetActivationCount() > 1)
            parentItem.SetActivationCount(parentItem.GetActivationCount() - 1);
        else
            player.GetInventory().DecreaseByObjectId(parentItem.GetObjectId(), 1);

        Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SmItemUsageAnimation(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemTemplate().GetTemplateId(), 0, 1, 0), true);
    }
}
