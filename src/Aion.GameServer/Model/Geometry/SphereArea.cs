using Aion.GameServer.Model.Templates.Zone;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Model.Geometry;

/// <summary>Java parity: model/geometry/SphereArea — spherical area (2D checks always return false).</summary>
public class SphereArea : Area
{
    protected float X;
    protected float Y;
    protected float Z;
    protected float R;
    protected int WorldId;
    protected ZoneName ZoneName;

    public SphereArea(ZoneName zoneName, int worldId, float x, float y, float z, float r)
    {
        X = x; Y = y; Z = z; R = r;
        WorldId  = worldId;
        ZoneName = zoneName;
    }

    // Java: @Deprecated — 2D checks always return false for spheres
    public bool IsInside2D(Point2D point)  => false;
    public bool IsInside2D(float x, float y) => false;

    public virtual bool IsInside3D(Point3D point) =>
        PositionUtil.IsInRange(X, Y, Z, point.GetX(), point.GetY(), point.GetZ(), R);

    public virtual bool IsInside3D(float x, float y, float z) =>
        PositionUtil.IsInRange(x, y, z, X, Y, Z, R);

    public virtual bool IsInsideZ(Point3D point) => IsInsideZ(point.GetZ());
    public bool IsInsideZ(float z)       => z >= GetMinZ() && z <= GetMaxZ();

    // Java: @Deprecated
    public double GetDistance2D(Point2D point)    => 0;
    public double GetDistance2D(float x, float y) => 0;

    public virtual double GetDistance3D(Point3D point) =>
        GetDistance3D(point.GetX(), point.GetY(), point.GetZ());

    public virtual double GetDistance3D(float x, float y, float z)
    {
        double distance = PositionUtil.GetDistance(x, y, z, X, Y, Z) - R;
        return distance > 0 ? distance : 0;
    }

    // Java: @Deprecated
    public Point2D GetClosestPoint(Point2D point)    => new Point2D(point.GetX(), point.GetY());
    public Point2D GetClosestPoint(float x, float y) => new Point2D(x, y);

    public Point3D  GetClosestPoint(Point3D point)            => new Point3D(point);
    public Point3D  GetClosestPoint(float x, float y, float z) => new Point3D(x, y, z);

    public virtual float GetMinZ() => Z - R;
    public virtual float GetMaxZ() => Z + R;

    public virtual bool IntersectsRectangle(RectangleArea area) =>
        area.GetDistance3D(X, Y, Z) <= R;

    public int      GetWorldId()  => WorldId;
    public ZoneName GetZoneName() => ZoneName;
}
