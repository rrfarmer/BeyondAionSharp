using System.Collections.Generic;
using Aion.GameServer.Model.Siege;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_SHIELD_EFFECT (xTz, Source). Sends siege-location shield state (locationId + underShield flag). SiegeLocation/SiegeService red-tolerated.</summary>
public class SM_SHIELD_EFFECT : AionServerPacket
{
    private ICollection<SiegeLocation> locations;

    public SM_SHIELD_EFFECT(ICollection<SiegeLocation> locations)
    {
        this.locations = locations;
    }

    public SM_SHIELD_EFFECT(int location)
    {
        this.locations = new List<SiegeLocation>();
        this.locations.Add(SiegeService.GetInstance().GetSiegeLocation(location));
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteH(locations.Count);
        foreach (SiegeLocation loc in locations)
        {
            WriteD(loc.GetLocationId());
            WriteC(loc.IsUnderShield() ? 1 : 0);
        }
    }
}
