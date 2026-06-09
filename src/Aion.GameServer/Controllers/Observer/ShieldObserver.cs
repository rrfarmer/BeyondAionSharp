using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Geometry;
using Aion.GameServer.Model.Siege;
using Aion.GameServer.Model.Templates.Shield;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Controllers.Observer;

/// <summary>Java parity: controllers/observer/ShieldObserver (Wakizashi, Source).</summary>
public class ShieldObserver : ActionObserver
{
    private readonly FortressLocation location;
    private readonly Creature creature;
    private readonly ShieldTemplate shield;
    private readonly Point3D oldPosition;

    public ShieldObserver(FortressLocation location, ShieldTemplate shield, Creature creature)
        : base(ObserverType.MOVE)
    {
        this.location = location;
        this.creature = creature;
        this.shield = shield;
        WorldPosition lastPos;
        if (creature is Player player && (lastPos = player.GetMoveController().GetLastPositionFromClient()) != null)
        {
            this.oldPosition = new Point3D(lastPos.GetX(), lastPos.GetY(), lastPos.GetZ());
        }
        else
        {
            this.oldPosition = new Point3D(creature.GetX(), creature.GetY(), creature.GetZ());
        }
    }

    public override void Moved()
    {
        ShieldPoint shieldCenter = shield.GetCenter();
        bool passedThrough = false;
        // only collide with upper half of sphere
        if (location.IsUnderShield() && !(creature.GetZ() < shieldCenter.GetZ() && oldPosition.GetZ() < shieldCenter.GetZ()))
        {
            bool wasInside = PositionUtil.IsInRange(oldPosition.GetX(), oldPosition.GetY(), oldPosition.GetZ(), shieldCenter.GetX(), shieldCenter.GetY(), shieldCenter.GetZ(), shield.GetRadius());
            bool isInside = PositionUtil.IsInRange(creature, shieldCenter.GetX(), shieldCenter.GetY(), shieldCenter.GetZ(), shield.GetRadius());
            passedThrough = wasInside != isInside;
        }

        if (passedThrough)
        {
            CollisionDieActor.Kill(creature);
        }
        else
        {
            lock (oldPosition)
            {
                oldPosition.SetX(creature.GetX());
                oldPosition.SetY(creature.GetY());
                oldPosition.SetZ(creature.GetZ());
            }
        }
    }
}
