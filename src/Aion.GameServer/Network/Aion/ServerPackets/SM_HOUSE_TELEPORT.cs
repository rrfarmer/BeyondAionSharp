using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_HOUSE_TELEPORT (Rolandas). Teleports to a house (address + playerId).</summary>
public class SM_HOUSE_TELEPORT : AionServerPacket
{
    private int address;
    private int playerId;

    public SM_HOUSE_TELEPORT(int houseAddress, int playerId)
    {
        this.address = houseAddress;
        this.playerId = playerId;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(address);
        WriteD(playerId);
    }
}
