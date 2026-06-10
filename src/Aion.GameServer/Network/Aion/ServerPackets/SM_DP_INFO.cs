using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_DP_INFO (Sweetkr). Sends a player's current DP.</summary>
public class SM_DP_INFO : AionServerPacket
{
    private int playerObjectId;
    private int currentDp;

    public SM_DP_INFO(int playerObjectId, int currentDp)
    {
        this.playerObjectId = playerObjectId;
        this.currentDp = currentDp;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(playerObjectId);
        WriteH(currentDp);
    }
}
