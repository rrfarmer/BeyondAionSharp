using System;
using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.GeoEngine.Collision;
using Aion.GameServer.GeoEngine.Math;
using Aion.GameServer.GeoEngine.Scene;
using Aion.GameServer.Model.House;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.GeoEngine.Models;

/// <summary>
/// Java parity: geoEngine/models/GeoMap.
/// </summary>
public class GeoMap : Node
{
    private static readonly ILogger log = NullLogger.Instance;
    public const float COLLISION_CHECK_Z_OFFSET = 1;
    private const float COLLISION_BOUND_OFFSET = 0.5f;
    private const int NODE_CHUNK_SIZE = 256;

    private Terrain? terrain;
    private readonly Dictionary<int, Node> chunkById = new();

    private readonly Dictionary<int, DespawnableNode> despawnables = new();
    private readonly Dictionary<int, List<DespawnableNode>> despawnableTownObjects = new();
    private readonly Dictionary<int, DespawnableNode> despawnableHouseDoors = new();
    private readonly Dictionary<int, DespawnableNode[]> despawnableDoors = new();
    private readonly int mapId;

    public GeoMap(int mapId)
        : base((string?)null)
    {
        this.mapId = mapId;
    }

    public int GetMapId()
    {
        return mapId;
    }

    public override int AttachChild(Spatial child)
    {
        if (child is DespawnableNode desp)
        {
            switch (desp.type)
            {
                case DespawnableNode.DespawnableType.EVENT: // event object
                    break;
                case DespawnableNode.DespawnableType.PLACEABLE: // placeable
                    despawnables[desp.id] = desp;
                    break;
                case DespawnableNode.DespawnableType.HOUSE: // house
                    break;
                case DespawnableNode.DespawnableType.HOUSE_DOOR: // house door
                    despawnableHouseDoors[desp.id] = desp;
                    break;
                case DespawnableNode.DespawnableType.TOWN_OBJECT: // town object
                    if (!despawnableTownObjects.TryGetValue(desp.id, out List<DespawnableNode>? list))
                    {
                        list = new List<DespawnableNode>();
                        despawnableTownObjects[desp.id] = list;
                    }
                    list.Add(desp);
                    break;
                case DespawnableNode.DespawnableType.DOOR_STATE1: // normal door state 1 (closed)
                case DespawnableNode.DespawnableType.DOOR_STATE2: // normal door state 2 (opened)
                    if (!despawnableDoors.TryGetValue(desp.id, out DespawnableNode[]? doorStates))
                    {
                        doorStates = new DespawnableNode[2];
                        despawnableDoors[desp.id] = doorStates;
                    }
                    doorStates[desp.type == DespawnableNode.DespawnableType.DOOR_STATE1 ? 0 : 1] = desp;
                    break;
                default:
                    throw new ArgumentException(desp.type + " is not implemented");
            }
        }
        GetOrCreateChunk(child).AttachChild(child);
        return 0;
    }

    public bool HasTerrain()
    {
        return terrain != null;
    }

    public bool HasTerrainMaterials()
    {
        return terrain != null && terrain.HasMaterials();
    }

    public void SetTerrain(Terrain terrain)
    {
        this.terrain = terrain;
    }

    private Node GetOrCreateChunk(Spatial child)
    {
        int chunkId = RegionUtil.Get2DRegionId(NODE_CHUNK_SIZE, child.GetWorldBound()!.GetCenter().X, child.GetWorldBound()!.GetCenter().Y);
        if (!chunkById.TryGetValue(chunkId, out Node? node))
        {
            node = new Node("");
            chunkById[chunkId] = node;
            base.AttachChild(node);
        }
        return node;
    }

    public int GetEntityCount()
    {
        return chunkById.Values.Sum(m => m.GetChildren().Count);
    }

    /// <summary>
    /// The surface Z coordinate nearest to the given zMax at the position, or NaN if not found / less than zMin.
    /// </summary>
    public float GetZ(float x, float y, float zMax, float zMin, int instanceId)
    {
        return GetZ(x, y, zMax, zMin, instanceId, false);
    }

