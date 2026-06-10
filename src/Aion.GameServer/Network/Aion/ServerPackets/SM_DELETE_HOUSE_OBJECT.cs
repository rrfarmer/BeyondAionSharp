using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_DELETE_HOUSE_OBJECT (Rolandas). Removes a placed house object (item objId).</summary>
public class SM_DELETE_HOUSE_OBJECT : AionServerPacket
{
    private int itemObjectId;

    public SM_DELETE_HOUSE_OBJECT(int itemObjectId)
    {
        this.itemObjectId = itemObjectId;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(itemObjectId);
    }
}
