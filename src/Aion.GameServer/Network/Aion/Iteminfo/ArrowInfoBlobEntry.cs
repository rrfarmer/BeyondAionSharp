using Aion.Commons.Nio;
using Aion.GameServer.Model.Items;
using ItemBlobType = Aion.GameServer.Network.Aion.Iteminfo.ItemInfoBlob.ItemBlobType;

namespace Aion.GameServer.Network.Aion.Iteminfo;

/// <summary>
/// Java parity: network/aion/iteminfo/ArrowInfoBlobEntry (Rolandas). ItemSlot red-tolerated.
/// </summary>
public class ArrowInfoBlobEntry : ItemBlobEntry
{
    public ArrowInfoBlobEntry() : base(ItemBlobType.SLOTS_ARROW)
    {
    }

    public override void WriteThisBlob(ByteBuffer buf)
    {
        WriteQ(buf, ItemSlot.GetSlotFor(ownerItem.GetItemTemplate().GetItemSlot()).GetSlotIdMask());
        WriteQ(buf, 0);
    }

    public override int GetSize()
    {
        return 8;
    }
}
