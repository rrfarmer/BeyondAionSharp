using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Siege;
using Aion.GameServer.Model.Templates.Event;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Model.Templates.Spawns.Riftspawns;
using Aion.GameServer.Model.Templates.Spawns.Siegespawns;
using Aion.GameServer.Model.Templates.Spawns.Vortexspawns;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Rift;
using Aion.GameServer.World;

namespace Aion.GameServer.SpawnEngine;

/// <summary>
/// Java parity: spawnengine/SpawnEngine (Luno, ATracer, Source, Wakizashi, xTz, nrg). NPC spawn management keystone.
/// instanceof X→is X cast; enum switch-arrows→switch statement; IllegalArgumentException→ArgumentException; slf4j→ILogger; inner Consumer&lt;VisibleObject&gt;→nested StatsCollector w/ Accept method-group. Java method name `forEachParalllel` (triple-l typo) preserved. World/DataManager/VisibleObjectSpawner/RiftManager/HousingService red-tolerated.
/// </summary>
public class SpawnEngine
{
    private static readonly ILogger Log = NullLoggerFactory.Instance.CreateLogger(nameof(SpawnEngine));

    public static VisibleObject SpawnObject(SpawnTemplate spawn, int instanceIndex)
    {
        VisibleObject visObj = GetSpawnedObject(spawn, instanceIndex);
        if (visObj != null)
        {
            if (visObj.GetSpawn() != null && visObj.GetSpawn().IsTemporarySpawn())
                TemporarySpawnEngine.RegisterSpawned(visObj);
            if (visObj.IsSpawned()) // WalkerFormator.ProcessClusteredNpc delays spawn of pooled walkers
                visObj.GetPosition().GetWorldMapInstance().GetInstanceHandler().OnSpawn(visObj);
        }
        return visObj;
    }

    private static VisibleObject GetSpawnedObject(SpawnTemplate spawn, int instanceIndex)
    {
        int npcId = spawn.GetNpcId();

        if (npcId > 400000 && npcId < 499999)
        {
            return VisibleObjectSpawner.SpawnGatherable(spawn, instanceIndex);
        }
        else if (spawn is RiftSpawnTemplate)
        {
            return VisibleObjectSpawner.SpawnRiftNpc((RiftSpawnTemplate)spawn, instanceIndex);
        }
        else if (spawn is SiegeSpawnTemplate)
        {
            return VisibleObjectSpawner.SpawnSiegeNpc((SiegeSpawnTemplate)spawn, instanceIndex);
        }
        else if (spawn is VortexSpawnTemplate)
        {
            return VisibleObjectSpawner.SpawnInvasionNpc((VortexSpawnTemplate)spawn, instanceIndex);
        }
        else
        {
            return VisibleObjectSpawner.SpawnNpc(spawn, instanceIndex);
        }
    }

    /// <summary>Create non-permanent spawn template with no respawn</summary>
    public static SpawnTemplate NewSingleTimeSpawn(int worldId, int npcId, float x, float y, float z, byte heading)
    {
        return NewSpawn(worldId, npcId, x, y, z, heading, 0, 0, null, null);
    }

    public static SpawnTemplate NewSingleTimeSpawn(int worldId, int npcId, float x, float y, float z, byte heading, VisibleObject creator, string aiName)
    {
        int creatorId = creator == null ? 0 : creator.GetObjectId();
        EventTemplate eventTemplate = creator == null || creator.GetSpawn() == null ? null : creator.GetSpawn().GetEventTemplate();
        return NewSpawn(worldId, npcId, x, y, z, heading, 0, creatorId, aiName, eventTemplate);
    }

    public static SpawnTemplate NewSingleTimeSpawn(int worldId, int npcId, float x, float y, float z, byte heading, int creatorId)
    {
        return NewSpawn(worldId, npcId, x, y, z, heading, 0, creatorId, null, null);
    }

    public static SpawnTemplate NewSpawn(int worldId, int npcId, float x, float y, float z, byte heading, int respawnTime)
    {
        return NewSpawn(worldId, npcId, x, y, z, heading, respawnTime, 0, null, null);
    }

    private static SpawnTemplate NewSpawn(int worldId, int npcId, float x, float y, float z, byte heading, int respawnTime, int creatorId,
        string aiName, EventTemplate eventTemplate)
    {
        return new SpawnTemplate(new SpawnGroup(worldId, npcId, respawnTime, eventTemplate), x, y, z, heading, 0, null, 0, creatorId, aiName);
    }

    /// <summary>Should be used when you need to add a siegespawn through code and not from static_data spawns (e.g. CustomBalaurAssault)</summary>
    public static SiegeSpawnTemplate NewSiegeSpawn(int worldId, int npcId, int siegeId, SiegeRace race, SiegeModType mod, float x, float y, float z,
        byte heading)
    {
        return new SiegeSpawnTemplate(siegeId, race, mod, new SpawnGroup(worldId, npcId, 0, null), x, y, z, heading, 0, null, 0);
    }

    internal static void BringIntoWorld(VisibleObject visibleObject, SpawnTemplate spawn, int instanceIndex)
    {
        BringIntoWorld(visibleObject, spawn.GetWorldId(), instanceIndex, spawn.GetX(), spawn.GetY(), spawn.GetZ(), spawn.GetHeading());
    }

