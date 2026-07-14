using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Zone;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.World;

/// <summary>
/// Java parity: world/MapRegion (-Nemesiss-). Just some part of map. Comparator chain -> Comparison; ArrayUtils.add -> Array.Resize;
/// ConcurrentHashMap -> ConcurrentDictionary; synchronized methods -> single lock; volatile kept. ZoneClassName/WorldMapType/ZoneName
/// confirmed PascalCase ports; AiEventType -> AiEventType.Activate/Deactivate. WorldMap.IsInstanceType red-tolerated.
/// </summary>
public class MapRegion
{
    private static readonly Comparison<ZoneInstance> zoneComparator = CompareZonesForOrdering;

    internal static int CompareZonesForOrdering(ZoneInstance a, ZoneInstance b)
    {
        int c = a.GetZoneTemplate().GetZoneType().CompareTo(b.GetZoneTemplate().GetZoneType());
        if (c != 0)
            return c;
        c = a.GetZoneTemplate().GetPriority().CompareTo(b.GetZoneTemplate().GetPriority());
        if (c != 0)
            return c;
        return a.GetZoneTemplate().GetName().Id().CompareTo(b.GetZoneTemplate().GetName().Id());
    }

    private readonly int regionId;
    private readonly WorldMapInstance parent;
    private MapRegion[] neighboursIncludingSelf;
    private readonly ZoneInstance[] zonesSortedByTypeAndPriority;
    private readonly ConcurrentDictionary<int, VisibleObject> objects = new ConcurrentDictionary<int, VisibleObject>();
    private int playerCount;
    private bool regionActive = false;
    private volatile bool deactivationPending = false;
    private readonly object sync = new object();

    internal MapRegion(int id, WorldMapInstance parent, ZoneInstance[] zones)
    {
        this.neighboursIncludingSelf = new MapRegion[] { this };
        this.regionId = id;
        this.parent = parent;
        this.zonesSortedByTypeAndPriority = zones;
        Array.Sort(zonesSortedByTypeAndPriority, zoneComparator);
    }

    public int GetRegionId()
    {
        return regionId;
    }

    public WorldMapInstance GetParent()
    {
        return parent;
    }

    public IDictionary<int, VisibleObject> GetObjects()
    {
        return objects;
    }

    public MapRegion[] GetNeighbours()
    {
        return neighboursIncludingSelf;
    }

    internal void AddNeighbourRegion(MapRegion neighbour)
    {
        MapRegion[] expanded = new MapRegion[neighboursIncludingSelf.Length + 1];
        Array.Copy(neighboursIncludingSelf, expanded, neighboursIncludingSelf.Length);
        expanded[neighboursIncludingSelf.Length] = neighbour;
        neighboursIncludingSelf = expanded;
    }

    internal void Add(VisibleObject obj)
    {
        if (objects.TryAdd(obj.GetObjectId(), obj) && obj is Player && IncrementPlayerCount() == 1)
            Activate();
    }

    internal void Remove(VisibleObject obj)
    {
        if (objects.TryRemove(obj.GetObjectId(), out VisibleObject removed) && removed is Player && DecrementPlayerCount() == 0)
            ScheduleDeactivation();
    }

    private int GetPlayerCount()
    {
        lock (sync)
        {
            return playerCount;
        }
    }

    private int IncrementPlayerCount()
    {
        lock (sync)
        {
            return ++playerCount;
        }
    }

    private int DecrementPlayerCount()
    {
        lock (sync)
        {
            return playerCount == 0 ? 0 : --playerCount;
        }
    }

    private bool SetRegionState(bool active)
    {
        lock (sync)
        {
            if (regionActive == active)
                return false;
            regionActive = active;
            return true;
        }
    }

    private void Activate()
    {
        List<MapRegion> activatedRegions = new List<MapRegion>();
        foreach (MapRegion mapRegion in neighboursIncludingSelf)
        {
            if (mapRegion.SetRegionState(true))
                activatedRegions.Add(mapRegion);
        }
        if (activatedRegions.Count != 0)
            ThreadPoolManager.GetInstance().Execute(() => activatedRegions.ForEach(r => r.NotifyCreatures(AiEventType.Activate)));
    }

