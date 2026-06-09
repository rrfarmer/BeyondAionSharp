using Aion.GameServer.Model.Templates.Zone;

namespace Aion.GameServer.World.Zone;

/// <summary>Java parity: world/zone/InvasionZoneInstance.</summary>
public class InvasionZoneInstance : ZoneInstance
{
    public InvasionZoneInstance(int mapId, ZoneInfo template)
        : base(mapId, template)
    {
    }
}
