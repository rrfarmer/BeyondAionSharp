using Aion.GameServer.GeoEngine.Collision;
using Aion.GameServer.GeoEngine.Math;
using Aion.GameServer.GeoEngine.Utils;
using JMath = System.Math;

namespace Aion.GameServer.GeoEngine.Models;

/// <summary>
/// Java parity: geoEngine/models/Terrain. Heightmap + material terrain with ray collision.
/// </summary>
public class Terrain
{
    private const int HEIGHTMAP_UNIT_SIZE = 2; // distance between points (always 2 m)
    private const int HEIGHTMAP_MAX_Z_EXCLUSIVE = 2048; // valid z values range from 0 (inclusive) to 2048 (exclusive)

    private int heightmapXSize, heightmapYSize;
    private short[]? heightmap;
    private int materialsXSize, materialsYSize;
    private byte[]? materials;

    public void SetHeightmap(short[] heightmap, int heightmapXSize, int heightmapYSize)
    {
        if (materials != null && (heightmapXSize < materialsXSize || heightmapYSize < materialsYSize))
            throw new System.ArgumentException("Terrain heightmap must not be smaller than terrain materials");
        int lengthDiff = heightmap.Length - heightmapXSize * heightmapYSize;
        if (lengthDiff != 0)
            throw new System.ArgumentException("Expected terrain heightmap length differs by " + lengthDiff + " bytes");
        bool allSameZValues = heightmap.Length > 0;
        foreach (short z in heightmap)
        {
            if (z != heightmap[0])
            {
                allSameZValues = false;
                break;
            }
        }
        this.heightmap = allSameZValues ? new short[] { heightmap[0] } : heightmap;
        this.heightmapXSize = heightmapXSize;
        this.heightmapYSize = heightmapYSize;
    }

    public void SetMaterials(byte[] materials, int materialsXSize, int materialsYSize)
    {
        if (heightmap != null && (materialsXSize > heightmapXSize || materialsYSize > heightmapYSize))
            throw new System.ArgumentException("Terrain materials need a terrain heightmap of at least the same size");
        int lengthDiff = materials.Length - materialsXSize * materialsYSize;
        if (lengthDiff != 0)
            throw new System.ArgumentException("Expected terrain materials length differs by " + lengthDiff + " bytes");
        this.materials = materials;
        this.materialsXSize = materialsXSize;
        this.materialsYSize = materialsYSize;
    }

    public bool HasHeightmap()
    {
        return heightmap != null;
    }

    public bool HasMaterials()
    {
        return materials != null;
    }

    public void CollideAtOrigin(Ray r, CollisionResults results)
    {
        TempVars vars = TempVars.Get();
        CollideNearXY(r.origin.X, r.origin.Y, r, vars.vect1, vars.vect2, vars.vect3, results);
        vars.Release();
    }

    public bool Collide(Ray ray, float targetX, float targetY, CollisionResults? results)
    {
        float distanceX = targetX - ray.origin.X;
        float distanceY = targetY - ray.origin.Y;
        float distance2D = (float)JMath.Sqrt(distanceX * distanceX + distanceY * distanceY);
        float checkDistanceLimit = distance2D + HEIGHTMAP_UNIT_SIZE;
        TempVars vars = TempVars.Get();
        for (int checkDistance = 0; checkDistance < checkDistanceLimit; checkDistance += HEIGHTMAP_UNIT_SIZE)
        {
            float distanceFactor = checkDistance / distance2D;
            float x = ray.origin.X + distanceX * distanceFactor;
            float y = ray.origin.Y + distanceY * distanceFactor;
            if (CollideNearXY(x, y, ray, vars.vect1, vars.vect2, vars.vect3, results)
                || CollideNearXY(x + HEIGHTMAP_UNIT_SIZE, y, ray, vars.vect1, vars.vect2, vars.vect3, results)
                || CollideNearXY(x, y + HEIGHTMAP_UNIT_SIZE, ray, vars.vect1, vars.vect2, vars.vect3, results))
            {
                vars.Release();
                return true;
            }
        }
        vars.Release();
        return false;
    }

