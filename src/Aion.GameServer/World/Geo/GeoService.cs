using System.Collections.Generic;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dataholders;
using Aion.GameServer.GeoEngine;
using Aion.GameServer.GeoEngine.Collision;
using Aion.GameServer.GeoEngine.Math;
using Aion.GameServer.GeoEngine.Models;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.House;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using JMath = System.Math;
using Aion.GameServer.Utils;

namespace Aion.GameServer.World.Geo;

/// <summary>
/// Java parity: world/geo/GeoService (ATracer). The geoEngine entry point: per-world GeoMaps with
/// getZ / canSee / collision queries used by creatures. Genuine Java singleton (SingletonHolder).
/// </summary>
public class GeoService : GameEngine
{

    // GameEngine async lifecycle (infra adapter over the faithful Init()).
    public string Name => GetType().Name;
    public System.Threading.Tasks.ValueTask InitAsync(System.Threading.CancellationToken cancellationToken) { Init(); return System.Threading.Tasks.ValueTask.CompletedTask; }
    public System.Threading.Tasks.ValueTask ShutdownAsync(System.Threading.CancellationToken cancellationToken) => System.Threading.Tasks.ValueTask.CompletedTask;
    private static readonly ILogger log = NullLogger.Instance;

    private readonly Dictionary<int, GeoMap> geoMaps = new();

    public void Init()
    {
        foreach (var map in DataManager.WORLD_MAPS_DATA)
            geoMaps[map.GetMapId()] = new GeoMap(map.GetMapId());
        if (GeoDataConfig.GEO_ENABLE)
        {
            GeoWorldLoader.Load(geoMaps.Values);
        }
        else
        {
            log.LogWarning("Geo data is disabled");
        }
    }

    /// <summary>The surface Z at the object's position, nearest to zMax (NaN if not found / less than zMin).</summary>
    public float GetZ(VisibleObject obj, float zMax, float zMin)
    {
        return GetZ(obj.GetWorldId(), obj.GetX(), obj.GetY(), zMax, zMin, obj.GetInstanceId());
    }

    public float GetZ(int worldId, float x, float y, float z, int instanceId)
    {
        return GetZ(worldId, x, y, z + 2, z - 2, instanceId);
    }

    public float GetZ(int worldId, float x, float y, float zMax, float zMin, int instanceId)
    {
        return geoMaps[worldId].GetZ(x, y, zMax, zMin, instanceId);
    }

    public CollisionResults GetCollisions(VisibleObject obj, float x, float y, float z, sbyte intentions, IgnoreProperties ignoreProperties)
    {
        return geoMaps[obj.GetWorldId()].GetCollisions(obj.GetX(), obj.GetY(), obj.GetZ() + GetSeeCheckOffset(obj), x, y, z,
            obj.GetInstanceId(), intentions, ignoreProperties);
    }

    /// <summary>True if object has unobstructed view on its target.</summary>
    public bool CanSee(VisibleObject obj, VisibleObject target)
    {
        if (!GeoDataConfig.CANSEE_ENABLE)
            return true;

        float objectSeeCheckZ = obj.GetZ() + GetSeeCheckOffset(obj);
        float targetSeeCheckZ = target.GetZ() + GetSeeCheckOffset(target);
        float x = obj.GetX();
        float y = obj.GetY();
        float targetX = target.GetX();
        float targetY = target.GetY();
        if (obj is Npc npc && npc.GetAi().Ask(AIQuestion.CONSIDER_BOUNDS_IN_CAN_SEE_CHECK_WHEN_ATTACKING))
        {
            double rad = PositionUtil.CalculateAngleFrom(obj, target) / 180.0 * JMath.PI;
            x += (float)(JMath.Cos(rad) * obj.GetObjectTemplate().GetBoundRadius().GetMaxOfFrontAndSide());
            y += (float)(JMath.Sin(rad) * obj.GetObjectTemplate().GetBoundRadius().GetMaxOfFrontAndSide());
        }
        if (target is Npc tnpc && tnpc.GetAi().Ask(AIQuestion.CONSIDER_BOUNDS_IN_CAN_SEE_CHECK_WHEN_ATTACKED))
        {
            double rad = PositionUtil.CalculateAngleFrom(target, obj) / 180.0 * JMath.PI;
            targetX += (float)(JMath.Cos(rad) * target.GetObjectTemplate().GetBoundRadius().GetMaxOfFrontAndSide());
            targetY += (float)(JMath.Sin(rad) * target.GetObjectTemplate().GetBoundRadius().GetMaxOfFrontAndSide());
        }
        Race? race = null;
        int staticId = -1;
        if (target.GetSpawn() != null)
        {
            staticId = target.GetSpawn().GetStaticId();
        }
        if (obj is Creature creature)
        {
            race = creature.GetRace();
        }
        IgnoreProperties ignoreProperties = IgnoreProperties.Of(race, staticId);
        return geoMaps[obj.GetWorldId()].CanSee(x, y, objectSeeCheckZ, targetX, targetY, targetSeeCheckZ, obj.GetInstanceId(), ignoreProperties);
    }

    public bool CanSee(VisibleObject obj, float targetX, float targetY, float targetZ, IgnoreProperties ignoreProperties)
    {
        float zOffset = GetSeeCheckOffset(obj);
        return geoMaps[obj.GetWorldId()].CanSee(obj.GetX(), obj.GetY(), obj.GetZ() + zOffset, targetX, targetY, targetZ + zOffset,
            obj.GetInstanceId(), ignoreProperties);
    }

