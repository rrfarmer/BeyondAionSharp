using Aion.GameServer.GeoEngine.Math;

namespace Aion.GameServer.Model.Geometry;

/// <summary>Java parity: model/geometry/Plane3D (Neon).</summary>
public class Plane3D
{
    private readonly Vector3f pointOnPlane;
    private readonly Vector3f normal;

    public Plane3D(Vector3f p1, Vector3f p2, Vector3f p3)
    {
        this.pointOnPlane = p1;
        Vector3f vector1 = p2.Subtract(p1);
        Vector3f vector2 = p3.Subtract(p1);
        normal = vector1.Cross(vector2);
    }

    public Vector3f Intersection(Vector3f rayStart, Vector3f rayEnd)
    {
        Vector3f rayDirection = rayEnd.Subtract(rayStart);
        float dotProduct = normal.Dot(rayDirection);
        if (dotProduct == 0) // ray is parallel to the plane
            return null;
        float distance = normal.Dot(pointOnPlane.Subtract(rayStart)) / dotProduct;
        if (distance < 0 || distance > 1) // intersection point is outside the range of the ray
            return null;
        return rayStart.Add(rayDirection.MultLocal(distance));
    }
}
