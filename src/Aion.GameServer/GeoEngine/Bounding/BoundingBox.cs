using System;
using System.Diagnostics;
using Aion.Commons.Nio;
using Aion.GameServer.GeoEngine.Collision;
using Aion.GameServer.GeoEngine.Math;
using Aion.GameServer.GeoEngine.Utils;

namespace Aion.GameServer.GeoEngine.Bounding;

/// <summary>
/// <c>BoundingBox</c> defines an axis-aligned cube containing a group of vertices; a center plus
/// extents along x, y and z.
/// Java parity: geoEngine/bounding/BoundingBox (jMonkeyEngine; Joshua Slack).
/// </summary>
public class BoundingBox : BoundingVolume
{
    internal float xExtent, yExtent, zExtent;

    public BoundingBox()
    {
    }

    public BoundingBox(Vector3f c, float x, float y, float z)
    {
        center.Set(c);
        xExtent = x;
        yExtent = y;
        zExtent = z;
    }

    public BoundingBox(BoundingBox source)
    {
        center.Set(source.center);
        xExtent = source.xExtent;
        yExtent = source.yExtent;
        zExtent = source.zExtent;
    }

    public BoundingBox(Vector3f min, Vector3f max)
    {
        SetMinMax(min, max);
    }

    public override Type GetTypeValue()
    {
        return Type.AABB;
    }

    public override void ComputeFromPoints(FloatBuffer points)
    {
        ContainAABB(points);
    }

    public static void CheckMinMax(Vector3f min, Vector3f max, Vector3f point)
    {
        if (point.X < min.X)
            min.X = point.X;
        if (point.X > max.X)
            max.X = point.X;
        if (point.Y < min.Y)
            min.Y = point.Y;
        if (point.Y > max.Y)
            max.Y = point.Y;
        if (point.Z < min.Z)
            min.Z = point.Z;
        if (point.Z > max.Z)
            max.Z = point.Z;
    }

    /// <summary>
    /// Creates a minimum-volume axis-aligned bounding box of the points.
    /// </summary>
    public void ContainAABB(FloatBuffer points)
    {
        if (points == null)
            return;

        if (points.Limit() <= 2) // we need at least a 3 float vector
            throw new ArgumentException();

        float minX = float.PositiveInfinity, minY = float.PositiveInfinity, minZ = float.PositiveInfinity;
        float maxX = float.NegativeInfinity, maxY = float.NegativeInfinity, maxZ = float.NegativeInfinity;

        TempVars vars = TempVars.Get();
        Vector3f vect1 = vars.vect1;
        for (int i = 0; i < points.Limit();)
        {
            vect1.X = points.Get(i++);
            vect1.Y = points.Get(i++);
            vect1.Z = points.Get(i++);
            if (vect1.X < minX)
                minX = vect1.X;
            if (vect1.X > maxX)
                maxX = vect1.X;

            if (vect1.Y < minY)
                minY = vect1.Y;
            if (vect1.Y > maxY)
                maxY = vect1.Y;

            if (vect1.Z < minZ)
                minZ = vect1.Z;
            if (vect1.Z > maxZ)
                maxZ = vect1.Z;
        }
        vars.Release();

        center.Set(minX + maxX, minY + maxY, minZ + maxZ);
        center.MultLocal(0.5f);

        xExtent = maxX - center.X;
        yExtent = maxY - center.Y;
        zExtent = maxZ - center.Z;
    }

    public override BoundingVolume Transform(Matrix4f trans, BoundingVolume? store)
    {
        BoundingBox box;
        if (store == null || store.GetTypeValue() != Type.AABB)
        {
            box = new BoundingBox();
        }
        else
        {
            box = (BoundingBox)store;
        }

        float w = trans.MultProj(center, box.center);
        box.center.DivideLocal(w);

        TempVars vars = TempVars.Get();
        Matrix3f transMatrix = vars.tempMat3;
        trans.ToRotationMatrix(transMatrix);

        // Make the rotation matrix all positive to get the maximum x/y/z extent
        transMatrix.AbsoluteLocal();

        vars.vect1.Set(xExtent, yExtent, zExtent);
        transMatrix.Mult(vars.vect1, vars.vect1);

        // Assign the biggest rotations after scales.
        box.xExtent = FastMath.Abs(vars.vect1.GetX());
        box.yExtent = FastMath.Abs(vars.vect1.GetY());
        box.zExtent = FastMath.Abs(vars.vect1.GetZ());

        vars.Release();
        return box;
    }

