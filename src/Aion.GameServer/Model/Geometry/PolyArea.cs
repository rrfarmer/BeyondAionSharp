using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model.Templates.Zone;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Model.Geometry;

/// <summary>Java parity: model/geometry/PolyArea — free-form polygon area.</summary>
public class PolyArea : AbstractArea
{
    private readonly Polygon2D _poly;

    public PolyArea(ZoneName zoneName, int worldId, ICollection<Point2D> points, float zMin, float zMax)
        : this(zoneName, worldId, points.ToArray(), zMin, zMax) { }

    public PolyArea(ZoneName zoneName, int worldId, Point2D[] points, float zMin, float zMax)
        : base(zoneName, worldId, zMin, zMax)
    {
        if (points.Length < 3)
            throw new ArgumentException($"Not enough points, needed at least 3 but got {points.Length}");

        float[] xPoints = new float[points.Length];
        float[] yPoints = new float[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            xPoints[i] = points[i].GetX();
            yPoints[i] = points[i].GetY();
        }
        _poly = new Polygon2D(xPoints, yPoints, points.Length);
    }

    public override bool IsInside2D(float x, float y) => _poly.Contains(x, y);

    public override double GetDistance2D(float x, float y)
    {
        if (IsInside2D(x, y)) return 0;
        Point2D cp = GetClosestPoint(x, y);
        return PositionUtil.GetDistance(cp.GetX(), cp.GetY(), x, y);
    }

    public override double GetDistance3D(float x, float y, float z)
    {
        if (IsInside3D(x, y, z)) return 0;
        if (IsInsideZ(z)) return GetDistance2D(x, y);
        Point3D cp = GetClosestPoint(x, y, z);
        return PositionUtil.GetDistance(cp.GetX(), cp.GetY(), cp.GetZ(), x, y, z);
    }

    public override Point2D GetClosestPoint(float x, float y)
    {
        Point2D? closestPoint = null;
        double closestDistance = 0;
        for (int i = 0; i < _poly.xpoints.Length; i++)
        {
            int nextIndex = (i + 1) % _poly.xpoints.Length;
            float p1x = _poly.xpoints[i],       p1y = _poly.ypoints[i];
            float p2x = _poly.xpoints[nextIndex], p2y = _poly.ypoints[nextIndex];
            Point2D point = PositionUtil.GetClosestPointOnSegment(p1x, p1y, p2x, p2y, x, y);
            if (closestPoint == null)
            {
                closestPoint     = point;
                closestDistance  = PositionUtil.GetDistance(closestPoint.GetX(), closestPoint.GetY(), x, y);
            }
            else
            {
                double newDistance = PositionUtil.GetDistance(point.GetX(), point.GetY(), x, y);
                if (newDistance < closestDistance)
                {
                    closestPoint    = point;
                    closestDistance = newDistance;
                }
            }
        }
        return closestPoint!;
    }

    public override bool IntersectsRectangle(RectangleArea area)
    {
        if (area.GetMinZ() > GetMaxZ() || area.GetMaxZ() < GetMinZ()) return false;
        return _poly.Intersects(area.GetMinX(), area.GetMinY(),
                                WorldConfig.WorldRegionSize, WorldConfig.WorldRegionSize);
    }
}
