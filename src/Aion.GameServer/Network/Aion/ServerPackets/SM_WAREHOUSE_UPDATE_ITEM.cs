using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Item;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.Iteminfo;
using ItemBlobType = Aion.GameServer.Network.Aion.Iteminfo.ItemInfoBlob.ItemBlobType;
using ItemUpdateType = Aion.GameServer.Services.Item.ItemPacketService.ItemUpdateType;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_WAREHOUSE_UPDATE_ITEM (kosyachok, -Nemesiss-). Updates a single warehouse item (general-info blob) for a warehouse type. ItemInfoBlob.writeMe(getBuf())->WriteMe(GetBuf()); nested aliases. Item/ItemInfoBlob red-tolerated.</summary>
public class SM_WAREHOUSE_UPDATE_ITEM : AionServerPacket
{
    private Player player;
    private Item item;
    private int warehouseType;
    private ItemUpdateType updateType;

    public SM_WAREHOUSE_UPDATE_ITEM(Player player, Item item, int warehouseType, ItemUpdateType updateType)
    {
        this.player = player;
        this.item = item;
        this.warehouseType = warehouseType;
        this.updateType = updateType;
    }

    protected override void WriteImpl(AionConnection con)
    {
        ItemTemplate itemTemplate = item.GetItemTemplate();

        WriteD(item.GetObjectId());
        WriteC(warehouseType);
        WriteS(itemTemplate.GetL10n());

        ItemInfoBlob itemInfoBlob = new ItemInfoBlob(player, item);
        itemInfoBlob.AddBlobEntry(ItemBlobType.GENERAL_INFO);
        itemInfoBlob.WriteMe(GetBuf());

        if (updateType.IsSendable())
            WriteH(updateType.GetMask());
    }
}
