using Aion.GameServer.Commons.Nio;
using Aion.GameServer.Dataholders;
using ItemBlobType = Aion.GameServer.Network.Aion.Iteminfo.ItemInfoBlob.ItemBlobType;

namespace Aion.GameServer.Network.Aion.Iteminfo;

/// <summary>
/// Java parity: network/aion/iteminfo/GeneralInfoBlobEntry (-Nemesiss-, Rolandas). Sent with ALL items. First and only block for
/// non-equipable items, last blob for equipable items. DataManager red-tolerated.
/// </summary>
public class GeneralInfoBlobEntry : ItemBlobEntry
{
    internal GeneralInfoBlobEntry() : base(ItemBlobType.GENERAL_INFO)
    {
    }

    public override void WriteThisBlob(ByteBuffer buf)
    {
        // TODO what with kinah?
        WriteH(buf, ownerItem.GetItemMask());
        WriteQ(buf, ownerItem.GetItemCount());
        WriteS(buf, ownerItem.GetItemCreator()); // Creator name
        WriteC(buf, 0);
        WriteD(buf, ownerItem.SecondsUntilExpiration()); // Disappear time
        WriteD(buf, 0);
        WriteD(buf, ownerItem.GetTemporaryExchangeTimeRemaining());
        WriteH(buf, DataManager.ITEM_CLEAN_UP.HasAccountOrLegionWhStorabilityDisabled(ownerItem.GetItemId()) ? 3 : 0); // TODO: Item Sealing - 1=sealed, 2=unsealing state, 3=special sealed(gm), 4=special unsealing(gm)
        WriteD(buf, 0); // Remaining unsealing time
        WriteH(buf, 18); // unk 4.7.5
    }

    public override int GetSize()
    {
        return 29 + ownerItem.GetItemCreator().Length * 2 + 4;
    }
}
