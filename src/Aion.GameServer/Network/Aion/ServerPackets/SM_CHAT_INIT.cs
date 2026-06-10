using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_CHAT_INIT (ATracer). Sends the chat-server auth token (length + bytes).</summary>
public class SM_CHAT_INIT : AionServerPacket
{
    private byte[] token;

    public SM_CHAT_INIT(byte[] token)
    {
        this.token = token;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(token.Length);
        WriteB(token);
    }
}
