using System.Collections.Concurrent;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.GeoEngine.Scene;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Services;
using Aion.GameServer.World.Zone;
using Aion.GameServer.World.Zone.Handler;

namespace Aion.GameServer.Model.Siege;

/// <summary>
/// Shields have material ID 11 in geo.
/// Java parity: model/siege/SiegeShield (Rolandas).
/// </summary>
public class SiegeShield : IZoneHandler
{
    private readonly ConcurrentDictionary<int, ActionObserver> observed = new ConcurrentDictionary<int, ActionObserver>();
    private readonly Spatial geometry;
    private int siegeLocationId;

    public SiegeShield(Spatial geometry)
    {
        this.geometry = geometry;
        if (geometry.GetParent() is DespawnableNode despawnableNode)
        {
            despawnableNode.SetType(DespawnableNode.DespawnableType.SHIELD);
        }
    }

    public Spatial GetGeometry()
    {
        return geometry;
    }

    public void OnEnterZone(Creature creature, ZoneInstance zone)
    {
        if (creature is Player player)
        {
            FortressLocation loc = SiegeService.GetInstance().GetFortress(siegeLocationId);
            if (loc.GetRace() != SiegeRace.GetByRace(player.GetRace()))
            {
                CollisionDieActor shieldObserver = new CollisionDieActor(creature, geometry, loc);
                creature.GetObserveController().AddObserver(shieldObserver);
                observed[creature.GetObjectId()] = shieldObserver;
            }
        }
    }

    public void OnLeaveZone(Creature creature, ZoneInstance zone)
    {
        observed.TryRemove(creature.GetObjectId(), out ActionObserver actionObserver);
        if (actionObserver != null)
            creature.GetObserveController().RemoveObserver(actionObserver);
    }

    public void SetSiegeLocationId(int siegeLocationId)
    {
        this.siegeLocationId = siegeLocationId;
        if (geometry.GetParent() is DespawnableNode despawnableNode)
        {
            despawnableNode.SetId(siegeLocationId);
        }
    }

    public override string ToString()
    {
        return "LocId=" + siegeLocationId + "; Name=" + geometry.GetName() + "; Bounds=" + geometry.GetWorldBound();
    }
}
