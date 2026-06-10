using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_GAMEGUARD — GameGuard challenge (size + zero-filled buffer).</summary>
public class SM_GAMEGUARD : AionServerPacket
{
    private int size;

    public SM_GAMEGUARD(int size)
    {
        this.size = size;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(size);
        WriteB(new byte[size]);
    }
}
