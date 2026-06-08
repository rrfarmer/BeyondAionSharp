using Aion.GameServer.Model.Templates.Zone;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Model.Geometry;

/// <summary>Java parity: model/geometry/RectangleArea — axis-aligned rectangular area.</summary>
public class RectangleArea : AbstractArea
{
    private readonly float _minX;
    private readonly float _maxX;
    private readonly float _minY;
    private readonly float _maxY;

    public RectangleArea(ZoneName zoneName, int worldId, float minX, float minY, float maxX, float maxY, float minZ, float maxZ)
        : base(zoneName, worldId, minZ, maxZ)
    {
        _minX = minX; _maxX = maxX;
        _minY = minY; _maxY = maxY;
    }

    /// <summary>Java parity: RectangleArea(ZoneName, int, Point, Point, Point, Point, int, int) — from four corner points.</summary>
    public RectangleArea(ZoneName zoneName, int worldId, (float x, float y) p1, (float x, float y) p2, (float x, float y) p3, (float x, float y) p4, int minZ, int maxZ)
        : base(zoneName, worldId, minZ, maxZ)
    {
        _minX = Math.Min(Math.Min(p1.x, p2.x), Math.Min(p3.x, p4.x));
        _maxX = Math.Max(Math.Max(p1.x, p2.x), Math.Max(p3.x, p4.x));
        _minY = Math.Min(Math.Min(p1.y, p2.y), Math.Min(p3.y, p4.y));
        _maxY = Math.Max(Math.Max(p1.y, p2.y), Math.Max(p3.y, p4.y));
    }

    public float GetMinX() => _minX;
    public float GetMaxX() => _maxX;
    public float GetMinY() => _minY;
    public float GetMaxY() => _maxY;

    public override bool IsInside2D(float x, float y) =>
        x >= _minX && x <= _maxX && y >= _minY && y <= _maxY;

    public override bool IsInside3D(float x, float y, float z) =>
        IsInside2D(x, y) && IsInsideZ(z);

    public override double GetDistance2D(float x, float y)
    {
        if (IsInside2D(x, y)) return 0;
        Point2D cp = GetClosestPoint(x, y);
        return PositionUtil.GetDistance(x, y, cp.GetX(), cp.GetY());
    }

    public override double GetDistance3D(float x, float y, float z)
    {
        if (IsInside3D(x, y, z)) return 0;
        if (IsInsideZ(z)) return GetDistance2D(x, y);
        Point3D cp = GetClosestPoint(x, y, z);
        return PositionUtil.GetDistance(x, y, z, cp.GetX(), cp.GetY(), cp.GetZ());
    }

    public override Point2D GetClosestPoint(float x, float y)
    {
        if (IsInside2D(x, y)) return new Point2D(x, y);

        // bottom edge
        Point2D closestPoint = PositionUtil.GetClosestPointOnSegment(_minX, _minY, _maxX, _minY, x, y);
        double distance = PositionUtil.GetDistance(x, y, closestPoint.GetX(), closestPoint.GetY());

        // top edge
        Point2D cp = PositionUtil.GetClosestPointOnSegment(_minX, _maxY, _maxX, _maxY, x, y);
        double d = PositionUtil.GetDistance(x, y, cp.GetX(), cp.GetY());
        if (d < distance) { closestPoint = cp; distance = d; }

        // left edge
        cp = PositionUtil.GetClosestPointOnSegment(_minX, _minY, _minX, _maxY, x, y);
        d = PositionUtil.GetDistance(x, y, cp.GetX(), cp.GetY());
        if (d < distance) { closestPoint = cp; distance = d; }

        // right edge
        cp = PositionUtil.GetClosestPointOnSegment(_maxX, _minY, _maxX, _maxY, x, y);
        d = PositionUtil.GetDistance(x, y, cp.GetX(), cp.GetY());
        if (d < distance) { closestPoint = cp; }

        return closestPoint;
    }

    public override bool IntersectsRectangle(RectangleArea area)
    {
        // Java: TODO Auto-generated method stub
        return false;
    }
}
