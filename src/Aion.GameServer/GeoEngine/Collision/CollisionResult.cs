using System;
using Aion.GameServer.GeoEngine.Math;
using Aion.GameServer.GeoEngine.Scene;

namespace Aion.GameServer.GeoEngine.Collision;

/// <summary>
/// Java parity: geoEngine/collision/CollisionResult (jMonkeyEngine; Kirill).
/// </summary>
public class CollisionResult : IComparable<CollisionResult>
{
    private readonly Vector3f _contactPoint;
    private readonly float _distance;
    private Geometry? _geometry;

    public CollisionResult(Vector3f contactPoint, float distance)
    {
        _contactPoint = contactPoint;
        _distance = distance;
    }

    public int CompareTo(CollisionResult? other)
    {
        return _distance.CompareTo(other!._distance);
    }

    public void SetGeometry(Geometry geom)
    {
        _geometry = geom;
    }

    public Vector3f GetContactPoint()
    {
        return _contactPoint;
    }

    public Geometry? GetGeometry()
    {
        return _geometry;
    }

    public float GetDistance()
    {
        return _distance;
    }

    public override bool Equals(object? obj)
    {
        if (obj is not CollisionResult)
        {
            return false;
        }
        if (this == obj)
        {
            return true;
        }

        if (_distance != ((CollisionResult)obj)._distance || !_contactPoint.Equals(((CollisionResult)obj)._contactPoint))
        {
            return false;
        }
        return Equals(_geometry!.GetName(), ((CollisionResult)obj).GetGeometry()!.GetName());
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(_contactPoint, _distance, _geometry);
    }
}
