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
    private int worldOptions;

    public WorldMap(WorldMapTemplate worldMapTemplate)
    {
        this.worldMapTemplate = worldMapTemplate;
        this.worldOptions = worldMapTemplate.GetFlags();

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

    public int GetDeathLevel()
    {
        return worldMapTemplate.GetDeathLevel();
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

    public bool IsFlightAllowed()
    {
        return (worldOptions & (int)ZoneAttributes.Fly) != 0;
    }

    public bool IsExceptBuff()
    {
        return worldMapTemplate.IsExceptBuff();
    }

    public bool CanGlide()
    {
        return (worldOptions & (int)ZoneAttributes.Glide) != 0;
    }

    public bool CanPutKisk()
    {
        return (worldOptions & (int)ZoneAttributes.Bind) != 0;
    }

    public bool CanRecall()
    {
        return (worldOptions & (int)ZoneAttributes.Recall) != 0;
    }

    public bool CanRide()
    {
        return (worldOptions & (int)ZoneAttributes.Ride) != 0;
    }

    public bool CanFlyRide()
    {
        return (worldOptions & (int)ZoneAttributes.FlyRide) != 0;
    }

    public bool IsPvpAllowed()
    {
        return (worldOptions & (int)ZoneAttributes.PvpEnabled) != 0;
    }

    public bool IsSameRaceDuelsAllowed()
    {
        return (worldOptions & (int)ZoneAttributes.DuelSameRaceEnabled) != 0;
    }

    public bool IsOtherRaceDuelsAllowed()
    {
        return (worldOptions & (int)ZoneAttributes.DuelOtherRaceEnabled) != 0;
    }

    public bool CanReturnToBattle()
    {
        return (worldOptions & (int)ZoneAttributes.NoReturnBattle) == 0;
    }

    public void SetWorldOption(ZoneAttributes option)
    {
        worldOptions |= (int)option;
    }

    public void RemoveWorldOption(ZoneAttributes option)
    {
        worldOptions &= ~(int)option;
    }

    public bool HasOverridenOption(ZoneAttributes option)
    {
        if ((worldMapTemplate.GetFlags() & (int)option) == 0)
            return (worldOptions & (int)option) != 0;
        return (worldOptions & (int)option) == 0;
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
        return instances.Values.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <summary>All instance ids of this map</summary>
    public ICollection<int> GetAvailableInstanceIds()
    {
        return instances.Keys;
    }

    public void ForEachObject(Action<VisibleObject> consumer)
    {
        foreach (WorldMapInstance instance in instances.Values)
            instance.ForEachObject(consumer);
    }
}
