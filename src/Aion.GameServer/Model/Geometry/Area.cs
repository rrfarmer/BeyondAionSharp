using Aion.GameServer.Model.Templates.Zone;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Model.Geometry;

/// <summary>Java parity: model/geometry/Area — basic interface for all areas in AionEmu.</summary>
public interface Area
{
    bool IsInside2D(Point2D point);
    bool IsInside2D(float x, float y);
    bool IsInside3D(Point3D point);
    bool IsInside3D(float x, float y, float z);
    bool IsInsideZ(Point3D point);
    bool IsInsideZ(float z);

    double GetDistance2D(Point2D point);
    double GetDistance2D(float x, float y);
    double GetDistance3D(Point3D point);
    double GetDistance3D(float x, float y, float z);

    Point2D GetClosestPoint(Point2D point);
    Point2D GetClosestPoint(float x, float y);
    Point3D GetClosestPoint(Point3D point);
    Point3D GetClosestPoint(float x, float y, float z);

    float GetMinZ();
    float GetMaxZ();

    bool IntersectsRectangle(RectangleArea area);

    int GetWorldId();
    ZoneName GetZoneName();
}
