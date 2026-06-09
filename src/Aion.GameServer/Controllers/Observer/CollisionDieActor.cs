using Aion.GameServer.Configs.Main;
using Aion.GameServer.GeoEngine.Collision;
using Aion.GameServer.GeoEngine.Scene;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Siege;
using Aion.GameServer.Services.Player;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Controllers.Observer;

/// <summary>Java parity: controllers/observer/CollisionDieActor (Rolandas).</summary>
public class CollisionDieActor : AbstractCollisionObserver
{
    private readonly FortressLocation fortressLocation;

    public CollisionDieActor(Creature creature, Spatial geometry, FortressLocation fortressLocation)
        : base(creature, geometry, CollisionIntention.MATERIAL.GetId(), CheckType.PASS)
    {
        this.fortressLocation = fortressLocation;
    }

    public override void OnMoved(CollisionResults collisionResults)
    {
        if (collisionResults.Size() != 0)
        {
            if (GeoDataConfig.GEO_MATERIALS_SHOWDETAILS && creature is Player player && player.IsStaff())
            {
                CollisionResult result = collisionResults.GetClosestCollision();
                PacketSendUtility.SendMessage(player, "Entered " + result.GetGeometry().GetName());
            }
            if (fortressLocation.IsUnderShield() && fortressLocation.GetRace() != SiegeRace.GetByRace(creature.GetRace()))
                Kill(creature);
        }
    }

    public static void Kill(Creature creature)
    {
        if (creature.GetController().Die() && creature is Player player)
            PlayerReviveService.ScheduleReviveAtBase(player, 2500, 0);
    }
}
