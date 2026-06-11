using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Trade;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.Iteminfo;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_PRIVATE_STORE (Simple). Sends a private store's sold items (objId/itemId/count/price + full ItemInfoBlob). (int) count cast; getBuf()->GetBuf(); Map.values iteration. PrivateStore/TradePSItem/ItemInfoBlob red-tolerated.</summary>
public class SM_PRIVATE_STORE : AionServerPacket
{
    private Player player;
    private PrivateStore store;

    public SM_PRIVATE_STORE(PrivateStore store, Player player)
    {
        this.player = player;
        this.store = store;
    }

    protected override void WriteImpl(AionConnection con)
    {
        if (store != null)
        {
            Player seller = store.GetOwner();
            IDictionary<int, TradePSItem> soldItems = store.GetSoldItems();

            WriteD(seller.GetObjectId());
            WriteH(soldItems.Count);
            foreach (TradePSItem tradeItem in soldItems.Values)
            {
                WriteD(tradeItem.GetItemObjId());
                WriteD(tradeItem.GetItemId());
                WriteH((int)tradeItem.GetCount());
                WriteQ(tradeItem.GetPrice());
                ItemInfoBlob.GetFullBlob(player, seller.GetInventory().GetItemByObjId(tradeItem.GetItemObjId())).WriteMe(GetBuf());
            }
        }
    }
}