    private float GetSeeCheckOffset(VisibleObject obj)
    {
        float height = obj.GetObjectTemplate().GetBoundRadius().GetUpper();
        if (obj is Player p && p.IsTransformed() && p.GetTransformModel().GetBanMovement() == 1)
        {
            NpcTemplate t = DataManager.NPC_DATA.GetNpcTemplate(p.GetTransformModel().GetModelId());
            if (t != null)
                return t.GetBoundRadius().GetUpper();
        }
        return height > 2.5f ? height / 2 : 1.25f;
    }

    public Vector3f GetClosestCollision(Creature obj, float x, float y, float z)
    {
        return GetClosestCollision(obj, x, y, z, true, CollisionIntention.DEFAULT_COLLISIONS.GetId(), IgnoreProperties.ANY_RACE);
    }

    public Vector3f GetClosestCollision(Creature obj, float x, float y, float z, bool atNearGroundZ, sbyte intentions, IgnoreProperties ignoreProperties)
    {
        return geoMaps[obj.GetWorldId()].GetClosestCollision(obj.GetX(), obj.GetY(), obj.GetZ(), x, y, z, atNearGroundZ,
            obj.GetInstanceId(), intentions, ignoreProperties);
    }

    /// <summary>
    /// Terrain-agnostic check that walks along the terrain, returning only actual obstacles (trees,
    /// walls, steep hills); inclines &lt;= 45° are not collisions.
    /// </summary>
    public Vector3f FindMovementCollision(Creature creature, float directionAngle, float maxDistance)
    {
        double rad = directionAngle / 180.0 * JMath.PI;
        float x1 = (float)(JMath.Cos(rad) * maxDistance);
        float y1 = (float)(JMath.Sin(rad) * maxDistance);
        Vector3f startPos;
        GeoMap map = geoMaps[creature.GetWorldId()];
        if (creature is Player player)
        {
            startPos = CalculateCurrentGeoPosition(player);
            if (creature.IsFlying())
                return map.GetClosestCollision(startPos.GetX(), startPos.GetY(), startPos.GetZ(), startPos.GetX() + x1, startPos.GetY() + y1,
                    startPos.GetZ(), false, creature.GetInstanceId(), CollisionIntention.DEFAULT_COLLISIONS.GetId(), IgnoreProperties.ANY_RACE);
        }
        else
        {
            startPos = new Vector3f(creature.GetX(), creature.GetY(), creature.GetZ());
        }
        return map.FindMovementCollision(startPos, startPos.GetX() + x1, startPos.GetY() + y1, creature.GetInstanceId());
    }

    private Vector3f CalculateCurrentGeoPosition(Player player)
    {
        WorldPosition approximatePos = player.GetPosition();
        WorldPosition? lastPos = player.GetMoveController().GetLastPositionFromClient();
        if (lastPos == null)
            return new Vector3f(approximatePos.GetX(), approximatePos.GetY(), approximatePos.GetZ());
        // client sends CM_MOVE in intervals when moving straight, so we search for possible collisions between lastPos and the server side position
        return geoMaps[approximatePos.GetMapId()].GetClosestCollision(lastPos.GetX(), lastPos.GetY(), lastPos.GetZ(), approximatePos.GetX(),
            approximatePos.GetY(), approximatePos.GetZ(), true, approximatePos.GetInstanceId(), CollisionIntention.DEFAULT_COLLISIONS.GetId(),
            IgnoreProperties.ANY_RACE);
    }

    public void SpawnPlaceableObject(int worldId, int instanceId, int staticId)
    {
        geoMaps[worldId].SpawnPlaceableObject(instanceId, staticId);
    }

    public void DespawnPlaceableObject(int worldId, int instanceId, int staticId)
    {
        geoMaps[worldId].DespawnPlaceableObject(instanceId, staticId);
    }

    public void UpdateTown(Race race, int townId, int level)
    {
        switch (race)
        {
            case Race.ELYOS:
                geoMaps[WorldMapType.ORIEL.GetId()].UpdateTownToLevel(townId, level);
                break;
            case Race.ASMODIANS:
                geoMaps[WorldMapType.PERNON.GetId()].UpdateTownToLevel(townId, level);
                break;
        }
    }

    public void SetHouseDoorState(int worldId, int instanceId, int houseAddress, HouseDoorState state)
    {
        geoMaps[worldId].SetHouseDoorState(instanceId, houseAddress, state);
    }

    public void SetDoorState(int worldId, int instanceId, int doorId, bool open)
    {
        geoMaps[worldId].SetDoorState(instanceId, doorId, open);
    }

    public bool WorldHasTerrainMaterials(int worldId)
    {
        return GeoDataConfig.GEO_MATERIALS_ENABLE && geoMaps[worldId].HasTerrainMaterials();
    }

    public int GetTerrainMaterialAt(int worldId, float x, float y, float z, int instanceId)
    {
        return GeoDataConfig.GEO_MATERIALS_ENABLE ? geoMaps[worldId].GetTerrainMaterialAt(x, y, z, instanceId) : 0;
    }

    public static GeoService GetInstance()
    {
        return SingletonHolder.instance;
    }

    private static class SingletonHolder
    {
        internal static readonly GeoService instance = new GeoService();
    }
}
