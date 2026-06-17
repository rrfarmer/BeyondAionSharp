using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.Templates.Items.Actions;

/// <summary>Java parity: model/templates/item/actions/ExpandInventoryAction.</summary>
[XmlType("ExpandInventoryAction")]
public class ExpandInventoryAction : AbstractItemAction
{
    [XmlAttribute("level")] public int level;
    [XmlAttribute("storage")] public StorageType storage;

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
        Aion.GameServer.Model.Templates.Items.ItemTemplate itemTemplate = parentItem.GetItemTemplate();
        Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player,
            new Aion.GameServer.Network.Aion.ServerPackets.SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), parentItem.GetObjectId(), itemTemplate.GetTemplateId()), true);

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
// Public so the now-public [XmlAttribute] storage field (XmlSerializer binds public members) can reference it.
public enum StorageType
{
    CUBE,
    WAREHOUSE,
}
