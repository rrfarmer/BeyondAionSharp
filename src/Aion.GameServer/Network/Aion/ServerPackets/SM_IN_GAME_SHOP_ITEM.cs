using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Ingameshop;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_IN_GAME_SHOP_ITEM (xTz, KID). In-game shop item detail (price/itemId/count/gift/type/descriptions). IGItem/InGameShopEn red-tolerated.</summary>
public class SM_IN_GAME_SHOP_ITEM : AionServerPacket
{
    private IGItem item;

    public SM_IN_GAME_SHOP_ITEM(Player player, int objectItem)
    {
        item = InGameShopEn.GetInstance().GetIGItem(objectItem);
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(item.GetObjectId()); // nrItem
        WriteQ(item.GetItemPrice()); // price
        WriteH(0);
        WriteD(item.GetItemId()); // itemId
        WriteQ(item.GetItemCount()); // itemCount
        WriteD(0); // unk
        WriteD(item.GetGift()); // gift 0, 1
        WriteD(item.GetItemType()); // item type 0, 1, 2
        WriteD(0); // unk
        WriteC(0); // unk
        WriteH(0); // unk
        WriteS(item.GetTitleDescription());
        WriteS(item.GetItemDescription());
    }
}
