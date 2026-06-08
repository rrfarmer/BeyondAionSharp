using Aion.GameServer.Model.Templates.Zone;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Model.Geometry;

/// <summary>Java parity: model/geometry/SemisphereArea — upper half-sphere (z > center.z only).</summary>
public class SemisphereArea : SphereArea
{
    public SemisphereArea(ZoneName zoneName, int worldId, float x, float y, float z, float r)
        : base(zoneName, worldId, x, y, z, r) { }

    public override bool IsInside3D(Point3D point) =>
        Z < point.GetZ() && PositionUtil.IsInRange(X, Y, Z, point.GetX(), point.GetY(), point.GetZ(), R);

    public override bool IsInside3D(float x, float y, float z) =>
        Z < z && PositionUtil.IsInRange(x, y, z, X, Y, Z, R);

    public override bool IsInsideZ(Point3D point) => IsInsideZ(point.GetZ());

    public override float GetMinZ() => Z;
    public override float GetMaxZ() => Z + R;

    public override double GetDistance3D(Point3D point) =>
        GetDistance3D(point.GetX(), point.GetY(), point.GetZ());

    public override double GetDistance3D(float x, float y, float z)
    {
        double distance = PositionUtil.GetDistance(x, y, z, X, Y, Z) - R;
        if (z < Z) return distance;
        return distance > 0 ? distance : 0;
    }

    public override bool IntersectsRectangle(RectangleArea area)
    {
        if ((area.GetMaxZ() >= Z || Z <= area.GetMinZ()) && area.GetDistance3D(X, Y, Z) <= R)
            return true;
        return false;
    }
}
