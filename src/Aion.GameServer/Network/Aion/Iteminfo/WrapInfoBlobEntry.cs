using Aion.Commons.Nio;
using ItemBlobType = Aion.GameServer.Network.Aion.Iteminfo.ItemInfoBlob.ItemBlobType;

namespace Aion.GameServer.Network.Aion.Iteminfo;

/// <summary>
/// Java parity: network/aion/iteminfo/WrapInfoBlobEntry (Rolandas). Written last, after all BonusInfoBlobEntry.
/// </summary>
public class WrapInfoBlobEntry : ItemBlobEntry
{
    internal WrapInfoBlobEntry() : base(ItemBlobType.WRAP_INFO)
    {
    }

    public override void WriteThisBlob(ByteBuffer buf)
    {
        WriteC(buf, ownerItem.GetPackCount());
    }

    public override int GetSize()
    {
        return 1;
    }
}
