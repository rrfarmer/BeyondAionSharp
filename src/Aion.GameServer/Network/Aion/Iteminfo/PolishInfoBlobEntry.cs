using Aion.GameServer.Commons.Nio;
using Aion.GameServer.Model.Items;
using ItemBlobType = Aion.GameServer.Network.Aion.Iteminfo.ItemInfoBlob.ItemBlobType;

namespace Aion.GameServer.Network.Aion.Iteminfo;

/// <summary>
/// Java parity: network/aion/iteminfo/PolishInfoBlobEntry (Rolandas). IdianStone red-tolerated.
/// </summary>
public class PolishInfoBlobEntry : ItemBlobEntry
{
    internal PolishInfoBlobEntry() : base(ItemBlobType.POLISH_INFO)
    {
    }

    public override void WriteThisBlob(ByteBuffer buf)
    {
        // Idian charge value
        IdianStone stone = ownerItem.GetIdianStone();
        WriteD(buf, stone == null ? 0 : stone.GetPolishCharge());
    }

    public override int GetSize()
    {
        return 4;
    }
}
