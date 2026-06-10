using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_SECURITY_TOKEN (ginho1). Sends the security token (region byte + token + zero-padding of equal length).</summary>
public class SM_SECURITY_TOKEN : AionServerPacket
{
    private byte[] token;

    public SM_SECURITY_TOKEN(byte[] token)
    {
        this.token = token;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteC(0x0);// NA(0),EU(3)
        WriteB(token);
        WriteB(new byte[token.Length]);
    }
}
