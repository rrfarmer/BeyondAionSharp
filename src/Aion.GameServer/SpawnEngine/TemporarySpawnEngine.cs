using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Event;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.World;

namespace Aion.GameServer.SpawnEngine;

/// <summary>Java parity: spawnengine/TemporarySpawnEngine (xTz, Neon).</summary>
public class TemporarySpawnEngine
{
    private static readonly Dictionary<SpawnGroup, HashSet<int>> spawnGroups = new Dictionary<SpawnGroup, HashSet<int>>();
    private static readonly HashSet<VisibleObject> spawnedObjects = new HashSet<VisibleObject>();
    private static readonly object syncLock = new object();

    public static void OnHourChange()
    {
        lock (syncLock)
        {
            Despawn();
            Spawn();
        }
    }

    private static void Despawn()
    {
        List<VisibleObject> remainingObjects = new List<VisibleObject>(spawnedObjects.Count);
        foreach (VisibleObject @object in spawnedObjects)
        {
            if (@object.GetSpawn().GetTemporarySpawn().CanDespawn())
            {
                @object.GetController().DeleteIfAliveOrCancelRespawn();
            }
            else
            {
                remainingObjects.Add(@object);
            }
        }
        spawnedObjects.IntersectWith(remainingObjects);
    }

    private static void Spawn()
    {
        Dictionary<SpawnGroup, List<VisibleObject>> spawnedBySpawnGroup = spawnedObjects.GroupBy(o => o.GetSpawn().GetGroup()).ToDictionary(g => g.Key, g => g.ToList());
        foreach (KeyValuePair<SpawnGroup, HashSet<int>> entry in spawnGroups)
        {
            SpawnGroup spawn = entry.Key;
            HashSet<int> instanceIds = entry.Value;
            if (instanceIds.Count == 0)
                continue;
            List<VisibleObject> spawned = spawnedBySpawnGroup.TryGetValue(spawn, out List<VisibleObject> s) ? s : new List<VisibleObject>();
            if (spawn.HasPool())
            {
                if (!spawn.GetTemporarySpawn().CanSpawn())
                    continue;
                HashSet<int> spawnableInstanceIds = new HashSet<int>(instanceIds);
                foreach (VisibleObject o in spawned)
                    spawnableInstanceIds.Remove(o.GetInstanceId());
                foreach (int instanceId in spawnableInstanceIds)
                {
                    spawn.ResetPoolSpots(instanceId);
                    for (int pool = 0; pool < spawn.GetPool(); pool++)
                    {
                        SpawnTemplate template = spawn.ReserveRandomFreePoolSpot(instanceId);
                        Aion.GameServer.SpawnEngine.SpawnEngine.SpawnObject(template, instanceId);
                    }
                }
            }
            else
            {
                foreach (SpawnTemplate template in spawn.GetSpawnTemplates())
                {
                    if (!template.GetTemporarySpawn().CanSpawn())
                        continue;
                    HashSet<int> spawnableInstanceIds = new HashSet<int>(instanceIds);
                    foreach (VisibleObject o in spawned.Where(o => o.GetSpawn().Equals(template)))
                        spawnableInstanceIds.Remove(o.GetInstanceId());
                    foreach (int instanceId in spawnableInstanceIds)
                        Aion.GameServer.SpawnEngine.SpawnEngine.SpawnObject(template, instanceId);
                }
            }
        }
    }

    public static void RegisterSpawned(VisibleObject @object)
    {
        lock (syncLock)
        {
            spawnedObjects.Add(@object);
        }
    }

    public static void UnregisterSpawned(int objectId)
    {
        lock (syncLock)
        {
            spawnedObjects.RemoveWhere(o => o.GetObjectId() == objectId);
        }
    }

    public static void AddSpawnGroup(SpawnGroup spawnGroup, int instanceId)
    {
        lock (syncLock)
        {
            if (!spawnGroups.TryGetValue(spawnGroup, out HashSet<int> instanceIds))
            {
                instanceIds = new HashSet<int>();
                spawnGroups[spawnGroup] = instanceIds;
            }
            instanceIds.Add(instanceId);
        }
    }

    public static void Unregister(EventTemplate eventTemplate)
    {
        lock (syncLock)
        {
            spawnedObjects.RemoveWhere(o => o.GetSpawn().GetEventTemplate() == eventTemplate);
            foreach (SpawnGroup s in spawnGroups.Keys.Where(s => s.GetEventTemplate() == eventTemplate).ToList())
                spawnGroups.Remove(s);
        }
    }

    public static void OnInstanceDestroy(WorldMapInstance instance)
    {
        lock (syncLock)
        {
            spawnedObjects.RemoveWhere(o => instance.Equals(o.GetWorldMapInstance()));
            foreach (KeyValuePair<SpawnGroup, HashSet<int>> entry in spawnGroups)
            {
                if (entry.Key.GetWorldId() == instance.GetMapId())
                    entry.Value.Remove(instance.GetInstanceId());
            }
        }
    }
}
