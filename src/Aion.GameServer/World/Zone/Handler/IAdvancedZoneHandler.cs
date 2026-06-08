using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.World.Zone.Handler;

/// <summary>
/// Zone handler that also reacts to a creature dying in the zone.
/// Java parity: world/zone/handler/AdvancedZoneHandler.
/// </summary>
public interface IAdvancedZoneHandler : IZoneHandler
{
    // Java parity: onDie(Creature attacker, Creature target, ZoneInstance zone) — TRUE if it handled the die event.
    bool OnDie(Creature attacker, Creature target, ZoneInstance zone);
}
