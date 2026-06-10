using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_DELETE_HOUSE (Rolandas). Removes a house from the map (address).</summary>
public class SM_DELETE_HOUSE : AionServerPacket
{
    private int address;

    public SM_DELETE_HOUSE(int address)
    {
        this.address = address;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(address);
    }
}
