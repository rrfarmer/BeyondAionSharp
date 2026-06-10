using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_ALLIANCE_READY_CHECK (Sarynth, Rhys2002). Alliance ready-check status (player objId + status code).</summary>
public class SM_ALLIANCE_READY_CHECK : AionServerPacket
{
    private int playerObjectId;
    private int statusCode;

    public SM_ALLIANCE_READY_CHECK(int playerObjectId, int statusCode)
    {
        this.playerObjectId = playerObjectId;
        this.statusCode = statusCode;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(playerObjectId);
        WriteC(statusCode);
    }
}
