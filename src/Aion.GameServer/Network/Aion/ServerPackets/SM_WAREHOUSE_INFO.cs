using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items.Storage;
using Aion.GameServer.Model.Templates.Item;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.Iteminfo;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_WAREHOUSE_INFO (kosyachok). Warehouse contents (type/firstPacket/expand + per-item info blob). Collections.emptyList->new List; getBuf()->GetBuf(); StorageType.REGULAR_WAREHOUSE.getId(). Item/ItemInfoBlob/StorageType red-tolerated.</summary>
public class SM_WAREHOUSE_INFO : AionServerPacket
{
    private int warehouseType;
    private ICollection<Item> itemList;
    private bool firstPacket;
    private int expandLvl;
    private Player player;

    public SM_WAREHOUSE_INFO(ICollection<Item> items, int warehouseType, int expandLvl, bool firstPacket, Player player)
    {
        this.warehouseType = warehouseType;
        this.expandLvl = expandLvl;
        this.firstPacket = firstPacket;
        if (items == null)
            this.itemList = new List<Item>();
        else
            this.itemList = items;
        this.player = player;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteC(warehouseType);
        WriteC(firstPacket ? 1 : 0);
        WriteC(expandLvl); // warehouse expand (0 - 9)
        if (warehouseType == StorageType.REGULAR_WAREHOUSE.GetId() && itemList.Count > 0)
        {
            WriteC(1);
            WriteC(0); // unk, seen value 0x02
        }
        else
        {
            WriteH(0);
        }
        WriteH(itemList.Count);
        foreach (Item item in itemList)
            WriteItemInfo(item);
    }

    private void WriteItemInfo(Item item)
    {
        ItemTemplate itemTemplate = item.GetItemTemplate();

        WriteD(item.GetObjectId());
        WriteD(itemTemplate.GetTemplateId());
        WriteC(0); // some item info (4 - weapon, 7 - armor, 8 - rings, 17 - bottles)
        WriteS(itemTemplate.GetL10n());

        ItemInfoBlob itemInfoBlob = ItemInfoBlob.GetFullBlob(player, item);
        itemInfoBlob.WriteMe(GetBuf());

        WriteH((int)(item.GetEquipmentSlot() & 0xFFFF));
    }
}