    public float GetZ(float x, float y, float zMax, float zMin, int instanceId, bool ignoreSlopingSurface)
    {
        CollisionResults results = new CollisionResults(CollisionIntention.PHYSICAL.GetId(), instanceId);
        results.SetInvalidateSlopingSurface(ignoreSlopingSurface);
        Vector3f origin = new Vector3f(x, y, zMax);
        Vector3f target = new Vector3f(x, y, zMin);
        target.SubtractLocal(origin).NormalizeLocal(); // convert to direction vector
        Ray r = new Ray(origin, target);
        r.SetLimit(zMax - zMin);
        CollideWith(r, results);
        if (terrain != null)
            terrain.CollideAtOrigin(r, results);
        CollisionResult? closestCollision = results.GetClosestCollision();
        return closestCollision == null ? float.NaN : closestCollision.GetContactPoint().Z;
    }

    public Vector3f GetClosestCollision(float x, float y, float z, float targetX, float targetY, float targetZ, bool atNearGroundZ, int instanceId,
        sbyte intentions, IgnoreProperties ignoreProperties)
    {
        Vector3f origin = new Vector3f(x, y, z + COLLISION_CHECK_Z_OFFSET);
        CollisionResult? closestCollision = GetCollisions(origin, targetX, targetY, targetZ + COLLISION_CHECK_Z_OFFSET, instanceId, intentions, ignoreProperties).GetClosestCollision();
        if (closestCollision == null)
        {
            Vector3f end = new Vector3f(targetX, targetY, targetZ);
            if (atNearGroundZ)
            {
                float geoZ = GetZ(end.X, end.Y, end.Z + 1, end.Z - 2, instanceId);
                if (!float.IsNaN(geoZ))
                    end.Z = geoZ;
            }
            return end;
        }
        else if (closestCollision.GetDistance() <= COLLISION_BOUND_OFFSET + 0.05f) // avoid climbing steep hills or passing through walls
        {
            return new Vector3f(x, y, z);
        }
        Vector3f contactPoint = closestCollision.GetContactPoint();
        ApplyCollisionCheckOffsets(contactPoint, origin, instanceId);
        return contactPoint;
    }

    private void ApplyCollisionCheckOffsets(Vector3f pos, Vector3f direction, int instanceId)
    {
        ApplyCollisionCheckOffsets(pos, direction, instanceId, false);
    }

    private void ApplyCollisionCheckOffsets(Vector3f pos, Vector3f? direction, int instanceId, bool allowNaN)
    {
        if (direction != null)
        {
            Vector3f dir = pos.Subtract(direction).NormalizeLocal();
            pos.SubtractLocal(dir.MultLocal(COLLISION_BOUND_OFFSET)); // set contact point back for proper ground calculation
            float geoZ = GetZ(pos.X, pos.Y, pos.Z, pos.Z - COLLISION_CHECK_Z_OFFSET * 3, instanceId);
            if (allowNaN || !float.IsNaN(geoZ))
            {
                pos.Z = geoZ;
            }
            else
            {
                pos.Z -= COLLISION_CHECK_Z_OFFSET;
            }
        }
        else
        {
            pos.Z -= COLLISION_CHECK_Z_OFFSET;
        }
    }

