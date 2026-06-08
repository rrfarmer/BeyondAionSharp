using Aion.GameServer.Commons.Nio;
using Aion.GameServer.GeoEngine.Collision;
using Aion.GameServer.GeoEngine.Math;

namespace Aion.GameServer.GeoEngine.Bounding;

/// <summary>
/// <c>BoundingVolume</c> defines an interface for dealing with containment of a collection of points.
/// Java parity: geoEngine/bounding/BoundingVolume (jMonkeyEngine; Mark Powell).
/// </summary>
public abstract class BoundingVolume : Collidable
{
    public enum Type
    {
        Sphere,
        AABB,
        OBB,
        Capsule,
    }

    internal Vector3f center = new Vector3f();

    protected BoundingVolume()
    {
    }

    protected BoundingVolume(Vector3f center)
    {
        this.center.Set(center);
    }

    /// <summary>
    /// Java parity: getType() — returns the type of bounding volume this is.
    /// (Named GetTypeValue to avoid clashing with Object.GetType and the nested Type enum.)
    /// </summary>
    public abstract Type GetTypeValue();

    /// <summary>Java parity: collideWith (inherited from Collidable; implemented by subclasses).</summary>
    public abstract int CollideWith(Collidable other, CollisionResults results);

    /// <summary>
    /// Alters the location of the bounding volume by a rotation, translation and a scalar.
    /// </summary>
    public abstract BoundingVolume Transform(Matrix4f trans, BoundingVolume store);

    /// <summary>Generates a bounding volume that encompasses a collection of points.</summary>
    public abstract void ComputeFromPoints(FloatBuffer points);

    /// <summary>
    /// Combines two bounding volumes into a single one containing both (stored locally).
    /// </summary>
    public abstract BoundingVolume MergeLocal(BoundingVolume volume);

    /// <summary>
    /// Creates a new BoundingVolume object containing the same data as this one.
    /// </summary>
    public abstract BoundingVolume Clone(BoundingVolume store);

    public Vector3f GetCenter()
    {
        return center;
    }

    public Vector3f GetCenter(Vector3f store)
    {
        store.Set(center);
        return store;
    }

    public void SetCenter(Vector3f newCenter)
    {
        center = newCenter;
    }

    /// <summary>Find the distance from the center of this Bounding Volume to the given point.</summary>
    public float DistanceTo(Vector3f point)
    {
        return center.Distance(point);
    }

    /// <summary>Find the squared distance from the center of this Bounding Volume to the given point.</summary>
    public float DistanceSquaredTo(Vector3f point)
    {
        return center.DistanceSquared(point);
    }

    /// <summary>Find the distance from the nearest edge of this Bounding Volume to the given point.</summary>
    public abstract float DistanceToEdge(Vector3f point);

    /// <summary>Determines if this bounding volume and a second given volume are intersecting.</summary>
    public abstract bool Intersects(BoundingVolume bv);

    /// <summary>Determines if a ray intersects this bounding volume.</summary>
    public abstract bool Intersects(Ray ray);

    /// <summary>Determines if this bounding volume and a given bounding box are intersecting.</summary>
    public abstract bool IntersectsBoundingBox(BoundingBox bb);

    /// <summary>Determines if a given point is contained within this bounding volume.</summary>
    public abstract bool Contains(Vector3f point);

    /// <summary>Determines if a given point intersects (touches or is inside) this bounding volume.</summary>
    public abstract bool Intersects(Vector3f point);

    public abstract float GetVolume();
}
