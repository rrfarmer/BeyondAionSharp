using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.World.Zone.Handler;

/// <summary>Java parity: world/zone/handler/AdvancedZoneHandler.</summary>
public interface AdvancedZoneHandler : IZoneHandler
{
    bool OnDie(Creature attacker, Creature target, ZoneInstance zone);
}
