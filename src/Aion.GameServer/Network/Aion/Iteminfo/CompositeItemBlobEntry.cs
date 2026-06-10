using System.Collections.Generic;
using Aion.GameServer.Commons.Nio;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Items;
using ItemBlobType = Aion.GameServer.Network.Aion.Iteminfo.ItemInfoBlob.ItemBlobType;

namespace Aion.GameServer.Network.Aion.Iteminfo;

/// <summary>
/// Java parity: network/aion/iteminfo/CompositeItemBlobEntry (-Nemesiss-, Rolandas). Sends info about the item fused with current
/// item. Item/ManaStone red-tolerated.
/// </summary>
public class CompositeItemBlobEntry : ItemBlobEntry
{
    internal CompositeItemBlobEntry() : base(ItemBlobType.COMPOSITE_ITEM)
    {
    }

    public override void WriteThisBlob(ByteBuffer buf)
    {
        WriteD(buf, ownerItem.GetFusionedItemId());
        WriteFusionStones(buf);
        WriteC(buf, ownerItem.GetFusionedItemOptionalSockets()); // additional manastone sockets
        WriteC(buf, ownerItem.GetFusionedItemBonusStatsId());
    }

    private void WriteFusionStones(ByteBuffer buf)
    {
        if (ownerItem.HasFusionStones())
        {
            ISet<ManaStone> itemStones = ownerItem.GetFusionStones();
            Dictionary<int, ManaStone> stonesBySlot = new Dictionary<int, ManaStone>();
            foreach (ManaStone itemStone in itemStones)
            {
                stonesBySlot[itemStone.GetSlot()] = itemStone;
            }
            for (int i = 0; i < Item.MAX_BASIC_STONES; i++)
            {
                stonesBySlot.TryGetValue(i, out ManaStone stone);
                WriteD(buf, stone == null ? 0 : stone.GetItemId());
            }
        }
        else
        {
            Skip(buf, Item.MAX_BASIC_STONES * 4);
        }
    }

    public override int GetSize()
    {
        return Item.MAX_BASIC_STONES * 4 + 6;
    }
}
