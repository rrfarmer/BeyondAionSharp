using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.World.Zone.Handler;

/// <summary>
/// Handler invoked when a creature enters/leaves a zone.
/// Java parity: world/zone/handler/IZoneHandler.
/// </summary>
public interface IZoneHandler
{
    // Java parity: onEnterZone(Creature, ZoneInstance)
    void OnEnterZone(Creature player, ZoneInstance zone);

    // Java parity: onLeaveZone(Creature, ZoneInstance)
    void OnLeaveZone(Creature player, ZoneInstance zone);
}
