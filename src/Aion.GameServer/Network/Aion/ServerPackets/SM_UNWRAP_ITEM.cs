using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_UNWRAP_ITEM (xTz). Unwrap result (item objId + count).</summary>
public class SM_UNWRAP_ITEM : AionServerPacket
{
    private readonly int objectId, count;

    public SM_UNWRAP_ITEM(int objectId, int count)
    {
        this.objectId = objectId;
        this.count = count;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(objectId);
        WriteC(count);
    }
}
