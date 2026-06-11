using Aion.Commons.Nio;
using Aion.GameServer.Model.Items;
using ItemBlobType = Aion.GameServer.Network.Aion.Iteminfo.ItemInfoBlob.ItemBlobType;

namespace Aion.GameServer.Network.Aion.Iteminfo;

/// <summary>
/// Java parity: network/aion/iteminfo/AccessoryInfoBlobEntry (-Nemesiss-, Rolandas). Sent for accessory items (ring, earring,
/// waist). Keeps info about slots that item can be equipped to. ItemSlot red-tolerated.
/// </summary>
public class AccessoryInfoBlobEntry : ItemBlobEntry
{
    internal AccessoryInfoBlobEntry() : base(ItemBlobType.SLOTS_ACCESSORY)
    {
    }

    public override void WriteThisBlob(ByteBuffer buf)
    {
        ItemSlot[] slots = ItemSlot.GetSlotsFor(ownerItem.GetItemTemplate().GetItemSlot());
        WriteQ(buf, slots[0].GetSlotIdMask());
        WriteQ(buf, slots.Length > 1 ? slots[1].GetSlotIdMask() : 0);
    }

    public override int GetSize()
    {
        return 16;
    }
}
