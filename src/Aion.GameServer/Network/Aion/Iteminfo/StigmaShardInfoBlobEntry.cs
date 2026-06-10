using Aion.GameServer.Commons.Nio;
using ItemBlobType = Aion.GameServer.Network.Aion.Iteminfo.ItemInfoBlob.ItemBlobType;

namespace Aion.GameServer.Network.Aion.Iteminfo;

/// <summary>
/// Java parity: network/aion/iteminfo/StigmaShardInfoBlobEntry (Rolandas).
/// </summary>
public class StigmaShardInfoBlobEntry : ItemBlobEntry
{
    public StigmaShardInfoBlobEntry() : base(ItemBlobType.STIGMA_SHARD)
    {
    }

    public override void WriteThisBlob(ByteBuffer buf)
    {
        WriteD(buf, 0);
    }

    public override int GetSize()
    {
        return 4;
    }
}
