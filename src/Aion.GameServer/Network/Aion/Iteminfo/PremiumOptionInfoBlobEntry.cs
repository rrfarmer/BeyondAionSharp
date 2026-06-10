using Aion.GameServer.Commons.Nio;
using ItemBlobType = Aion.GameServer.Network.Aion.Iteminfo.ItemInfoBlob.ItemBlobType;

namespace Aion.GameServer.Network.Aion.Iteminfo;

/// <summary>
/// Java parity: network/aion/iteminfo/PremiumOptionInfoBlobEntry (Rolandas).
/// </summary>
public class PremiumOptionInfoBlobEntry : ItemBlobEntry
{
    public PremiumOptionInfoBlobEntry() : base(ItemBlobType.PREMIUM_OPTION)
    {
    }

    public override void WriteThisBlob(ByteBuffer buf)
    {
        WriteC(buf, !ownerItem.IsIdentified() ? -1 : ownerItem.GetBonusStatsId());
        WriteC(buf, !ownerItem.IsIdentified() ? 0 : ownerItem.GetTuneCount());
        WriteC(buf, 0);
    }

    public override int GetSize()
    {
        return 3;
    }
}