    /// <summary>
    /// Terrain layout (top view): p1-p4 are terrain points around the given x/y, 2 m apart. Faces
    /// (p1,p2,p3) and (p2,p3,p4) are checked against the ray; the first collision is written to the result.
    /// </summary>
    private bool CollideNearXY(float x, float y, Ray ray, Vector3f p1or4, Vector3f p2, Vector3f p3, CollisionResults? results)
    {
        int xIndexNorth = (int)(x / HEIGHTMAP_UNIT_SIZE);
        int yIndexWest = (int)(y / HEIGHTMAP_UNIT_SIZE);
        int yIndexEast = yIndexWest + 1;
        float z2 = GetZ(xIndexNorth, yIndexEast);
        if (float.IsNaN(z2))
            return false;
        int xIndexSouth = xIndexNorth + 1;
        float z3 = GetZ(xIndexSouth, yIndexWest);
        if (float.IsNaN(z3))
            return false;
        float z1 = GetZ(xIndexNorth, yIndexWest);
        float z4 = GetZ(xIndexSouth, yIndexEast);
        int xNorth = xIndexNorth * HEIGHTMAP_UNIT_SIZE;
        int yWest = yIndexWest * HEIGHTMAP_UNIT_SIZE;
        int yEast = yWest + HEIGHTMAP_UNIT_SIZE;
        int xSouth = xNorth + HEIGHTMAP_UNIT_SIZE;
        p2.Set(xNorth, yEast, z2);
        p3.Set(xSouth, yWest, z3);
        Vector3f contactPoint = new Vector3f();
        if ((float.IsNaN(z1) || !ray.IntersectWhere(p1or4.Set(xNorth, yWest, z1), p2, p3, contactPoint))
            && (float.IsNaN(z4) || !ray.IntersectWhere(p1or4.Set(xSouth, yEast, z4), p2, p3, contactPoint)))
            return false;
        float distance = contactPoint.Distance(ray.origin);
        if (distance > ray.GetLimit())
            return false;
        if (results != null)
        {
            if (results.ShouldInvalidateSlopingSurface() && GetMaximumZDiff(p1or4, p2, p3) > HEIGHTMAP_UNIT_SIZE) // height diff >2m means >45° elevation
                contactPoint.SetZ(float.NaN);
            results.AddCollision(new CollisionResult(contactPoint, distance));
        }
        return true;
    }

    /// <summary>
    /// z value at the given heightmap grid index per game logic. The game renders n+1 terrain points
    /// per axis (max coords inclusive, all z=0); perimeter points (x or y == 0 or size) are forced to z=0.
    /// </summary>
    private float GetZ(int xIndex, int yIndex)
    {
        if (xIndex < 0 || yIndex < 0 || xIndex > heightmapXSize || yIndex > heightmapYSize)
            return float.NaN;
        if (xIndex == 0 || yIndex == 0 || xIndex == heightmapXSize || yIndex == heightmapYSize)
            return 0;
        if (heightmap!.Length == 1) // simple flat terrain (memory optimized)
            return GetZ(0);
        return GetZ(yIndex + (xIndex * heightmapYSize));
    }

    /// <summary>z value at the given heightmap index.</summary>
    private float GetZ(int index)
    {
        return heightmap![index] == -1 ? float.NaN : (heightmap[index] & 0xFFFF) * HEIGHTMAP_MAX_Z_EXCLUSIVE / (0xFFFF + 1f);
    }

    public int GetTerrainMaterialAt(float x, float y)
    {
        if (materials == null)
            return 0;
        int mat1x = (int)(x / HEIGHTMAP_UNIT_SIZE);
        int mat1y = (int)(y / HEIGHTMAP_UNIT_SIZE);
        if (mat1x < 0 || mat1y < 0 || mat1x >= materialsXSize || mat1y >= materialsYSize)
            return 0;
        int mat1Index = mat1y + (mat1x * materialsYSize);
        int mat3Index = mat1Index + materialsYSize;
        int mat = materials[mat1Index];
        // check whether triangle points p1, p2, p3 have materials assigned
        if (mat != 0 && mat == materials[mat1Index + 1] && mat == materials[mat3Index])
        {
            if (IsLeft(x + HEIGHTMAP_UNIT_SIZE, y, x, y + HEIGHTMAP_UNIT_SIZE, x, y)) // check if x, y is in triangle
            {
                return materials[mat1Index] & 0xFF;
            }
        }
        if ((mat3Index + 1) < materials.Length && (mat = materials[mat3Index + 1]) != 0 && mat == materials[mat3Index] && mat == materials[mat1Index + 1]) // check whether triangle points p2, p3, p4 have materials assigned
        {
            if (!IsLeft(x + HEIGHTMAP_UNIT_SIZE, y, x, y + HEIGHTMAP_UNIT_SIZE, x, y)) // check if x, y is in triangle
            {
                return materials[mat3Index + 1] & 0xFF;
            }
        }
        return 0;
    }

    /// <summary>True if (targetX, targetY) is left of the line (startX, startY) → (endX, endY).</summary>
    private bool IsLeft(float startX, float startY, float endX, float endY, float targetX, float targetY)
    {
        return (endX - startX) * (targetY - startY) > (endY - startY) * (targetX - startX);
    }

    private float GetMaximumZDiff(Vector3f v1, Vector3f v2, Vector3f v3)
    {
        return JMath.Max(v1.Z, JMath.Max(v2.Z, v3.Z)) - JMath.Min(v1.Z, JMath.Min(v2.Z, v3.Z));
    }
}
