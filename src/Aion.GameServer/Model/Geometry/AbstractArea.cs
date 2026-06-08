using Aion.GameServer.Model.Templates.Zone;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Model.Geometry;

/// <summary>Java parity: model/geometry/AbstractArea — base implementation for all area shapes.</summary>
public abstract class AbstractArea : Area
{
    private readonly float _minZ;
    private readonly float _maxZ;
    private readonly ZoneName _zoneName;
    private readonly int _worldId;

    protected AbstractArea(ZoneName zoneName, int worldId, float minZ, float maxZ)
    {
        if (minZ > maxZ)
            throw new ArgumentException($"minZ({minZ}) > maxZ({maxZ})");
        _minZ     = minZ;
        _maxZ     = maxZ;
        _zoneName = zoneName;
        _worldId  = worldId;
    }

    public bool IsInside2D(Point2D point) => IsInside2D(point.GetX(), point.GetY());
    public bool IsInside3D(Point3D point) => IsInside3D(point.GetX(), point.GetY(), point.GetZ());

    public virtual bool IsInside3D(float x, float y, float z) => IsInsideZ(z) && IsInside2D(x, y);

    public bool IsInsideZ(Point3D point) => IsInsideZ(point.GetZ());
    public bool IsInsideZ(float z) => z >= _minZ && z <= _maxZ;

    public double GetDistance2D(Point2D point) => GetDistance2D(point.GetX(), point.GetY());
    public double GetDistance3D(Point3D point) => GetDistance3D(point.GetX(), point.GetY(), point.GetZ());

    public Point2D GetClosestPoint(Point2D point) => GetClosestPoint(point.GetX(), point.GetY());
    public Point3D GetClosestPoint(Point3D point) => GetClosestPoint(point.GetX(), point.GetY(), point.GetZ());

    public virtual Point3D GetClosestPoint(float x, float y, float z)
    {
        Point2D closest2d = GetClosestPoint(x, y);
        float zCoord;
        if (IsInsideZ(z))
            zCoord = z;
        else if (z < _minZ)
            zCoord = _minZ;
        else
            zCoord = _maxZ;
        return new Point3D(closest2d.GetX(), closest2d.GetY(), zCoord);
    }

    public float GetMinZ()          => _minZ;
    public float GetMaxZ()          => _maxZ;
    public int   GetWorldId()       => _worldId;
    public ZoneName GetZoneName()   => _zoneName;

    // Abstract members to be implemented by concrete shapes
    public abstract bool    IsInside2D(float x, float y);
    public abstract double  GetDistance2D(float x, float y);
    public abstract double  GetDistance3D(float x, float y, float z);
    public abstract Point2D GetClosestPoint(float x, float y);
    public abstract bool    IntersectsRectangle(RectangleArea area);
}
