using Aion.GameServer.GeoEngine.Bounding;
using Aion.GameServer.GeoEngine.Collision;
using Aion.GameServer.GeoEngine.Math;
using Aion.GameServer.GeoEngine.Scene;
using Aion.GameServer.GeoEngine.Utils;
using JMath = System.Math;

namespace Aion.GameServer.GeoEngine.Collision.Bih;

/// <summary>
/// Java parity: geoEngine/collision/bih/BIHTree.
/// </summary>
public class BIHTree : CollisionData
{
    public const int MAX_TREE_DEPTH = 100;
    public const int MAX_TRIS_PER_NODE = 21;

    private BIHNode? root;
    private readonly Mesh mesh;

    public BIHTree(Mesh mesh)
    {
        this.mesh = mesh;
    }

    public void Construct()
    {
        int numTris = mesh.GetTriangleCount();
        root = CreateNode(0, numTris - 1, (BoundingBox)mesh.GetBound(), 0);
    }

    private BoundingBox CreateBox(int l, int r)
    {
        TempVars vars = TempVars.Get();
        Vector3f min = vars.vect1.Set(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        Vector3f max = vars.vect2.Set(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

        Vector3f v1 = vars.vect3, v2 = vars.vect4, v3 = vars.vect5;

        for (int i = l; i <= r; i++)
        {
            GetTriangle(i, v1, v2, v3);
            BoundingBox.CheckMinMax(min, max, v1);
            BoundingBox.CheckMinMax(min, max, v2);
            BoundingBox.CheckMinMax(min, max, v3);
        }

        BoundingBox bbox = new BoundingBox(min, max);
        vars.Release();
        return bbox;
    }

    private int SortTriangles(int l, int r, float split, int axis)
    {
        int pivot = l;
        int j = r;

        TempVars vars = TempVars.Get();
        Vector3f v1 = vars.vect1, v2 = vars.vect2, v3 = vars.vect3;

        while (pivot <= j)
        {
            GetTriangle(pivot, v1, v2, v3);
            v1.AddLocal(v2).AddLocal(v3).MultLocal(FastMath.ONE_THIRD);
            if (v1.Get(axis) > split)
            {
                mesh.SwapTriangles(pivot, j);
                --j;
            }
            else
            {
                ++pivot;
            }
        }

        vars.Release();
        pivot = (pivot == l && j < pivot) ? j : pivot;
        return pivot;
    }

    private void SetMinMax(BoundingBox bbox, bool doMin, int axis, float value)
    {
        Vector3f min = bbox.GetMin(null!);
        Vector3f max = bbox.GetMax(null!);

        if (doMin)
            min.Set(axis, value);
        else
            max.Set(axis, value);

        bbox.SetMinMax(min, max);
    }

    private float GetMinMax(BoundingBox bbox, bool doMin, int axis)
    {
        if (doMin)
            return bbox.GetMin(null!).Get(axis);
        else
            return bbox.GetMax(null!).Get(axis);
    }

    private BIHNode CreateNode(int l, int r, BoundingBox nodeBbox, int depth)
    {
        if ((r - l) < MAX_TRIS_PER_NODE || depth > MAX_TREE_DEPTH)
        {
            return new BIHNode(l, r);
        }

        BoundingBox currentBox = ReferenceEquals(nodeBbox, mesh.GetBound()) ? nodeBbox : CreateBox(l, r);

        Vector3f exteriorExt = nodeBbox.GetExtent(null!);
        Vector3f interiorExt = currentBox.GetExtent(null!);
        exteriorExt.SubtractLocal(interiorExt);

        int axis = 0;
        if (exteriorExt.X > exteriorExt.Y)
        {
            if (exteriorExt.X > exteriorExt.Z)
                axis = 0;
            else
                axis = 2;
        }
        else
        {
            if (exteriorExt.Y > exteriorExt.Z)
                axis = 1;
            else
                axis = 2;
        }
        if (exteriorExt.Equals(Vector3f.ZERO))
            axis = 0;

        float split = currentBox.GetCenter().Get(axis);
        int pivot = SortTriangles(l, r, split, axis);
        if (pivot == l || pivot == r)
            pivot = (r + l) / 2;

        // If one of the partitions is empty, continue with recursion: same level but different bbox
        if (pivot < l)
        {
            // Only right
            BoundingBox rbbox = new BoundingBox(currentBox);
            SetMinMax(rbbox, true, axis, split);
            return CreateNode(l, r, rbbox, depth + 1);
        }
        else if (pivot > r)
        {
            // Only left
            BoundingBox lbbox = new BoundingBox(currentBox);
            SetMinMax(lbbox, false, axis, split);
            return CreateNode(l, r, lbbox, depth + 1);
        }
        else
        {
            // Build the node
            BIHNode node = new BIHNode(axis);

            // Left child
            BoundingBox lbbox = new BoundingBox(currentBox);
            SetMinMax(lbbox, false, axis, split);

            // The left node right border is the plane most right
            node.SetLeftPlane(GetMinMax(CreateBox(l, JMath.Max(l, pivot - 1)), false, axis));
            node.SetLeftChild(CreateNode(l, JMath.Max(l, pivot - 1), lbbox, depth + 1)); // Recursive call

            // Right Child
            BoundingBox rbbox = new BoundingBox(currentBox);
            SetMinMax(rbbox, true, axis, split);
            // The right node left border is the plane most left
            node.SetRightPlane(GetMinMax(CreateBox(pivot, r), true, axis));
            node.SetRightChild(CreateNode(pivot, r, rbbox, depth + 1)); // Recursive call

            return node;
        }
    }

    public void GetTriangle(int index, Vector3f v1, Vector3f v2, Vector3f v3)
    {
        mesh.GetTriangle(index, v1, v2, v3);
    }

    private int CollideWithRay(Ray r,
        Matrix4f worldMatrix,
        BoundingVolume worldBound,
        CollisionResults results)
    {
        CollisionResults wbCollisions = new CollisionResults(results.GetIntentions(), results.GetInstanceId(), results.IsOnlyFirst());
        worldBound.CollideWith(r, wbCollisions);
        int collisions = 0;
        // if worldBound contains ray origin and there are no collisions it means ray starts and ends inside worldBound
        if (wbCollisions.Size() > 0 || worldBound.Contains(r.GetOrigin()))
        {
            float tMin = 0;
            float tMax = r.GetLimit();
            if (wbCollisions.Size() > 0)
            {
                tMin = wbCollisions.GetClosestCollision()!.GetDistance();
                tMax = wbCollisions.GetFarthestCollision()!.GetDistance();
                if (tMax <= 0)
                    tMax = float.PositiveInfinity;
                else if (tMin == tMax)
                    tMin = 0;

                if (tMin <= 0)
                    tMin = 0;

                if (r.GetLimit() < float.PositiveInfinity)
                    tMax = JMath.Min(tMax, r.GetLimit());
            }

            // collisions += root.intersectBrute(r, worldMatrix, this, tMin, tMax, results);
            collisions += root!.IntersectWhere(r, worldMatrix, this, tMin, tMax, results);
        }
        return collisions;
    }

    public int CollideWith(Collidable other, Matrix4f worldMatrix, BoundingVolume worldBound, CollisionResults results)
    {
        if (other is Ray ray)
        {
            return CollideWithRay(ray, worldMatrix, worldBound, results);
        }
        // Java parity: geoEngine/collision/bih/BIHTree.java::collideWith throws UnsupportedCollisionException
        throw new UnsupportedCollisionException();
    }
}
