using Aion.GameServer.Commons.Nio;
using Aion.GameServer.Model.Items;
using ItemBlobType = Aion.GameServer.Network.Aion.Iteminfo.ItemInfoBlob.ItemBlobType;

namespace Aion.GameServer.Network.Aion.Iteminfo;

/// <summary>
/// Java parity: network/aion/iteminfo/ShieldInfoBlobEntry (-Nemesiss-, Rolandas). Sent for shields. Keeps info about slots that
/// shield can be equipped to. ItemSlot red-tolerated.
/// </summary>
public class ShieldInfoBlobEntry : ItemBlobEntry
{
    internal ShieldInfoBlobEntry() : base(ItemBlobType.SLOTS_SHIELD)
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