    public override BoundingVolume MergeLocal(BoundingVolume volume)
    {
        if (volume == null)
        {
            return this;
        }

        switch (volume.GetTypeValue())
        {
            case Type.AABB:
            {
                BoundingBox vBox = (BoundingBox)volume;
                return MergeLocal(vBox.center, vBox.xExtent, vBox.yExtent, vBox.zExtent);
            }

            // case OBB: return mergeOBB((OrientedBoundingBox) volume);

            default:
                return null!;
        }
    }

    /// <summary>
    /// Combines this bounding box locally with a second box described by its center and extents.
    /// </summary>
    private BoundingBox MergeLocal(Vector3f boxCenter, float boxX, float boxY, float boxZ)
    {
        if (xExtent == float.PositiveInfinity || boxX == float.PositiveInfinity)
        {
            center.X = 0;
            xExtent = float.PositiveInfinity;
        }
        else
        {
            float low = center.X - xExtent;
            if (low > boxCenter.X - boxX)
            {
                low = boxCenter.X - boxX;
            }
            float high = center.X + xExtent;
            if (high < boxCenter.X + boxX)
            {
                high = boxCenter.X + boxX;
            }
            center.X = (low + high) / 2;
            xExtent = high - center.X;
        }

        if (yExtent == float.PositiveInfinity || boxY == float.PositiveInfinity)
        {
            center.Y = 0;
            yExtent = float.PositiveInfinity;
        }
        else
        {
            float low = center.Y - yExtent;
            if (low > boxCenter.Y - boxY)
            {
                low = boxCenter.Y - boxY;
            }
            float high = center.Y + yExtent;
            if (high < boxCenter.Y + boxY)
            {
                high = boxCenter.Y + boxY;
            }
            center.Y = (low + high) / 2;
            yExtent = high - center.Y;
        }

        if (zExtent == float.PositiveInfinity || boxZ == float.PositiveInfinity)
        {
            center.Z = 0;
            zExtent = float.PositiveInfinity;
        }
        else
        {
            float low = center.Z - zExtent;
            if (low > boxCenter.Z - boxZ)
            {
                low = boxCenter.Z - boxZ;
            }
            float high = center.Z + zExtent;
            if (high < boxCenter.Z + boxZ)
            {
                high = boxCenter.Z + boxZ;
            }
            center.Z = (low + high) / 2;
            zExtent = high - center.Z;
        }

        return this;
    }

    public override BoundingBox Clone(BoundingVolume? store)
    {
        BoundingBox rVal;
        if (store != null && store.GetTypeValue() == Type.AABB)
            rVal = (BoundingBox)store;
        else
            rVal = new BoundingBox();
        rVal.center.Set(center);
        rVal.xExtent = xExtent;
        rVal.yExtent = yExtent;
        rVal.zExtent = zExtent;
        return rVal;
    }

    public override string ToString()
    {
        return GetType().Name + " [Center: " + center + "  xExtent: " + xExtent + "  yExtent: " + yExtent + "  zExtent: " + zExtent + "]";
    }

    public override bool Intersects(BoundingVolume bv)
    {
        return bv.IntersectsBoundingBox(this);
    }

    public override bool IntersectsBoundingBox(BoundingBox bb)
    {
        Debug.Assert(Vector3f.IsValidVector(center) && Vector3f.IsValidVector(bb.center));

        if (center.X + xExtent < bb.center.X - bb.xExtent || center.X - xExtent > bb.center.X + bb.xExtent)
            return false;
        else if (center.Y + yExtent < bb.center.Y - bb.yExtent || center.Y - yExtent > bb.center.Y + bb.yExtent)
            return false;
        else if (center.Z + zExtent < bb.center.Z - bb.zExtent || center.Z - zExtent > bb.center.Z + bb.zExtent)
            return false;
        else
            return true;
    }

