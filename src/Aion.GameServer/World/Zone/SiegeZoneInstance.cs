using Aion.GameServer.Model.Templates.Zone;

namespace Aion.GameServer.World.Zone;

/// <summary>Java parity: world/zone/SiegeZoneInstance.</summary>
public class SiegeZoneInstance : ZoneInstance
{
    public SiegeZoneInstance(int mapId, ZoneInfo template)
        : base(mapId, template)
    {
    }
}
