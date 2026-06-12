using Aion.Commons.Nio;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Calc.Functions;
using ItemBlobType = global::Aion.GameServer.Network.Aion.Iteminfo.ItemInfoBlob.ItemBlobType;

namespace Aion.GameServer.Network.Aion.Iteminfo;

/// <summary>
/// Java parity: network/aion/iteminfo/ItemBlobEntry (-Nemesiss-, Rolandas). ItemInfo blob entry (contains detailed item info).
/// Client does have blob tree as implemented, it contains sequence of blobs. Extends PacketWriteHelper. Package-private fields
/// (owner/ownerItem/modifier) -> protected for subclass access. Item/IStatFunction red-tolerated.
/// </summary>
public abstract class ItemBlobEntry : PacketWriteHelper
{
    private readonly ItemBlobType type;
    protected internal Player owner;
    protected internal Item ownerItem;
    protected internal IStatFunction modifier;

    protected ItemBlobEntry(ItemBlobType type)
    {
        this.type = type;
    }

    public ItemBlobType GetItemBlobType()
    {
        return type;
    }

    internal void SetOwner(Player owner, Item item, IStatFunction modifier)
    {
        this.owner = owner;
        this.ownerItem = item;
        this.modifier = modifier;
    }

    protected override void WriteMe(ByteBuffer buf)
    {
        WriteC(buf, type.GetEntryId());
        WriteThisBlob(buf);
    }

    /// <summary>Java parity: same-package call of protected writeMe(buf) from ItemInfoBlob; C# needs an internal forwarder.</summary>
    internal void WriteMeInternal(ByteBuffer buf)
    {
        WriteMe(buf);
    }

    public abstract void WriteThisBlob(ByteBuffer buf);

    public abstract int GetSize();
}
