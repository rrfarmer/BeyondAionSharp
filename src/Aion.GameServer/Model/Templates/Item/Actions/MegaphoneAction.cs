using System;
using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.Templates.Items.Actions;

/// <summary>Java parity: model/templates/item/actions/MegaphoneAction.</summary>
[XmlType("MegaphoneAction")]
public class MegaphoneAction : AbstractItemAction
{
    [XmlAttribute("color")] protected string color;

    public int GetColor()
    {
        int rgb = Convert.ToInt32(color, 16);
        return rgb;
    }

    public override bool CanAct(Aion.GameServer.Model.GameObjects.Players.Player player, Item item, Item targetItem, params object[] @params)
    {
        return true;
    }

    public override void Act(Aion.GameServer.Model.GameObjects.Players.Player player, Item item, Item targetItem, params object[] @params)
    {
        string message = (string)@params[0];
        Aion.GameServer.Model.Templates.Items.ItemTemplate itemTemplate = item.GetItemTemplate();
        Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SmItemUsageAnimation(player.GetObjectId(), item.GetObjectId(), itemTemplate.GetTemplateId()), true);
        Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_USE_ITEM(item.GetL10n()));
        player.GetInventory().DecreaseByObjectId(item.GetObjectId(), 1);
        Aion.GameServer.Utils.PacketSendUtility.BroadcastToWorld(new Aion.GameServer.Network.Aion.ServerPackets.SM_MEGAPHONE(player, message, item.GetItemId()));
    }
}
