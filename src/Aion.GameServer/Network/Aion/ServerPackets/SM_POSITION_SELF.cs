using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_POSITION_SELF (cura). Instantly moves the player to a position (x/y/z + heading); client replies with CM_POSITION_SELF.</summary>
public class SM_POSITION_SELF : AionServerPacket
{
    private readonly float x, y, z;
    private readonly byte heading;

    public SM_POSITION_SELF(float x, float y, float z, byte heading)
    {
        this.x = x;
        this.y = y;
        this.z = z;
        this.heading = heading;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteF(x);
        WriteF(y);
        WriteF(z);
        WriteC(heading);
    }
}