    public Vector3f FindMovementCollision(Vector3f origin, float targetX, float targetY, int instanceId)
    {
        // check if we have an obstacle 1m in target direction
        origin.SetZ(origin.GetZ() + COLLISION_CHECK_Z_OFFSET);
        Vector2f targetXY = new Vector2f(targetX, targetY);
        Vector2f xyOffset = targetXY.Subtract(origin.GetX(), origin.GetY()).NormalizeLocal().MultLocal(COLLISION_CHECK_Z_OFFSET);
        float nextX = origin.GetX() + xyOffset.GetX(), nextY = origin.GetY() + xyOffset.GetY();
        if (xyOffset.GetX() >= 0 && nextX > targetX || xyOffset.GetX() < 0 && nextX < targetX)
            nextX = targetX;
        if (xyOffset.GetY() >= 0 && nextY > targetY || xyOffset.GetY() < 0 && nextY < targetY)
            nextY = targetY;
        if (origin.GetX() != nextX || origin.GetY() != nextY)
        {
            CollisionResult? closestCollision = GetCollisions(origin, nextX, nextY, origin.GetZ(), instanceId, CollisionIntention.DEFAULT_COLLISIONS.GetId(), IgnoreProperties.ANY_RACE).GetClosestCollision();
            if (closestCollision != null) // obstacle found within 1m in target direction, return 0.5m offset position or origin if there's no ground
            {
                Vector3f targetPoint = closestCollision.GetContactPoint();
                ApplyCollisionCheckOffsets(targetPoint, origin, instanceId, true);
                if (!float.IsNaN(targetPoint.GetZ()))
                    return targetPoint;
            }
            else // no obstacle 1m in target direction, now check if there's ground to stand on
            {
                float geoZ = GetZ(nextX, nextY, origin.GetZ(), origin.GetZ() - COLLISION_CHECK_Z_OFFSET * 2.5f, instanceId, true);
                if (!float.IsNaN(geoZ)) // there is ground, so we set our origin to the 1m offset position and start over
                    return FindMovementCollision(origin.Set(nextX, nextY, geoZ), targetX, targetY, instanceId);
            }
        }
        return origin.SetZ(origin.GetZ() - COLLISION_CHECK_Z_OFFSET);
    }

    public CollisionResults GetCollisions(float x, float y, float z, float targetX, float targetY, float targetZ, int instanceId, sbyte intentions, IgnoreProperties ignoreProperties)
    {
        return GetCollisions(new Vector3f(x, y, z), targetX, targetY, targetZ, instanceId, intentions, ignoreProperties);
    }

    public CollisionResults GetCollisions(Vector3f origin, float targetX, float targetY, float targetZ, int instanceId, sbyte intentions, IgnoreProperties ignoreProperties)
    {
        CollisionResults results = new CollisionResults(intentions, instanceId, ignoreProperties);
        Vector3f target = new Vector3f(targetX, targetY, targetZ);
        float limit = origin.Distance(target);
        target.SubtractLocal(origin).NormalizeLocal(); // convert to direction vector
        Ray r = new Ray(origin, target);
        r.SetLimit(limit);
        if (terrain != null)
        {
            terrain.Collide(r, targetX, targetY, results);
        }
        CollideWith(r, results);
        return results;
    }

    public bool CanSee(float x, float y, float z, float targetX, float targetY, float targetZ, int instanceId, IgnoreProperties ignoreProperties)
    {
        Vector3f origin = new Vector3f(x, y, z);
        Vector3f target = new Vector3f(targetX, targetY, targetZ);
        float distance = origin.Distance(target);
        if (distance > 80f)
            return false;
        target.SubtractLocal(origin).NormalizeLocal(); // convert to direction vector
        Ray ray = new Ray(origin, target);
        ray.SetLimit(distance);
        if (terrain != null && terrain.Collide(ray, targetX, targetY, null))
            return false;
        CollisionResults results = new CollisionResults(CollisionIntention.CANT_SEE_COLLISIONS.GetId(), instanceId, true, ignoreProperties);
        return CollideWith(ray, results) == 0;
    }

    /// <summary>The terrain materialId at the position if no obstacle is in between, otherwise 0.</summary>
    public int GetTerrainMaterialAt(float x, float y, float z, int instanceId)
    {
        int matId = terrain == null ? 0 : terrain.GetTerrainMaterialAt(x, y);
        if (matId > 0)
        {
            CollisionResults results = new CollisionResults(CollisionIntention.PHYSICAL.GetId(), instanceId);
            float zMax = z + 1;
            float zMin = z - 1;
            Vector3f origin = new Vector3f(x, y, zMax);
            Vector3f target = new Vector3f(x, y, zMin);
            target.SubtractLocal(origin).NormalizeLocal(); // convert to direction vector
            Ray r = new Ray(origin, target);
            r.SetLimit(zMax - zMin);
            terrain!.CollideAtOrigin(r, results);
            CollisionResult? terrainCollision = results.GetClosestCollision();
            if (terrainCollision != null && (CollideWith(r, results) == 0 || results.GetClosestCollision()!.Equals(terrainCollision)))
            {
                return matId;
            }
        }
        return 0;
    }

