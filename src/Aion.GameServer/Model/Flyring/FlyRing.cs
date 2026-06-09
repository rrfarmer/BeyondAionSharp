using Aion.GameServer.Controllers;
using Aion.GameServer.GeoEngine.Math;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Geometry;
using Aion.GameServer.Model.Templates.Flyring;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.IdFactory;
using Aion.GameServer.World.Knownlist;

namespace Aion.GameServer.Model.Flyring;

/// <summary>Java parity: model/flyring/FlyRing (xavier).</summary>
public class FlyRing : VisibleObject
{
    private readonly FlyRingTemplate template;
    private readonly Plane3D plane;

    public FlyRing(FlyRingTemplate template, int instanceId)
        : base(IDFactory.GetInstance().NextId(), new FlyRingController(), null, null,
            Aion.GameServer.World.World.GetInstance().CreatePosition(template.GetMap(),
                template.GetCenter().GetX(), template.GetCenter().GetY(), template.GetCenter().GetZ(), (byte) 0, instanceId), true)
    {
        ((FlyRingController) GetController()).SetOwner(this);
        this.template = template;
        Vector3f p1 = new Vector3f(template.GetCenter().GetX(), template.GetCenter().GetY(), template.GetCenter().GetZ());
        Vector3f p2 = new Vector3f(template.GetP1().GetX(), template.GetP1().GetY(), template.GetP1().GetZ());
        Vector3f p3 = new Vector3f(template.GetP2().GetX(), template.GetP2().GetY(), template.GetP2().GetZ());
        this.plane = new Plane3D(p1, p2, p3);
        SetKnownlist(new PlayerAwareKnownList(this));
    }

    public bool IsCrossed(Vector3f oldPosition, Vector3f newPosition)
    {
        Vector3f intersection = plane.Intersection(oldPosition, newPosition);
        return intersection != null && PositionUtil.IsInRange(this, intersection.X, intersection.Y, intersection.Z, template.GetRadius());
    }

    public FlyRingTemplate GetTemplate()
    {
        return template;
    }

    // Java parity: getName()
    public override string Name => template.GetName();

    public void Spawn()
    {
        Aion.GameServer.World.World.GetInstance().Spawn(this);
    }
}
