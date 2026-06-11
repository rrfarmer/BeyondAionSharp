using Aion.Commons.Nio;
using Aion.GameServer.GeoEngine.Bounding;
using Aion.GameServer.GeoEngine.Collision;
using Aion.GameServer.GeoEngine.Collision.Bih;
using Aion.GameServer.GeoEngine.Math;
using Aion.GameServer.GeoEngine.Scene.Meshes;

namespace Aion.GameServer.GeoEngine.Scene;

/// <summary>
/// Java parity: geoEngine/scene/Mesh.
/// </summary>
public class Mesh
{
    /// <summary>The bounding volume that contains the mesh entirely. By default a BoundingBox (AABB).</summary>
    private BoundingVolume meshBound = new BoundingBox();

    private CollisionData? collisionTree = null;

    private float[] vertices = null!;
    private IndexArray indices = null!;

    private sbyte materialId = 0;
    private sbyte collisionIntentions = 0;

    public Mesh()
    {
    }

    public int GetTriangleCount()
    {
        return indices.Size() / 3;
    }

    public int GetVertexCount()
    {
        return vertices.Length;
    }

    public void GetTriangle(int index, Vector3f v1, Vector3f v2, Vector3f v3)
    {
        index *= 3;
        int vertexIndex = indices.Get(index++) * 3;
        v1.X = vertices[vertexIndex++];
        v1.Y = vertices[vertexIndex++];
        v1.Z = vertices[vertexIndex];
        vertexIndex = indices.Get(index++) * 3;
        v2.X = vertices[vertexIndex++];
        v2.Y = vertices[vertexIndex++];
        v2.Z = vertices[vertexIndex];
        vertexIndex = indices.Get(index) * 3;
        v3.X = vertices[vertexIndex++];
        v3.Y = vertices[vertexIndex++];
        v3.Z = vertices[vertexIndex];
    }

    public void SwapTriangles(int i1, int i2)
    {
        indices.Swap(i1, i2);
    }

    public void CreateCollisionData()
    {
        if (collisionTree != null)
        {
            return;
        }
        BIHTree tree = new BIHTree(this);
        tree.Construct();
        collisionTree = tree;
    }

    public int CollideWith(Collidable other, Matrix4f worldMatrix, BoundingVolume worldBound, CollisionResults results)
    {
        if (collisionTree == null)
        {
            CreateCollisionData();
        }

        return collisionTree!.CollideWith(other, worldMatrix, worldBound, results);
    }

    public void SetVertices(FloatBuffer vertices)
    {
        collisionTree = null;
        this.vertices = new float[vertices.Limit()];
        vertices.Get(this.vertices);
    }

    public void SetIndices(Aion.Commons.Nio.Buffer indices)
    {
        collisionTree = null;
        this.indices = IndexArray.From(indices);
    }

    public void UpdateBound()
    {
        meshBound.ComputeFromPoints(FloatBuffer.Wrap(vertices));
    }

    public BoundingVolume GetBound()
    {
        return meshBound;
    }

    public void SetBound(BoundingVolume? modelBound)
    {
        meshBound = modelBound!;
    }

    public void SetCollisionIntentions(sbyte collisionIntentions)
    {
        this.collisionIntentions = collisionIntentions;
    }

    public void SetMaterialId(sbyte materialId)
    {
        this.materialId = materialId;
    }

    public sbyte GetCollisionIntentions()
    {
        return collisionIntentions;
    }

    public int GetMaterialId()
    {
        return (materialId & 0xFF);
    }
}
