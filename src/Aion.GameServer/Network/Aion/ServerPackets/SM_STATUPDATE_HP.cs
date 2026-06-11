using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_STATUPDATE_HP (Luno). Updates current/max HP.</summary>
public class SM_STATUPDATE_HP : AionServerPacket
{
    private int currentHp;
    private int maxHp;

    public SM_STATUPDATE_HP(int currentHp, int maxHp)
    {
        this.currentHp = currentHp;
        this.maxHp = maxHp;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(currentHp);
        WriteD(maxHp);
    }
}
