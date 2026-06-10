using Aion.GameServer.Commons.Nio;
using ItemBlobType = Aion.GameServer.Network.Aion.Iteminfo.ItemInfoBlob.ItemBlobType;

namespace Aion.GameServer.Network.Aion.Iteminfo;

/// <summary>
/// Java parity: network/aion/iteminfo/EquippedSlotBlobEntry (-Nemesiss-, Rolandas). Sent for all equipable items. If equipped, says
/// to which slot; otherwise 0.
/// </summary>
public class EquippedSlotBlobEntry : ItemBlobEntry
{
    internal EquippedSlotBlobEntry() : base(ItemBlobType.EQUIPPED_SLOT)
    {
    }

    public override void WriteThisBlob(ByteBuffer buf)
    {
        WriteQ(buf, ownerItem.IsEquipped() ? ownerItem.GetEquipmentSlot() : 0);
    }

    public override int GetSize()
    {
        return 8;
    }
}
