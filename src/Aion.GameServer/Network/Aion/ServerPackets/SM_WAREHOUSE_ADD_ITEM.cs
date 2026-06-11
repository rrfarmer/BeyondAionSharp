using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Item;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.Iteminfo;
using ItemAddType = Aion.GameServer.Services.Item.ItemPacketService.ItemAddType;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_WAREHOUSE_ADD_ITEM (kosyachok, -Nemesiss-). Adds an item to a warehouse view (type/addMask + item info blob). Collections.singletonList->new List; getBuf()->GetBuf(); ItemAddType aliased. Item/ItemInfoBlob/ItemPacketService red-tolerated.</summary>
public class SM_WAREHOUSE_ADD_ITEM : AionServerPacket
{
    private int warehouseType;
    private List<Item> items;
    private Player player;
    private ItemAddType addType;

    public SM_WAREHOUSE_ADD_ITEM(Item item, int warehouseType, Player player, ItemAddType addType)
    {
        this.player = player;
        this.warehouseType = warehouseType;
        this.items = new List<Item> { item };
        this.addType = addType;
    }

    public ItemInfoBlob GetFirstItemInfoBlob()
    {
        if (items.Count == 0)
            return null;
        return ItemInfoBlob.GetFullBlob(player, items[0]);
    }

    public Item GetFirstItem()
    {
        return items.Count == 0 ? null : items[0];
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteC(warehouseType);
        WriteH(addType.GetMask());
        WriteH(items.Count);

        foreach (Item item in items)
            WriteItemInfo(item);
    }

    private void WriteItemInfo(Item item)
    {
        ItemTemplate itemTemplate = item.GetItemTemplate();

        WriteD(item.GetObjectId());
        WriteD(itemTemplate.GetTemplateId());
        WriteC(0); // some item info (4 - weapon, 7 - armor, 8 - rings, 17 - bottles)
        WriteS(itemTemplate.GetL10n());

        ItemInfoBlob.GetFullBlob(player, item).WriteMe(GetBuf());

        WriteH((int)(item.GetEquipmentSlot() & 0xFFFF));
    }
}
