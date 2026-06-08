using System;
using Aion.GameServer.GeoEngine.Bounding;
using Aion.GameServer.GeoEngine.Collision;
using Aion.GameServer.GeoEngine.Math;

namespace Aion.GameServer.GeoEngine.Scene;

/// <summary>
/// Java parity: geoEngine/scene/Geometry (jMonkeyEngine).
/// </summary>
public class Geometry : Spatial
{
    /// <summary>The mesh contained herein.</summary>
    protected Mesh mesh = null!;

    protected Matrix4f cachedWorldMat = new Matrix4f();

    /// <summary>Do not use this constructor. Serialization purposes only.</summary>
    protected Geometry()
    {
    }

    /// <summary>Create a geometry node with mesh data.</summary>
    public Geometry(string name, Mesh mesh)
        : base(name)
    {
        if (mesh == null)
            throw new ArgumentNullException(nameof(mesh));

        this.mesh = mesh;
    }

    public override int GetVertexCount()
    {
        return mesh.GetVertexCount();
    }

    public override int GetTriangleCount()
    {
        return mesh.GetTriangleCount();
    }

    public void SetMesh(Mesh mesh)
    {
        this.mesh = mesh;
    }

    public Mesh GetMesh()
    {
        return mesh;
    }

    /// <summary>The bounding volume of the mesh, in model space.</summary>
    public BoundingVolume GetModelBound()
    {
        return mesh.GetBound();
    }

    /// <summary>Updates the bounding volume of the mesh. Should be called when the mesh has been modified.</summary>
    public override void UpdateModelBound()
    {
        mesh.UpdateBound();
        worldBound = GetModelBound().Transform(cachedWorldMat, worldBound);
    }

    public Matrix4f GetWorldMatrix()
    {
        return cachedWorldMat;
    }

    public override void SetModelBound(BoundingVolume modelBound)
    {
        mesh.SetBound(modelBound);
    }

    public override int CollideWith(Collidable other, CollisionResults results)
    {
        if (other is Ray)
        {
            if (!worldBound!.Intersects((Ray)other))
                return 0;
        }
        // NOTE: BIHTree in mesh already checks collision with the mesh's bound
        int prevSize = results.Size();
        int added = mesh.CollideWith(other, cachedWorldMat, worldBound!, results);
        int newSize = results.Size();
        for (int i = prevSize; i < newSize; i++)
            results.GetCollisionDirect(i).SetGeometry(this);
        return added;
    }

    public override void SetTransform(Matrix3f rotation, Vector3f loc, Vector3f scale)
    {
        cachedWorldMat.LoadIdentity();
        cachedWorldMat.SetRotationMatrix(rotation);
        cachedWorldMat.Scale(scale);
        cachedWorldMat.SetTranslation(loc);
    }

    public override sbyte GetCollisionIntentions()
    {
        return mesh.GetCollisionIntentions();
    }

    public override void SetCollisionIntentions(sbyte collisionIntentions)
    {
        mesh.SetCollisionIntentions(collisionIntentions);
    }

    public override int GetMaterialId()
    {
        return mesh.GetMaterialId();
    }

    public override void SetMaterialId(sbyte materialId)
    {
        mesh.SetMaterialId(materialId);
    }

    public override Spatial Clone()
    {
        Geometry geometry = (Geometry)base.Clone();
        geometry.cachedWorldMat = cachedWorldMat.Clone();
        return geometry;
    }
}
