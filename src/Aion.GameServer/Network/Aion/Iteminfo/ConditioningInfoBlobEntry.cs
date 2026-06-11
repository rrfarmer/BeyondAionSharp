using Aion.Commons.Nio;
using ItemBlobType = Aion.GameServer.Network.Aion.Iteminfo.ItemInfoBlob.ItemBlobType;

namespace Aion.GameServer.Network.Aion.Iteminfo;

/// <summary>
/// Java parity: network/aion/iteminfo/ConditioningInfoBlobEntry (-Nemesiss-, Rolandas). Sends info about conditioning.
/// </summary>
public class ConditioningInfoBlobEntry : ItemBlobEntry
{
    internal ConditioningInfoBlobEntry() : base(ItemBlobType.CONDITIONING_INFO)
    {
    }

    public override void WriteThisBlob(ByteBuffer buf)
    {
        WriteD(buf, ownerItem.GetChargePoints());
    }

    public override int GetSize()
    {
        return 4;
    }
}
