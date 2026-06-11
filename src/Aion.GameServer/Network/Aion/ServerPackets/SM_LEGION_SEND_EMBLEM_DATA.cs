using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_LEGION_SEND_EMBLEM_DATA (cura). Streams legion emblem byte data (size + bytes), following SM_LEGION_SEND_EMBLEM.</summary>
public class SM_LEGION_SEND_EMBLEM_DATA : AionServerPacket
{
    private int size;
    private byte[] data;

    public SM_LEGION_SEND_EMBLEM_DATA(int size, byte[] data)
    {
        this.size = size;
        this.data = data;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(size);
        WriteB(data);
    }
}
