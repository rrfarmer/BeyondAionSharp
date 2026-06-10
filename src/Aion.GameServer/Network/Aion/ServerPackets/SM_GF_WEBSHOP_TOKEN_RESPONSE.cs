using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_GF_WEBSHOP_TOKEN_RESPONSE (Artur). Sends a webshop token (fixed 32-char string).</summary>
public class SM_GF_WEBSHOP_TOKEN_RESPONSE : AionServerPacket
{
    private readonly string token;

    public SM_GF_WEBSHOP_TOKEN_RESPONSE(string token)
    {
        this.token = token;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteS(token, 32);
    }
}
