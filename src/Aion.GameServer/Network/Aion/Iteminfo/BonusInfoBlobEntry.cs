using Aion.GameServer.Commons.Nio;
using Aion.GameServer.Model.Stats.Calc.Functions;
using ItemBlobType = Aion.GameServer.Network.Aion.Iteminfo.ItemInfoBlob.ItemBlobType;

namespace Aion.GameServer.Network.Aion.Iteminfo;

/// <summary>
/// Java parity: network/aion/iteminfo/BonusInfoBlobEntry (Rolandas). StatRateFunction red-tolerated.
/// </summary>
public class BonusInfoBlobEntry : ItemBlobEntry
{
    public BonusInfoBlobEntry() : base(ItemBlobType.STAT_BONUSES)
    {
    }

    public override void WriteThisBlob(ByteBuffer buf)
    {
        WriteH(buf, modifier.GetName().GetItemStoneMask());
        WriteD(buf, modifier.GetValue() * modifier.GetName().GetSign());
        WriteC(buf, modifier is StatRateFunction ? 1 : 0);
    }

    public override int GetSize()
    {
        return 7;
    }
}
