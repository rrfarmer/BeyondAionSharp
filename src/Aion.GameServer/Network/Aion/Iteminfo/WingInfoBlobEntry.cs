using Aion.Commons.Nio;
using Aion.GameServer.Model.Items;
using ItemBlobType = global::Aion.GameServer.Network.Aion.Iteminfo.ItemInfoBlob.ItemBlobType;

namespace Aion.GameServer.Network.Aion.Iteminfo;

/// <summary>
/// Java parity: network/aion/iteminfo/WingInfoBlobEntry (-Nemesiss-, Rolandas). Sent for clothes. Keeps info about slots that cloth
/// can be equipped to. ItemSlot red-tolerated.
/// </summary>
public class WingInfoBlobEntry : ItemBlobEntry
{
    internal WingInfoBlobEntry() : base(ItemBlobType.SLOTS_WING)
    {
    }

    public override void WriteThisBlob(ByteBuffer buf)
    {
        WriteQ(buf, ItemSlotExtensions.GetSlotFor(ownerItem.GetItemTemplate().GetItemSlot()).GetSlotIdMask());
        WriteQ(buf, 0); // no secondary slot
    }

    public override int GetSize()
    {
        return 16;
    }
}
