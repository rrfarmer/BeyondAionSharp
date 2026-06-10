using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_RECONNECT_KEY (-Nemesiss-). Response for CM_RECONNECT_AUTH with the LoginServer reconnection key.</summary>
public class SM_RECONNECT_KEY : AionServerPacket
{
    /// <summary>key for reconnection - will be used for authentication</summary>
    private readonly int key;

    public SM_RECONNECT_KEY(int key)
    {
        this.key = key;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteC(0x00);
        WriteD(key);
    }
}