    public override bool Intersects(Ray ray)
    {
        TempVars vars = TempVars.Get();
        Vector3f diff = ray.origin.Subtract(GetCenter(vars.vect2), vars.vect1);

        float[] fWdU = vars.fWdU;
        float[] fAWdU = vars.fAWdU;
        float[] fDdU = vars.fDdU;
        float[] fADdU = vars.fADdU;
        float[] fAWxDdU = vars.fAWxDdU;

        fWdU[0] = ray.GetDirection().Dot(Vector3f.UNIT_X);
        fAWdU[0] = FastMath.Abs(fWdU[0]);
        fDdU[0] = diff.Dot(Vector3f.UNIT_X);
        fADdU[0] = FastMath.Abs(fDdU[0]);
        if (fADdU[0] > xExtent && fDdU[0] * fWdU[0] >= 0.0)
        {
            vars.Release();
            return false;
        }

        fWdU[1] = ray.GetDirection().Dot(Vector3f.UNIT_Y);
        fAWdU[1] = FastMath.Abs(fWdU[1]);
        fDdU[1] = diff.Dot(Vector3f.UNIT_Y);
        fADdU[1] = FastMath.Abs(fDdU[1]);
        if (fADdU[1] > yExtent && fDdU[1] * fWdU[1] >= 0.0)
        {
            vars.Release();
            return false;
        }

        fWdU[2] = ray.GetDirection().Dot(Vector3f.UNIT_Z);
        fAWdU[2] = FastMath.Abs(fWdU[2]);
        fDdU[2] = diff.Dot(Vector3f.UNIT_Z);
        fADdU[2] = FastMath.Abs(fDdU[2]);
        if (fADdU[2] > zExtent && fDdU[2] * fWdU[2] >= 0.0)
        {
            vars.Release();
            return false;
        }

        Vector3f wCrossD = ray.GetDirection().Cross(diff, vars.vect2);

        fAWxDdU[0] = FastMath.Abs(wCrossD.Dot(Vector3f.UNIT_X));
        float rhs = yExtent * fAWdU[2] + zExtent * fAWdU[1];
        if (fAWxDdU[0] > rhs)
        {
            vars.Release();
            return false;
        }

        fAWxDdU[1] = FastMath.Abs(wCrossD.Dot(Vector3f.UNIT_Y));
        rhs = xExtent * fAWdU[2] + zExtent * fAWdU[0];
        if (fAWxDdU[1] > rhs)
        {
            vars.Release();
            return false;
        }

        fAWxDdU[2] = FastMath.Abs(wCrossD.Dot(Vector3f.UNIT_Z));
        rhs = xExtent * fAWdU[1] + yExtent * fAWdU[0];
        if (fAWxDdU[2] > rhs)
        {
            vars.Release();
            return false;
        }

        vars.Release();
        return true;
    }

    private int CollideWithRay(Ray ray, CollisionResults results)
    {
        TempVars vars = TempVars.Get();
        Vector3f diff = vars.vect1.Set(ray.origin).SubtractLocal(center);
        Vector3f direction = vars.vect2.Set(ray.direction);

        float[] t = vars.fWdU; // use one of the TempVars arrays
        t[0] = 0;
        t[1] = ray.GetLimit();
        int collisions = 0;

        float saveT0 = t[0], saveT1 = t[1];
        bool notEntirelyClipped = Clip(+direction.X, -diff.X - xExtent, t) && Clip(-direction.X, +diff.X - xExtent, t)
            && Clip(+direction.Y, -diff.Y - yExtent, t) && Clip(-direction.Y, +diff.Y - yExtent, t) && Clip(+direction.Z, -diff.Z - zExtent, t)
            && Clip(-direction.Z, +diff.Z - zExtent, t);

        if (notEntirelyClipped && (t[0] != saveT0 || t[1] != saveT1))
        {
            Vector3f contactPoint1 = new Vector3f(ray.direction).MultLocal(t[0]).AddLocal(ray.origin);
            results.AddCollision(new CollisionResult(contactPoint1, t[0]));
            collisions++;
            if (t[1] > t[0])
            {
                Vector3f contactPoint2 = new Vector3f(ray.direction).MultLocal(t[1]).AddLocal(ray.origin);
                results.AddCollision(new CollisionResult(contactPoint2, t[1]));
                collisions++;
            }
        }
        vars.Release();
        return collisions;
    }

    public override int CollideWith(Collidable other, CollisionResults results)
    {
        if (other is Ray ray)
        {
            return CollideWithRay(ray, results);
        }
        // Java parity: geoEngine/bounding/BoundingBox.java::collideWith throws UnsupportedCollisionException
        throw new UnsupportedCollisionException("With: " + other.GetType().Name);
    }

