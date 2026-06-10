using Aion.GameServer.Model.Siege;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_SIEGE_LOCATION_STATE (Source). Siege location vulnerability state (locationId + state). SiegeLocation red-tolerated.</summary>
public class SM_SIEGE_LOCATION_STATE : AionServerPacket
{
    private int locationId;
    private int state;

    public SM_SIEGE_LOCATION_STATE(SiegeLocation location)
    {
        this.locationId = location.GetLocationId();
        this.state = location.IsVulnerable() ? 1 : 0;
    }

    public SM_SIEGE_LOCATION_STATE(int locationId, int state)
    {
        this.locationId = locationId;
        this.state = state;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(locationId);
        WriteC(state);
    }
}
