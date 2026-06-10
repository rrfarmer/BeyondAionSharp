using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Templates.Item;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.Iteminfo;
using ItemBlobType = Aion.GameServer.Network.Aion.Iteminfo.ItemInfoBlob.ItemBlobType;
using ItemUpdateType = Aion.GameServer.Services.Item.ItemPacketService.ItemUpdateType;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_INVENTORY_UPDATE_ITEM (ATracer, -Nemesiss-). Updates a single inventory item with the blob matching the update type (equipped slot / conditioning / polish / full). Converges ItemActionService/ItemChargeService. switch-on-ItemUpdateType; ItemInfoBlob.writeMe(getBuf())->WriteMe(GetBuf()); nested aliases. Item/ItemInfoBlob red-tolerated.</summary>
public class SM_INVENTORY_UPDATE_ITEM : AionServerPacket
{
    private readonly Player player;
    private readonly Item item;
    private readonly ItemUpdateType updateType;

    public SM_INVENTORY_UPDATE_ITEM(Player player, Item item)
        : this(player, item, ItemUpdateType.DEC_ITEM_USE)
    {
    }

    public SM_INVENTORY_UPDATE_ITEM(Player player, Item item, ItemUpdateType updateType)
    {
        this.player = player;
        this.item = item;
        this.updateType = updateType;
    }

    protected override void WriteImpl(AionConnection con)
    {
        ItemTemplate itemTemplate = item.GetItemTemplate();

        WriteD(item.GetObjectId());
        WriteS(itemTemplate.GetL10n());

        ItemInfoBlob itemInfoBlob;
        switch (updateType)
        {
            case ItemUpdateType.EQUIP_UNEQUIP:
                itemInfoBlob = new ItemInfoBlob(player, item);
                itemInfoBlob.AddBlobEntry(ItemBlobType.EQUIPPED_SLOT);
                break;
            case ItemUpdateType.CHARGE:
                itemInfoBlob = new ItemInfoBlob(player, item);
                itemInfoBlob.AddBlobEntry(ItemBlobType.CONDITIONING_INFO);
                break;
            case ItemUpdateType.POLISH_CHARGE:
                itemInfoBlob = new ItemInfoBlob(player, item);
                itemInfoBlob.AddBlobEntry(ItemBlobType.POLISH_INFO);
                break;
            default:
                itemInfoBlob = ItemInfoBlob.GetFullBlob(player, item);
                break;
        }
        itemInfoBlob.WriteMe(GetBuf());

        if (updateType.IsSendable())
            WriteH(updateType.GetMask());
    }
}
