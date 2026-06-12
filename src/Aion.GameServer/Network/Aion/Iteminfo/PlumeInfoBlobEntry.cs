using Aion.Commons.Nio;
using Aion.GameServer.Model.Items;
using ItemBlobType = global::Aion.GameServer.Network.Aion.Iteminfo.ItemInfoBlob.ItemBlobType;

namespace Aion.GameServer.Network.Aion.Iteminfo;

/// <summary>
/// Java parity: network/aion/iteminfo/PlumeInfoBlobEntry (Rolandas). ItemSlot red-tolerated.
/// </summary>
public class PlumeInfoBlobEntry : ItemBlobEntry
{
    internal PlumeInfoBlobEntry() : base(ItemBlobType.PLUME_INFO)
    {
    }

    public override void WriteThisBlob(ByteBuffer buf)
    {
        WriteQ(buf, ItemSlotExtensions.GetSlotFor(ownerItem.GetItemTemplate().GetItemSlot()).GetSlotIdMask());
        WriteQ(buf, 0x100000); // secondary slot ?
        WriteD(buf, 0); // unks
        WriteD(buf, 0);
        WriteD(buf, 0);
        WriteD(buf, 0);
    }

    public override int GetSize()
    {
        return 32;
    }
}