    private void ScheduleDeactivation()
    {
        if (deactivationPending)
            return;
        deactivationPending = true;
        ThreadPoolManager.GetInstance().Schedule(() =>
        {
            deactivationPending = false;
            if (GetPlayerCount() == 0)
            {
                foreach (MapRegion mapRegion in neighboursIncludingSelf)
                    mapRegion.TryDeactivate();
            }
        }, 60000);
    }

    private void TryDeactivate()
    {
        if (parent.GetParent().IsInstanceType() || parent.GetMapId() == WorldMapType.TRANSIDIUM_ANNEX.GetId())
            return;
        if (!IsActive() || AnyNeighbourHasPlayers())
            return;
        if (!SetRegionState(false))
            return;
        NotifyCreatures(AiEventType.Deactivate);
    }

    private void NotifyCreatures(AiEventType ev)
    {
        foreach (VisibleObject visObject in objects.Values)
        {
            if (visObject is Creature creature)
                creature.GetAi().OnGeneralEvent(ev);
        }
    }

    public bool IsActive()
    {
        lock (sync)
        {
            return regionActive;
        }
    }

    private bool AnyNeighbourHasPlayers()
    {
        foreach (MapRegion r in neighboursIncludingSelf)
        {
            if (r.GetPlayerCount() > 0)
                return true;
        }
        return false;
    }

    public void RevalidateZones(Creature creature)
    {
        ZoneClassName? zoneType = null;
        bool enteredPriorityZone = false;
        foreach (ZoneInstance zone in zonesSortedByTypeAndPriority)
        {
            if (zoneType != zone.GetZoneTemplate().GetZoneType())
            {
                zoneType = zone.GetZoneTemplate().GetZoneType();
                enteredPriorityZone = false;
            }
            if (!creature.IsSpawned() || enteredPriorityZone || !zone.Revalidate(creature))
            {
                zone.OnLeave(creature);
                continue;
            }
            if (zone.GetZoneTemplate().GetPriority() != 0)
            {
                enteredPriorityZone = true;
            }
            zone.OnEnter(creature);
        }
    }

    public List<ZoneInstance> FindZones(Creature creature)
    {
        List<ZoneInstance> z = new List<ZoneInstance>();
        foreach (ZoneInstance zone in zonesSortedByTypeAndPriority)
        {
            if (zone.IsInsideCreature(creature))
            {
                z.Add(zone);
            }
        }
        return z;
    }

    public bool OnDie(Creature attacker, Creature target)
    {
        foreach (ZoneInstance zone in zonesSortedByTypeAndPriority)
        {
            if (zone.IsInsideCreature(target))
            {
                if (zone.OnDie(attacker, target))
                    return true;
            }
        }
        return false;
    }

    public bool IsInsideZone(ZoneName zoneName, float x, float y, float z)
    {
        foreach (ZoneInstance zone in zonesSortedByTypeAndPriority)
        {
            if (zone.GetZoneTemplate().GetName() != zoneName)
                continue;
            return zone.IsInsideCordinate(x, y, z);
        }
        return false;
    }

    public bool IsInsideZone(ZoneName zoneName, Creature creature)
    {
        foreach (ZoneInstance zone in zonesSortedByTypeAndPriority)
        {
            if (zone.GetZoneTemplate().GetName() == zoneName)
                return zone.IsInsideCreature(creature);
        }
        return false;
    }

    /// <summary>
    /// Item use zones always have the same names instances, while we have unique names; Thus, a special check for item use.
    /// </summary>
    public bool IsInsideItemUseZone(ZoneName zoneName, Creature creature)
    {
        bool checkFortresses = "_ABYSS_CASTLE_AREA_".Equals(zoneName.Name); // some items have this special zonename in uselimits
        foreach (ZoneInstance zone in zonesSortedByTypeAndPriority)
        {
            if (checkFortresses)
            {
                if (zone.GetZoneTemplate().GetZoneType() != ZoneClassName.FORT)
                    continue;
            }
            else if (!zone.GetZoneTemplate().XmlName.StartsWith(zoneName.ToString()))
            {
                continue;
            }
            if (zone.IsInsideCreature(creature))
                return true;
        }
        return false;
    }

    public int GetZoneCount()
    {
        return zonesSortedByTypeAndPriority.Length;
    }
}
