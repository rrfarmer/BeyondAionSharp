using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.Templates.Item.Actions;

/// <summary>Java parity: model/templates/item/actions/ExpandInventoryAction.</summary>
[XmlType("ExpandInventoryAction")]
public class ExpandInventoryAction : AbstractItemAction
{
    [XmlAttribute("level")] private int level;
    [XmlAttribute("storage")] private StorageType storage;

    public override bool CanAct(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        switch (storage)
        {
            case StorageType.CUBE:
                return Aion.GameServer.Services.CubeExpandService.CanExpandByTicket(player, level);
            case StorageType.WAREHOUSE:
                return Aion.GameServer.Services.WarehouseService.CanExpandByTicket(player, level);
        }
        return false;
    }

    public override void Act(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        if (!player.GetInventory().DecreaseByObjectId(parentItem.GetObjectId(), 1))
            return;
        Aion.GameServer.Model.Templates.Item.ItemTemplate itemTemplate = parentItem.GetItemTemplate();
        Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player,
            new Aion.GameServer.Network.Aion.ServerPackets.SmItemUsageAnimation(player.GetObjectId(), parentItem.GetObjectId(), itemTemplate.GetTemplateId()), true);

        switch (storage)
        {
            case StorageType.CUBE:
                Aion.GameServer.Services.CubeExpandService.ItemExpand(player);
                break;
            case StorageType.WAREHOUSE:
                Aion.GameServer.Services.WarehouseService.Expand(player, false);
                break;
        }
    }
}

// Java parity: package-private enum StorageType in ExpandInventoryAction.java (distinct from items.storage.StorageType).
internal enum StorageType
{
    CUBE,
    WAREHOUSE,
}
