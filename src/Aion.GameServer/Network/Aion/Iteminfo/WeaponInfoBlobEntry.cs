using Aion.Commons.Nio;
using Aion.GameServer.Model.Items;
using ItemBlobType = Aion.GameServer.Network.Aion.Iteminfo.ItemInfoBlob.ItemBlobType;

namespace Aion.GameServer.Network.Aion.Iteminfo;

/// <summary>
/// Java parity: network/aion/iteminfo/WeaponInfoBlobEntry (-Nemesiss-, Rolandas). Sent for weapons. Keeps info about slots that
/// weapon can be equipped to. ItemSlot red-tolerated.
/// </summary>
public class WeaponInfoBlobEntry : ItemBlobEntry
{
    internal WeaponInfoBlobEntry() : base(ItemBlobType.SLOTS_WEAPON)
    {
    }

    public override void WriteThisBlob(ByteBuffer buf)
    {
        ItemSlot[] slots = ItemSlot.GetSlotsFor(ownerItem.GetItemTemplate().GetItemSlot());
        if (slots.Length == 1)
        {
            WriteQ(buf, slots[0].GetSlotIdMask());
            WriteQ(buf, ownerItem.HasFusionedItem() ? 0x00 : 0x02);
            return;
        }
        if (ownerItem.GetItemTemplate().IsTwoHandWeapon())
        {
            // must occupy two slots
            WriteQ(buf, slots[0].GetSlotIdMask() | slots[1].GetSlotIdMask());
            WriteQ(buf, 0);
        }
        else
        {
            // primary and secondary slots
            WriteQ(buf, slots[0].GetSlotIdMask());
            WriteQ(buf, slots[1].GetSlotIdMask());
        }
    }

    public override int GetSize()
    {
        return 16;
    }
}
