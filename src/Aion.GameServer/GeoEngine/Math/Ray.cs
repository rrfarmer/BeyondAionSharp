using Aion.GameServer.GeoEngine.Bounding;
using Aion.GameServer.GeoEngine.Collision;
using Aion.GameServer.GeoEngine.Utils;

namespace Aion.GameServer.GeoEngine.Math;

/// <summary>
/// <c>Ray</c> defines a line segment with an origin and a direction: R(t) = origin + t*direction for t &gt;= 0.
/// Java parity: geoEngine/math/Ray (jMonkeyEngine; Mark Powell, Joshua Slack).
/// </summary>
public sealed class Ray : Collidable
{
    /// <summary>The ray's beginning point.</summary>
    public Vector3f origin;

    /// <summary>The direction of the ray.</summary>
    public Vector3f direction;

    public float limit = float.PositiveInfinity;

    public Ray()
    {
        origin = new Vector3f();
        direction = new Vector3f();
    }

    public Ray(Vector3f origin, Vector3f direction)
    {
        this.origin = origin;
        this.direction = direction;
    }

    /// <summary>
    /// Determines if the Ray intersects the triangle (v0,v1,v2) and, if so, stores the point in loc.
    /// </summary>
    public bool IntersectWhere(Vector3f v0, Vector3f v1, Vector3f v2, Vector3f loc)
    {
        return Intersects(v0, v1, v2, loc, false, false);
    }

    private bool Intersects(Vector3f v0, Vector3f v1, Vector3f v2, Vector3f? store, bool doPlanar, bool quad)
    {
        TempVars vars = TempVars.Get();
        Vector3f tempVa = vars.vect1, tempVb = vars.vect2, tempVc = vars.vect3, tempVd = vars.vect4;

        Vector3f diff = origin.Subtract(v0, tempVa);
        Vector3f edge1 = v1.Subtract(v0, tempVb);
        Vector3f edge2 = v2.Subtract(v0, tempVc);
        Vector3f norm = edge1.Cross(edge2, tempVd);

        float dirDotNorm = direction.Dot(norm);
        float sign;
        if (dirDotNorm > FastMath.FLT_EPSILON)
        {
            sign = 1;
        }
        else if (dirDotNorm < -FastMath.FLT_EPSILON)
        {
            sign = -1f;
            dirDotNorm = -dirDotNorm;
        }
        else
        {
            // ray and triangle/quad are parallel
            vars.Release();
            return false;
        }

        float dirDotDiffxEdge2 = sign * direction.Dot(diff.Cross(edge2, edge2));
        if (dirDotDiffxEdge2 >= 0.0f)
        {
            float dirDotEdge1xDiff = sign * direction.Dot(edge1.CrossLocal(diff));

            if (dirDotEdge1xDiff >= 0.0f)
            {
                if (!quad ? dirDotDiffxEdge2 + dirDotEdge1xDiff <= dirDotNorm : dirDotEdge1xDiff <= dirDotNorm)
                {
                    float diffDotNorm = -sign * diff.Dot(norm);
                    if (diffDotNorm >= 0.0f)
                    {
                        // this method always returns
                        vars.Release();

                        // ray intersects triangle
                        // if storage vector is null, just return true,
                        if (store == null)
                            return true;

                        // else fill in.
                        float inv = 1f / dirDotNorm;
                        float t = diffDotNorm * inv;
                        if (!doPlanar)
                        {
                            store.Set(origin).AddLocal(direction.X * t, direction.Y * t, direction.Z * t);
                        }
                        else
                        {
                            // these weights can be used to determine interpolated values, such as texture coord.
                            float w1 = dirDotDiffxEdge2 * inv;
                            float w2 = dirDotEdge1xDiff * inv;
                            // float w0 = 1.0f - w1 - w2;
                            store.Set(t, w1, w2);
                        }
                        return true;
                    }
                }
            }
        }
        vars.Release();
        return false;
    }

