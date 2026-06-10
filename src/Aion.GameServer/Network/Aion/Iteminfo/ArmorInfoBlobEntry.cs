using Aion.GameServer.Commons.Nio;
using Aion.GameServer.Model.Items;
using ItemBlobType = Aion.GameServer.Network.Aion.Iteminfo.ItemInfoBlob.ItemBlobType;

namespace Aion.GameServer.Network.Aion.Iteminfo;

/// <summary>
/// Java parity: network/aion/iteminfo/ArmorInfoBlobEntry (-Nemesiss-, Rolandas). Sent for armors. Keeps info about slots that armor
/// can be equipped to. ItemSlot red-tolerated.
/// </summary>
public class ArmorInfoBlobEntry : ItemBlobEntry
{
    internal ArmorInfoBlobEntry() : base(ItemBlobType.SLOTS_ARMOR)
    {
    }

    public override void WriteThisBlob(ByteBuffer buf)
    {
        WriteQ(buf, ItemSlot.GetSlotFor(ownerItem.GetItemTemplate().GetItemSlot()).GetSlotIdMask());
        WriteQ(buf, 0); // TODO! secondary slot?
        WriteDyeInfo(buf, ownerItem.GetItemColor()); // 4 bytes
    }

    public override int GetSize()
    {
        return 20;
    }
}
