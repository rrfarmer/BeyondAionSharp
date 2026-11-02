using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.World;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.World;

/// <summary>
/// Java parity: world/WorldMap (-Nemesiss-). One in-game map that can have instances. Iterable&lt;WorldMapInstance&gt; -> IEnumerable;
/// AtomicInteger -> int + Interlocked.Increment; ConcurrentHashMap -> ConcurrentDictionary; Consumer -> Action; ZoneAttributes.X.getId()
/// -> (int)ZoneAttributes.X (enum value is the bit id); GeneralInstanceHandler::new -> inst => new GeneralInstanceHandler(inst).
/// WorldMapTemplate/WorldType/WorldDropType red-tolerated.
/// </summary>
public class WorldMap : IEnumerable<WorldMapInstance>
{
    private readonly WorldMapTemplate worldMapTemplate;
    private int nextInstanceId = 0;
    private readonly ConcurrentDictionary<int, WorldMapInstance> instances = new ConcurrentDictionary<int, WorldMapInstance>();

    public WorldMap(WorldMapTemplate worldMapTemplate)
    {
        this.worldMapTemplate = worldMapTemplate;

        for (int i = 1; i <= GetInstanceCount(); i++)
        {
            if (IsInstanceType()) // default instances are inaccessible but its handler methods are sometimes called via MainWorldMapInstance, e.g. on relog
                WorldMapInstanceFactory.CreateWorldMapInstance(this, 0, inst => new GeneralInstanceHandler(inst), 0);
            else
                WorldMapInstanceFactory.CreateWorldMapInstance(this, 0);
        }
    }

    public string GetName()
    {
        return worldMapTemplate.GetName();
    }

    public int GetWaterLevel()
    {
        return worldMapTemplate.GetWaterLevel();
    }

    public WorldType GetWorldType()
    {
        return worldMapTemplate.GetWorldType();
    }

    public int GetWorldSize()
    {
        return worldMapTemplate.GetWorldSize();
    }

    public WorldDropType GetWorldDropType()
    {
        return worldMapTemplate.GetWorldDropType();
    }

    public int GetMapId()
    {
        return worldMapTemplate.GetMapId();
    }



    public bool IsExceptBuff()
    {
        return worldMapTemplate.IsExceptBuff();
    }

    public int GetInstanceCount()
    {
        int twinCount = worldMapTemplate.GetTwinCount();
        if (twinCount == 0)
            twinCount = 1;
        twinCount += worldMapTemplate.GetBeginnerTwinCount();
        return twinCount;
    }

    /// <summary>
    /// Return a WorldMapInstance - depends on map configuration one map may have twins instances to balance player. This method
    /// will return WorldMapInstance by server chose.
    /// </summary>
    public WorldMapInstance GetMainWorldMapInstance()
    {
        // TODO Balance players into instances.
        return instances.GetValueOrDefault(1);
    }

    public WorldMapInstance GetWorldMapInstance(int instanceId)
    {
        // instanceId is a count, some code still uses 0 for the default instance
        if (instanceId == 0)
            instanceId = 1;
        if (!IsInstanceType())
        {
            if (instanceId > GetInstanceCount())
            {
                throw new ArgumentException("WorldMapInstance " + GetMapId() + " has lower instances count than " + instanceId);
            }
        }
        return instances.GetValueOrDefault(instanceId);
    }

    /// <summary>Remove WorldMapInstance by instanceId.</summary>
    public void RemoveWorldMapInstance(int instanceId)
    {
        // instanceId is a count, some code still uses 0 for the default instance
        if (instanceId == 0)
            instanceId = 1;
        instances.TryRemove(instanceId, out _);
    }

    /// <summary>Add instance to map</summary>
    public void AddInstance(int instanceId, WorldMapInstance instance)
    {
        // instanceId is a count, some code still uses 0 for the default instance
        if (instanceId == 0)
            instanceId = 1;
        instances[instanceId] = instance;
    }

    public WorldMapTemplate GetTemplate()
    {
        return worldMapTemplate;
    }

    /// <summary>The nextInstanceId</summary>
    public int GetNextInstanceId()
    {
        return Interlocked.Increment(ref nextInstanceId);
    }

    /// <summary>Whether this world map is instance type</summary>
    public bool IsInstanceType()
    {
        return worldMapTemplate.IsInstance();
    }

    public IEnumerator<WorldMapInstance> GetEnumerator()
    {
        return DeterministicIteration.ByIntKey(instances.Values, instance => instance.GetInstanceId()).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <summary>All instance ids of this map</summary>
    public ICollection<int> GetAvailableInstanceIds()
    {
        return ThreadPoolManager.IsDeterministicMode ? instances.Keys.OrderBy(id => id).ToArray() : instances.Keys;
    }

    public void ForEachObject(Action<VisibleObject> consumer)
    {
        foreach (WorldMapInstance instance in DeterministicIteration.ByIntKey(instances.Values, instance => instance.GetInstanceId()))
            instance.ForEachObject(consumer);
    }
}
