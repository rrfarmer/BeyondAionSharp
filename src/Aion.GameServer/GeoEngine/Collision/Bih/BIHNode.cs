using System.Collections.Generic;
using Aion.GameServer.GeoEngine.Collision;
using Aion.GameServer.GeoEngine.Math;
using Aion.GameServer.GeoEngine.Utils;
using JMath = System.Math;

namespace Aion.GameServer.GeoEngine.Collision.Bih;

/// <summary>
/// Bounding Interval Hierarchy node. Based on: "Instant Ray Tracing: The Bounding Interval
/// Hierarchy" by Carsten Wächter and Alexander Keller.
/// Java parity: geoEngine/collision/bih/BIHNode.
/// </summary>
public sealed class BIHNode
{
    private int leftIndex, rightIndex;

    private BIHNode? left;
    private BIHNode? right;
    private float leftPlane;
    private float rightPlane;
    private int axis;

    public BIHNode(int l, int r)
    {
        leftIndex = l;
        rightIndex = r;
        axis = 3; // indicates leaf
    }

    public BIHNode(int axis)
    {
        this.axis = axis;
    }

    public BIHNode()
    {
    }

    public BIHNode? GetLeftChild()
    {
        return left;
    }

    public void SetLeftChild(BIHNode left)
    {
        this.left = left;
    }

    public float GetLeftPlane()
    {
        return leftPlane;
    }

    public void SetLeftPlane(float leftPlane)
    {
        this.leftPlane = leftPlane;
    }

    public BIHNode? GetRightChild()
    {
        return right;
    }

    public void SetRightChild(BIHNode right)
    {
        this.right = right;
    }

    public float GetRightPlane()
    {
        return rightPlane;
    }

    public void SetRightPlane(float rightPlane)
    {
        this.rightPlane = rightPlane;
    }

    public sealed class BIHStackData
    {
        internal readonly BIHNode node;
        internal readonly float min, max;

        internal BIHStackData(BIHNode node, float min, float max)
        {
            this.node = node;
            this.min = min;
            this.max = max;
        }
    }

    public int IntersectWhere(Ray r, Matrix4f worldMatrix, BIHTree tree, float sceneMin, float sceneMax, CollisionResults results)
    {
        TempVars vars = TempVars.Get();
        List<BIHStackData> stack = vars.bihStack;
        stack.Clear();

        Vector3f o = vars.vect1.Set(r.GetOrigin());
        Vector3f d = vars.vect2.Set(r.GetDirection());

        Matrix4f inv = worldMatrix.Invert();

        inv.Mult(r.GetOrigin(), r.GetOrigin());

        // Fixes rotation collision bug
        inv.MultNormal(r.GetDirection(), r.GetDirection());
        // inv.multNormalAcross(r.getDirection(), r.getDirection());

        float[] origins = { r.GetOrigin().X, r.GetOrigin().Y, r.GetOrigin().Z };

        float[] invDirections = { 1f / r.GetDirection().X, 1f / r.GetDirection().Y, 1f / r.GetDirection().Z };

        r.GetDirection().NormalizeLocal();

        Vector3f v1 = vars.vect3, v2 = vars.vect4, v3 = vars.vect5;
        int cols = 0;

        stack.Add(new BIHStackData(this, sceneMin, sceneMax));
        while (stack.Count > 0)
        {
            BIHStackData data = stack[stack.Count - 1];
            stack.RemoveAt(stack.Count - 1);
            BIHNode node = data.node;
            float tMin = data.min, tMax = data.max;

            if (tMax < tMin)
                continue;

            while (node.axis != 3)
            { // while node is not a leaf
                int a = node.axis;

                // find the origin and direction value for the given axis
                float origin = origins[a];
                float invDirection = invDirections[a];

                float tNearSplit, tFarSplit;
                BIHNode? nearNode, farNode;

                tNearSplit = (node.leftPlane - origin) * invDirection;
                tFarSplit = (node.rightPlane - origin) * invDirection;
                nearNode = node.left;
                farNode = node.right;

                if (invDirection < 0)
                {
                    float tmpSplit = tNearSplit;
                    tNearSplit = tFarSplit;
                    tFarSplit = tmpSplit;

                    BIHNode? tmpNode = nearNode;
                    nearNode = farNode;
                    farNode = tmpNode;
                }

                if (tMin > tNearSplit && tMax < tFarSplit)
                {
                    goto ContinueStackloop;
                }

                if (tMin > tNearSplit)
                {
                    tMin = JMath.Max(tMin, tFarSplit);
                    node = farNode!;
                }
                else if (tMax < tFarSplit)
                {
                    tMax = JMath.Min(tMax, tNearSplit);
                    node = nearNode!;
                }
                else
                {
                    stack.Add(new BIHStackData(farNode!, JMath.Max(tMin, tFarSplit), tMax));
                    tMax = JMath.Min(tMax, tNearSplit);
                    node = nearNode!;
                }
            }

            // a leaf
            for (int i = node.leftIndex; i <= node.rightIndex; i++)
            {
                tree.GetTriangle(i, v1, v2, v3);

                float t = r.Intersects(v1, v2, v3);
                if (!float.IsInfinity(t))
                {
                    worldMatrix.Mult(v1, v1);
                    worldMatrix.Mult(v2, v2);
                    worldMatrix.Mult(v3, v3);
                    float t_world = new Ray(o, d).Intersects(v1, v2, v3);
                    t = t_world;

                    Vector3f tempVarsContactPoint = vars.vect6.Set(d).MultLocal(t).AddLocal(o);
                    float worldSpaceDist = o.Distance(tempVarsContactPoint);
                    // fix invisible walls
                    if (worldSpaceDist > r.limit)
                        continue;
                    if (results.ShouldInvalidateSlopingSurface())
                    {
                        // taken from https://www.scratchapixel.com/lessons/3d-basic-rendering/ray-tracing-rendering-a-triangle/geometry-of-a-triangle
                        Vector3f planeNormal = v2.SubtractLocal(v1).CrossLocal(v3.SubtractLocal(v1)).NormalizeLocal();
                        double elevationAngleRad = planeNormal.AngleBetween(Vector3f.UNIT_Z);
                        if (elevationAngleRad > FastMath.HALF_PI) // convert angle >90-180° to 0-90° range
                            elevationAngleRad = JMath.PI - elevationAngleRad;
                        if (elevationAngleRad > results.GetSlopingSurfaceAngleRad())
                            tempVarsContactPoint.SetZ(float.NaN);
                    }
                    results.AddCollision(new CollisionResult(new Vector3f(tempVarsContactPoint), worldSpaceDist));
                    cols++;
                    if (results.IsOnlyFirst())
                        goto BreakStackloop;
                }
            }

            ContinueStackloop: ;
        }
        BreakStackloop:
        vars.Release();
        r.SetOrigin(o);
        r.SetDirection(d);
        return cols;
    }
}