    public float Intersects(Vector3f v0, Vector3f v1, Vector3f v2)
    {
        float edge1X = v1.X - v0.X;
        float edge1Y = v1.Y - v0.Y;
        float edge1Z = v1.Z - v0.Z;

        float edge2X = v2.X - v0.X;
        float edge2Y = v2.Y - v0.Y;
        float edge2Z = v2.Z - v0.Z;

        float normX = ((edge1Y * edge2Z) - (edge1Z * edge2Y));
        float normY = ((edge1Z * edge2X) - (edge1X * edge2Z));
        float normZ = ((edge1X * edge2Y) - (edge1Y * edge2X));

        float dirDotNorm = direction.X * normX + direction.Y * normY + direction.Z * normZ;

        float diffX = origin.X - v0.X;
        float diffY = origin.Y - v0.Y;
        float diffZ = origin.Z - v0.Z;

        float sign;
        if (dirDotNorm > FastMath.FLT_EPSILON)
        {
            sign = 1;
        }
        else if (dirDotNorm < -FastMath.FLT_EPSILON)
        {
            sign = -1f;
            dirDotNorm = -dirDotNorm;
        }
        else
        {
            // ray and triangle/quad are parallel
            return float.PositiveInfinity;
        }

        float diffEdge2X = ((diffY * edge2Z) - (diffZ * edge2Y));
        float diffEdge2Y = ((diffZ * edge2X) - (diffX * edge2Z));
        float diffEdge2Z = ((diffX * edge2Y) - (diffY * edge2X));

        float dirDotDiffxEdge2 = sign * (direction.X * diffEdge2X + direction.Y * diffEdge2Y + direction.Z * diffEdge2Z);

        if (dirDotDiffxEdge2 >= 0.0f)
        {
            diffEdge2X = ((edge1Y * diffZ) - (edge1Z * diffY));
            diffEdge2Y = ((edge1Z * diffX) - (edge1X * diffZ));
            diffEdge2Z = ((edge1X * diffY) - (edge1Y * diffX));

            float dirDotEdge1xDiff = sign * (direction.X * diffEdge2X + direction.Y * diffEdge2Y + direction.Z * diffEdge2Z);

            if (dirDotEdge1xDiff >= 0.0f)
            {
                if (dirDotDiffxEdge2 + dirDotEdge1xDiff <= dirDotNorm)
                {
                    float diffDotNorm = -sign * (diffX * normX + diffY * normY + diffZ * normZ);
                    if (diffDotNorm >= 0.0f)
                    {
                        // ray intersects triangle
                        // fill in.
                        float inv = 1f / dirDotNorm;
                        float t = diffDotNorm * inv;
                        return t;
                    }
                }
            }
        }

        return float.PositiveInfinity;
    }

    /// <summary>
    /// Determines if the Ray intersects a quad and stores the point as t, u, v.
    /// </summary>
    public bool IntersectWherePlanarQuad(Vector3f v0, Vector3f v1, Vector3f v2, Vector3f loc)
    {
        return Intersects(v0, v1, v2, loc, true, true);
    }

    public int CollideWith(Collidable other, CollisionResults results)
    {
        if (other is BoundingVolume)
        {
            BoundingVolume bv = (BoundingVolume)other;
            return bv.CollideWith(this, results);
        }
        else
        {
            // Java parity: geoEngine/math/Ray.java::collideWith throws UnsupportedCollisionException
            throw new UnsupportedCollisionException();
        }
    }

    public float DistanceSquared(Vector3f point)
    {
        TempVars vars = TempVars.Get();
        Vector3f tempVa = vars.vect1, tempVb = vars.vect2;

        point.Subtract(origin, tempVa);
        float rayParam = direction.Dot(tempVa);
        if (rayParam > 0)
        {
            origin.Add(direction.Mult(rayParam, tempVb), tempVb);
        }
        else
        {
            tempVb.Set(origin);
        }

        tempVb.Subtract(point, tempVa);
        float len = tempVa.LengthSquared();
        vars.Release();
        return len;
    }

    /// <summary>Retrieves the origin point of the ray.</summary>
    public Vector3f GetOrigin()
    {
        return origin;
    }

    /// <summary>Sets the origin of the ray.</summary>
    public void SetOrigin(Vector3f origin)
    {
        this.origin.Set(origin);
    }

    /// <summary>
    /// Returns the limit (length) of the ray. If not infinity, this ray is a line of length limit.
    /// </summary>
    public float GetLimit()
    {
        return limit;
    }

    /// <summary>Sets the limit of the ray.</summary>
    public void SetLimit(float limit)
    {
        this.limit = limit;
    }

    /// <summary>Retrieves the direction vector of the ray.</summary>
    public Vector3f GetDirection()
    {
        return direction;
    }

    /// <summary>Sets the direction vector of the ray.</summary>
    public void SetDirection(Vector3f direction)
    {
        this.direction.Set(direction);
    }

    /// <summary>Copies information from a source ray into this ray.</summary>
    public void Set(Ray source)
    {
        origin.Set(source.GetOrigin());
        direction.Set(source.GetDirection());
    }

    public override string ToString()
    {
        return GetType().Name + " [Origin: " + origin + ", Direction: " + direction + "]";
    }

    public Ray Clone()
    {
        Ray r = (Ray)MemberwiseClone();
        r.direction = direction.Clone();
        r.origin = origin.Clone();
        return r;
    }
}