    public void SpawnPlaceableObject(int instanceId, int staticId)
    {
        if (despawnables.TryGetValue(staticId, out DespawnableNode? node))
        {
            node.SetActive(instanceId, true);
        }
    }

    public void DespawnPlaceableObject(int instanceId, int staticId)
    {
        if (despawnables.TryGetValue(staticId, out DespawnableNode? node))
        {
            node.SetActive(instanceId, false);
        }
    }

    public void UpdateTownToLevel(int townId, int level)
    {
        if (despawnableTownObjects.TryGetValue(townId, out List<DespawnableNode>? list) && list.Count != 0)
        {
            foreach (DespawnableNode despawnableNode in list)
            {
                int levelBitMask = 1 << (level - 1);
                despawnableNode.SetActive(1, (despawnableNode.levelBitMask & levelBitMask) != 0);
            }
        }
    }

    public void SetHouseDoorState(int instanceId, int houseAddress, HouseDoorState state)
    {
        if (despawnableHouseDoors.TryGetValue(houseAddress, out DespawnableNode? houseDoor))
            houseDoor.SetActive(instanceId, state != HouseDoorState.OPEN);
    }

    public void SetDoorState(int instanceId, int doorId, bool open)
    {
        if (!despawnableDoors.TryGetValue(doorId, out DespawnableNode[]? doors))
        {
            if (GeoDataConfig.GEO_ENABLE && !GetIgnorableDoorIds().Contains(doorId))
                log.LogWarning("No geometry found for door " + doorId + " in world " + mapId);
        }
        else
        {
            if (doors[0] != null)
            {
                doors[0].SetActive(instanceId, !open);
            }
            else
            {
                log.LogWarning("Door state 1 not available for door " + doorId + " in world " + mapId);
            }
            if (doors[1] != null)
            {
                doors[1].SetActive(instanceId, open);
            }
            else
            {
                log.LogWarning("Door state 2 not available for door " + doorId + " in world " + mapId);
            }
        }
    }

    private ISet<int> GetIgnorableDoorIds()
    {
        return WorldMapTypeExtensions.GetWorld(mapId) switch
        {
            // TODO mesh is excluded on purpose in geobuilder due to incorrect collision data: objects/npc/level_object/idyun_bridge/idyun_bridge_01a.cga
            WorldMapType.RENTUS_BASE or WorldMapType.OCCUPIED_RENTUS_BASE => new HashSet<int> { 145 },
            // all of the following doors have no collision mesh in the game client (you can walk right through them)
            WorldMapType.ABYSSAL_SPLINTER or WorldMapType.UNSTABLE_SPLINTER => new HashSet<int> { 15, 16, 18, 69 },
            WorldMapType.ATURAM_SKY_FORTRESS => new HashSet<int> { 128, 138, 308, 307 },
            WorldMapType.ESOTERRACE => new HashSet<int> { 78 },
            WorldMapType.Test_MRT_IDZone => new HashSet<int> { 49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 73 },
            WorldMapType.RAKSANG_RUINS => new HashSet<int> { 219 },
            WorldMapType.KAMAR_BATTLEFIELD => new HashSet<int> { 5, 144 },
            _ => new HashSet<int>(),
        };
    }

    public IEnumerable<Geometry> GetGeometries()
    {
        return GetGeometries(GetChildren());
    }

    private static IEnumerable<Geometry> GetGeometries(List<Spatial> spatials)
    {
        foreach (Spatial child in spatials)
        {
            if (child is Geometry geometry)
                yield return geometry;
            else if (child is Node node)
                foreach (Geometry g in GetGeometries(node.GetChildren()))
                    yield return g;
        }
    }
}