    public override bool Contains(Vector3f point)
    {
        return FastMath.Abs(center.X - point.X) < xExtent && FastMath.Abs(center.Y - point.Y) < yExtent && FastMath.Abs(center.Z - point.Z) < zExtent;
    }

    public override bool Intersects(Vector3f point)
    {
        return FastMath.Abs(center.X - point.X) <= xExtent && FastMath.Abs(center.Y - point.Y) <= yExtent && FastMath.Abs(center.Z - point.Z) <= zExtent;
    }

    public override float DistanceToEdge(Vector3f point)
    {
        // compute coordinates of point in box coordinate system
        TempVars vars = TempVars.Get();
        Vector3f closest = point.Subtract(center, vars.vect1);

        // project test point onto box
        float sqrDistance = 0.0f;
        float delta;

        if (closest.X < -xExtent)
        {
            delta = closest.X + xExtent;
            sqrDistance += delta * delta;
        }
        else if (closest.X > xExtent)
        {
            delta = closest.X - xExtent;
            sqrDistance += delta * delta;
        }

        if (closest.Y < -yExtent)
        {
            delta = closest.Y + yExtent;
            sqrDistance += delta * delta;
        }
        else if (closest.Y > yExtent)
        {
            delta = closest.Y - yExtent;
            sqrDistance += delta * delta;
        }

        if (closest.Z < -zExtent)
        {
            delta = closest.Z + zExtent;
            sqrDistance += delta * delta;
        }
        else if (closest.Z > zExtent)
        {
            delta = closest.Z - zExtent;
            sqrDistance += delta * delta;
        }

        vars.Release();
        return FastMath.Sqrt(sqrDistance);
    }

    /// <summary>
    /// Determines if a line segment intersects the current test plane.
    /// </summary>
    private bool Clip(float denom, float numer, float[] t)
    {
        // Return value is 'true' if line segment intersects the current test
        // plane. Otherwise 'false' is returned in which case the line segment
        // is entirely clipped.
        if (denom > 0.0f)
        {
            if (numer > denom * t[1])
                return false;
            if (numer > denom * t[0])
                t[0] = numer / denom;
            return true;
        }
        else if (denom < 0.0f)
        {
            if (numer > denom * t[0])
                return false;
            if (numer > denom * t[1])
                t[1] = numer / denom;
            return true;
        }
        else
        {
            return numer <= 0.0;
        }
    }

    /// <summary>Query extent. store null → returns a new vector.</summary>
    public Vector3f GetExtent(Vector3f store)
    {
        if (store == null)
        {
            store = new Vector3f();
        }
        store.Set(xExtent, yExtent, zExtent);
        return store;
    }

    public float GetXExtent()
    {
        return xExtent;
    }

    public float GetYExtent()
    {
        return yExtent;
    }

    public float GetZExtent()
    {
        return zExtent;
    }

    public void SetXExtent(float xExtent)
    {
        if (xExtent < 0)
            throw new ArgumentException();

        this.xExtent = xExtent;
    }

    public void SetYExtent(float yExtent)
    {
        if (yExtent < 0)
            throw new ArgumentException();

        this.yExtent = yExtent;
    }

    public void SetZExtent(float zExtent)
    {
        if (zExtent < 0)
            throw new ArgumentException();

        this.zExtent = zExtent;
    }

    public Vector3f GetMin(Vector3f store)
    {
        if (store == null)
        {
            store = new Vector3f();
        }
        store.Set(center).SubtractLocal(xExtent, yExtent, zExtent);
        return store;
    }

    public Vector3f GetMax(Vector3f store)
    {
        if (store == null)
        {
            store = new Vector3f();
        }
        store.Set(center).AddLocal(xExtent, yExtent, zExtent);
        return store;
    }

    public void SetMinMax(Vector3f min, Vector3f max)
    {
        center.Set(max).AddLocal(min).MultLocal(0.5f);
        xExtent = FastMath.Abs(max.X - center.X);
        yExtent = FastMath.Abs(max.Y - center.Y);
        zExtent = FastMath.Abs(max.Z - center.Z);
    }

    public override float GetVolume()
    {
        return (8 * xExtent * yExtent * zExtent);
    }
}
