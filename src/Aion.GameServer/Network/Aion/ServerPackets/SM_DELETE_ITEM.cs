using Aion.GameServer.Network.Aion;
using ItemDeleteType = Aion.GameServer.Services.Items.ItemPacketService.ItemDeleteType;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_DELETE_ITEM (Avol). Removes an item from the client inventory view with a delete-type mask. Converges MailService. ItemDeleteType nested alias; getMask()->GetMask(). AionServerPacket red-tolerated.</summary>
public class SM_DELETE_ITEM : AionServerPacket
{
    private readonly int itemObjectId;
    private readonly ItemDeleteType deleteType;

    public SM_DELETE_ITEM(int itemObjectId)
        : this(itemObjectId, ItemDeleteType.DEFAULT)
    {
    }

    public SM_DELETE_ITEM(int itemObjectId, ItemDeleteType deleteType)
    {
        this.itemObjectId = itemObjectId;
        this.deleteType = deleteType;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(itemObjectId);
        WriteC(deleteType.GetMask());
    }
}
