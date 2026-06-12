using Aion.GameServer.Model.Templates.Zone;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Model.Geometry;

/// <summary>Java parity: model/geometry/CylinderArea — circular cylinder area.</summary>
public class CylinderArea : AbstractArea
{
    private readonly float _centerX;
    private readonly float _centerY;
    private readonly float _radius;

    public CylinderArea(ZoneName zoneName, int worldId, Point2D center, float radius, float minZ, float maxZ)
        : this(zoneName, worldId, center.GetX(), center.GetY(), radius, minZ, maxZ) { }

    // Java parity: Cylinder.getX()/getR()/getTop()/getBottom() return boxed Float (auto-unboxed); accept float?.
    public CylinderArea(ZoneName zoneName, int worldId, float? x, float? y, float? radius, float? minZ, float? maxZ)
        : base(zoneName, worldId, minZ ?? 0, maxZ ?? 0)
    {
        _centerX = x ?? 0;
        _centerY = y ?? 0;
        _radius  = radius ?? 0;
    }

    public override bool IsInside2D(float x, float y) =>
        PositionUtil.GetDistance(_centerX, _centerY, x, y) < _radius;

    public override double GetDistance2D(float x, float y)
    {
        if (IsInside2D(x, y)) return 0;
        return Math.Abs(PositionUtil.GetDistance(_centerX, _centerY, x, y) - _radius);
    }

    public override double GetDistance3D(float x, float y, float z)
    {
        if (IsInside3D(x, y, z)) return 0;
        if (IsInsideZ(z)) return GetDistance2D(x, y);
        float zEdge = z < GetMinZ() ? GetMinZ() : GetMaxZ();
        return PositionUtil.GetDistance(_centerX, _centerY, zEdge, x, y, z);
    }

    public override Point2D GetClosestPoint(float x, float y)
    {
        if (IsInside2D(x, y)) return new Point2D(x, y);
        float vX = x - _centerX;
        float vY = y - _centerY;
        double magV = PositionUtil.GetDistance(_centerX, _centerY, x, y);
        double pointX = _centerX + vX / magV * _radius;
        double pointY = _centerY + vY / magV * _radius;
        return new Point2D((float)pointX, (float)pointY);
    }

    public override bool IntersectsRectangle(RectangleArea area)
    {
        if (area.GetMinZ() > GetMaxZ() || area.GetMaxZ() < GetMinZ()) return false;
        return area.GetDistance2D(_centerX, _centerY) < _radius;
    }
}
