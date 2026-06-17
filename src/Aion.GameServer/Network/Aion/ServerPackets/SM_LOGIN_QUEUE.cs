using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_LOGIN_QUEUE (Simple). Login queue position/time/count (fixed sample values).</summary>
public class SM_LOGIN_QUEUE : AionServerPacket
{
    private int waitingPosition; // What is the player's position in line
    private int waitingTime; // Per waiting position in seconds
    private int waitingCount; // How many are waiting in line

    private SM_LOGIN_QUEUE()
    {
        this.waitingPosition = 5;
        this.waitingTime = 60;
        this.waitingCount = 50;
    }

    // Java parity (writeImpl golden'd byte-exact vs game-server/.../SM_LOGIN_QUEUE.java): 2026-06-17 batch 23
    protected override void WriteImpl(AionConnection con)
    {
        WriteD(waitingPosition);
        WriteD(waitingTime);
        WriteD(waitingCount);
    }
}
