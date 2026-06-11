using Aion.GameServer.GeoEngine.Math;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Road;
using Aion.GameServer.Model.Templates.Road;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.World;

namespace Aion.GameServer.Controllers.Observer;

/// <summary>Java parity: controllers/observer/RoadObserver (SheppeR).</summary>
public class RoadObserver : ActionObserver
{
    private readonly Player player;
    private readonly Road road;
    private Vector3f oldPosition;

    public RoadObserver(Road road, Player player)
        : base(ObserverType.MOVE)
    {
        this.player = player;
        this.road = road;
        this.oldPosition = new Vector3f(player.GetX(), player.GetY(), player.GetZ());
    }

    public override void Moved()
    {
        Vector3f newPosition = new Vector3f(player.GetX(), player.GetY(), player.GetZ());
        if (road.IsCrossed(oldPosition, newPosition))
        {
            RoadExit exit = road.GetTemplate().GetRoadExit();

            WorldType type = road.GetWorldType();
            if (type == WorldType.Elysea)
            {
                if (player.GetRace() == Race.ELYOS)
                {
                    TeleportService.TeleportTo(player, exit.GetMap(), exit.GetX(), exit.GetY(), exit.GetZ(), (byte) 0, TeleportAnimation.FadeOutBeam);
                }
            }
            else if (type == WorldType.Asmodae)
            {
                if (player.GetRace() == Race.ASMODIANS)
                {
                    TeleportService.TeleportTo(player, exit.GetMap(), exit.GetX(), exit.GetY(), exit.GetZ(), (byte) 0, TeleportAnimation.FadeOutBeam);
                }
            }
            else
            {
                TeleportService.TeleportTo(player, exit.GetMap(), exit.GetX(), exit.GetY(), exit.GetZ(), (byte) 0, TeleportAnimation.FadeOutBeam);
            }
        }
        oldPosition = newPosition;
    }
}
