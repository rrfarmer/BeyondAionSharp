using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items.Storage;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.Iteminfo;
using Aion.GameServer.Services.Items;
using ItemAddType = global::Aion.GameServer.Services.Items.ItemPacketService.ItemAddType;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_INVENTORY_ADD_ITEM (ATracer). Adds items to the inventory view (mask + per-item info blob). getBuf()->GetBuf(); ItemAddType aliased from ItemPacketService. Item/ItemInfoBlob/ItemStorage red-tolerated.</summary>
public class SM_INVENTORY_ADD_ITEM : AionServerPacket
{
    private readonly List<Item> items;
    private Player player;
    private ItemAddType addType;

    public SM_INVENTORY_ADD_ITEM(List<Item> items, Player player, ItemAddType addType)
    {
        this.player = player;
        this.items = items;
        this.addType = addType;
    }

    protected override void WriteImpl(AionConnection con)
    {
        // TODO: Rework it, who knows where it could be bugged else.
        int mask = addType.GetMask();
        if (addType == ItemAddType.ITEM_COLLECT)
        {
            // TODO: if size != 1, then it's buy item, should not specify any slot in other places then !!!
            if (items.Count == 1 && items[0].GetEquipmentSlot() != ItemStorage.FIRST_AVAILABLE_SLOT)
                mask = ItemAddType.PARTIAL_WITH_SLOT.GetMask();
        }
        WriteH(mask); //
        WriteH(items.Count); // number of entries
        foreach (Item item in items)
            WriteItemInfo(item);
    }

    private void WriteItemInfo(Item item)
    {
        ItemTemplate itemTemplate = item.GetItemTemplate();

        WriteD(item.GetObjectId());
        WriteD(itemTemplate.GetTemplateId());
        WriteS(itemTemplate.GetL10n());

        ItemInfoBlob.GetFullBlob(player, item).WriteMe(GetBuf());

        WriteH((int)(item.GetEquipmentSlot() & 0xFFFF));
        WriteC(item.GetItemTemplate().IsCloth() ? 1 : 0);
    }
}