    public static void BringIntoWorld(VisibleObject visibleObject, int worldId, int instanceIndex, float x, float y, float z, byte h)
    {
        World world = World.GetInstance();
        world.StoreObject(visibleObject);
        world.SetPosition(visibleObject, worldId, instanceIndex, x, y, z, h);
        world.Spawn(visibleObject);
    }

    public static void BringIntoWorld(VisibleObject visibleObject)
    {
        if (visibleObject.GetPosition() == null)
            throw new ArgumentException("Position is null");
        World world = World.GetInstance();
        world.StoreObject(visibleObject);
        world.Spawn(visibleObject);
    }

    /// <summary>Spawn all NPCs from templates</summary>
    public static void SpawnAll()
    {
        DataManager.WORLD_MAPS_DATA.ForEachParalllel(worldMapTemplate =>
        {
            WorldMap worldMap = World.GetInstance().GetWorldMap(worldMapTemplate.GetMapId());
            if (!worldMap.IsInstanceType())
                worldMap.ForEach(instance => SpawnInstance(instance, (byte)0, instance.GetOwnerId()));
        });
        PrintWorldSpawnStats();
    }

    public static void SpawnInstance(WorldMapInstance instance, byte difficultId, int ownerId)
    {
        SpawnInstance(instance, difficultId, ownerId, null);
    }

    public static void SpawnEventSpawns(WorldMapInstance instance, byte difficultId, int ownerId, EventTemplate eventTemplate)
    {
        SpawnInstance(instance, difficultId, ownerId, eventTemplate);
    }

    private static void SpawnInstance(WorldMapInstance instance, byte difficultId, int ownerId, EventTemplate eventTemplate)
    {
        List<SpawnGroup> worldSpawns = DataManager.SPAWNS_DATA.GetSpawnsByWorldId(instance.GetMapId());
        if (eventTemplate == null)
            StaticDoorSpawnManager.SpawnTemplate(instance);

        int spawnedCounter = 0;
        if (worldSpawns != null)
        {
            foreach (SpawnGroup spawn in worldSpawns)
            {
                if (eventTemplate != null && eventTemplate != spawn.GetEventTemplate())
                    continue;
                if (spawn.GetDifficultId() != 0 && spawn.GetDifficultId() != difficultId)
                    continue;

                if (spawn.IsTemporarySpawn())
                {
                    TemporarySpawnEngine.AddSpawnGroup(spawn, instance.GetInstanceId());
                    if (!spawn.GetTemporarySpawn().IsInSpawnTime())
                        continue;
                }

                if (spawn.GetHandlerType() != null)
                {
                    switch (spawn.GetHandlerType())
                    {
                        case SpawnHandlerType.RIFT:
                            RiftManager.AddRiftSpawnTemplate(spawn);
                            break;
                        case SpawnHandlerType.STATIC:
                            StaticObjectSpawnManager.SpawnTemplate(spawn, instance.GetInstanceId());
                            break;
                    }
                }
                else if (spawn.HasPool() && CheckPool(spawn))
                {
                    spawn.ResetPoolSpots(instance.GetInstanceId());
                    for (int i = 0; i < spawn.GetPool(); i++)
                    {
                        SpawnTemplate template = spawn.ReserveRandomFreePoolSpot(instance.GetInstanceId());
                        if (template == null)
                            break;
                        SpawnObject(template, instance.GetInstanceId());
                        spawnedCounter++;
                    }
                }
                else
                {
                    foreach (SpawnTemplate template in spawn.GetSpawnTemplates())
                    {
                        if (template.GetTemporarySpawn() != null && !template.GetTemporarySpawn().IsInSpawnTime())
                            continue;
                        SpawnObject(template, instance.GetInstanceId());
                        spawnedCounter++;
                    }
                }
            }
            if (eventTemplate == null)
                WalkerFormator.OrganizeAndSpawn(instance.GetMapId(), instance.GetInstanceId());
        }
        if (spawnedCounter > 0)
        {
            if (eventTemplate == null)
                Log.LogInformation("Spawned " + spawnedCounter + " objects in " + instance);
            else
                Log.LogInformation('[' + eventTemplate.GetName() + "] Spawned " + spawnedCounter + " event objects in " + instance);
        }
        if (eventTemplate == null)
            HousingService.GetInstance().SpawnHouses(instance, ownerId);
    }

    public static bool CheckPool(SpawnGroup spawn)
    {
        if (spawn.GetPool() >= spawn.GetSpawnTemplates().Count)
        {
            Log.LogWarning("Spawn pool size must be smaller than spots to take effect, npcId: " + spawn.GetNpcId() + ", worldId: " + spawn.GetWorldId());
            return false;
        }
        return true;
    }

    public static void PrintWorldSpawnStats()
    {
        StatsCollector function = new StatsCollector();
        World.GetInstance().ForEachObject(function.Accept);
        Log.LogInformation("Loaded " + function.GetNpcCount() + " npc spawns");
        Log.LogInformation("Loaded " + function.GetGatherableCount() + " gatherable spawns");
    }

    // Java parity: inner class implements Consumer<VisibleObject>; C# exposes Accept as an Action<VisibleObject> method group.
    internal class StatsCollector
    {
        internal int npcCount;
        internal int gatherableCount;

        public void Accept(VisibleObject obj)
        {
            if (obj is Npc)
            {
                npcCount++;
            }
            else if (obj is Gatherable)
            {
                gatherableCount++;
            }
        }

        public int GetNpcCount()
        {
            return npcCount;
        }

        public int GetGatherableCount()
        {
            return gatherableCount;
        }
    }
}
