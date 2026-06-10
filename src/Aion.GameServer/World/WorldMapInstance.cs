using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.Commons.Threading;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Team;
using Aion.GameServer.Model.Templates.Quest;
using Aion.GameServer.Model.Templates.World;
using Aion.GameServer.Model.Templates.Zone;
using Aion.GameServer.QuestEngine;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Collections;
using Aion.GameServer.World.Exceptions;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.World;

/// <summary>
/// Java parity: world/WorldMapInstance (-Nemesiss-). World map instance object. Iterable&lt;VisibleObject&gt; -> IEnumerable;
/// Function/Consumer -> Func/Action; ConcurrentHashMap.newKeySet() -> ConcurrentDictionary-backed set (here HashSet under the existing
/// concurrent usage pattern); Future -> ScheduledTask; putIfAbsent != null -> !TryAdd; GeneralTeam&lt;?,?&gt; wildcard erased to
/// &lt;AionObject, ITeamMember&lt;AionObject&gt;&gt;. WorldMap/MapRegion/InstanceHandler/QuestNpc red-tolerated.
/// </summary>
public abstract class WorldMapInstance : IEnumerable<VisibleObject>
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(WorldMapInstance));
    public static readonly int regionSize = WorldConfig.WORLD_REGION_SIZE; // Java static final; C# static readonly (target isn't a compile-time const)

    private readonly WorldMap parent;
    protected readonly Dictionary<int, MapRegion> regions = new Dictionary<int, MapRegion>();
    private readonly ConcurrentDictionary<int, VisibleObject> worldMapObjects = new ConcurrentDictionary<int, VisibleObject>(); // All objects spawned in this world map instance
    private readonly ConcurrentDictionary<int, Npc> worldMapNpcs = new ConcurrentDictionary<int, Npc>(); // All npcs spawned in this world map instance
    private readonly ConcurrentDictionary<int, Player> worldMapPlayers = new ConcurrentDictionary<int, Player>(); // All players spawned in this world map instance
    private readonly ISet<int> registeredObjects = new HashSet<int>();
    private readonly ISet<int> questIds = new HashSet<int>();
    private readonly Dictionary<ZoneName, ZoneInstance> zones;
    private readonly InstanceHandler instanceHandler;
    private readonly int instanceId; // Id of this instance (channel)
    private readonly int maxPlayers;
    private WorldPosition startPos;
    private long lastPlayerLeaveTime;
    private GeneralTeam<AionObject, ITeamMember<AionObject>> registeredTeam;
    private ScheduledTask emptyInstanceTask, updateNearbyQuestsTask;

    public WorldMapInstance(WorldMap parent, int instanceId, int maxPlayers, Func<WorldMapInstance, InstanceHandler> instanceHandlerSupplier)
    {
        this.parent = parent;
        this.zones = ZoneService.GetInstance().GetZoneInstancesByWorldId(parent.GetMapId());
        this.instanceHandler = instanceHandlerSupplier(this);
        this.instanceId = instanceId;
        this.maxPlayers = maxPlayers;
        InitMapRegions();
    }

    public int GetMapId()
    {
        return GetParent().GetMapId();
    }

    public WorldMap GetParent()
    {
        return parent;
    }

    public WorldMapTemplate GetTemplate()
    {
        return parent.GetTemplate();
    }

    /// <summary>
    /// Returns MapRegion that contains coordinates of VisibleObject. If the region doesn't exist, it's created.
    /// </summary>
    internal MapRegion GetRegion(VisibleObject obj)
    {
        return GetRegion(obj.GetX(), obj.GetY(), obj.GetZ());
    }

    /// <summary>
    /// Returns MapRegion that contains given x,y coordinates. If the region doesn't exist, it's created.
    /// </summary>
    public abstract MapRegion GetRegion(float x, float y, float z);

    /// <summary>Create new MapRegion and add link to neighbours.</summary>
    protected abstract MapRegion CreateMapRegion(int regionId);

    protected abstract void InitMapRegions();

    public abstract bool IsPersonal();

    public abstract int GetOwnerId();

    public void AddObject(VisibleObject obj)
    {
        if (!worldMapObjects.TryAdd(obj.GetObjectId(), obj))
        {
            throw new DuplicateAionObjectException(obj, worldMapObjects[obj.GetObjectId()]);
        }
        if (obj is Npc)
        {
            worldMapNpcs[obj.GetObjectId()] = (Npc)obj;
            QuestNpc qNpc = QuestEngine.GetInstance().GetQuestNpc(obj.GetObjectTemplate().GetTemplateId());
            if (qNpc != null)
            {
                bool updateNearbyQuests = false;
                foreach (int id in qNpc.GetOnQuestStart())
                {
                    if (questIds.Add(id))
                    {
                        updateNearbyQuests = true;
                    }
                }
                if (updateNearbyQuests && updateNearbyQuestsTask == null) // delayed with null check to prevent packet spam on multispawns (bases, siege, ...)
                {
                    updateNearbyQuestsTask = ThreadPoolManager.GetInstance().Schedule(() =>
                    {
                        updateNearbyQuestsTask = null;
                        ForEachPlayer(player => player.GetController().UpdateNearbyQuests());
                    }, 1500);
                }
            }
        }
        if (obj is Player player)
        {
            if (GetParent().IsFlightAllowed())
                player.SetInsideZoneType(ZoneType.FLY);
            worldMapPlayers[obj.GetObjectId()] = player;
        }
    }

    public void RemoveObject(AionObject obj)
    {
        worldMapObjects.TryRemove(obj.GetObjectId(), out _);
        if (obj is Player player)
        {
            lastPlayerLeaveTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (GetParent().IsFlightAllowed())
            {
                player.UnsetInsideZoneType(ZoneType.FLY);
                // necessary for fly maps like the abyss (they don't have FlyZones, so no FlyZoneInstance.onLeave() is called)
                player.GetController().OnLeaveFlyArea();
            }
            worldMapPlayers.TryRemove(obj.GetObjectId(), out _);
        }
        else if (obj is Npc)
        {
            worldMapNpcs.TryRemove(obj.GetObjectId(), out _);
        }
    }

    public int GetPlayerCount()
    {
        return worldMapPlayers.Count;
    }

    public List<Player> GetPlayersInside()
    {
        return new List<Player>(worldMapPlayers.Values);
    }

    /// <summary>Gets the player with the given object id in this world map instance, or null.</summary>
    public Player GetPlayer(int objId)
    {
        return worldMapPlayers.GetValueOrDefault(objId);
    }

    public VisibleObject GetObject(int objId)
    {
        return worldMapObjects.GetValueOrDefault(objId);
    }

    public Npc GetNpc(int npcId)
    {
        foreach (Npc npc in worldMapNpcs.Values)
        {
            if (npc != null && npc.GetNpcId() == npcId)
                return npc;
        }
        return null;
    }

    public List<Npc> GetNpcs(params int[] npcIds)
    {
        List<Npc> npcs = new List<Npc>();
        ForEachNpc(npc =>
        {
            foreach (int npcId in npcIds)
            {
                if (npc.GetNpcId() == npcId)
                    npcs.Add(npc);
            }
        });
        return npcs;
    }

    public List<Npc> GetNpcs()
    {
        List<Npc> npcs = new List<Npc>();
        ForEachNpc(npcs.Add);
        return npcs;
    }

    public VisibleObject GetObjectByStaticId(int staticId)
    {
        return worldMapObjects.Values.FirstOrDefault(o => o != null && o.GetSpawn() != null && o.GetSpawn().GetStaticId() == staticId);
    }

    public int GetInstanceId()
    {
        return instanceId;
    }

    public bool IsBeginnerInstance()
    {
        if (parent == null)
            return false;

        if (parent.GetTemplate().IsInstance())
        {
            // TODO: check Karamatis and Ataxiar for exception in FastTrack ?
            // return parent.getTemplate().getBeginnerTwinCount() > 0;
            return false;
        }

        int twinCount = parent.GetTemplate().GetTwinCount();
        if (twinCount == 0)
            twinCount = 1;
        return GetInstanceId() > twinCount;
    }

    public void RegisterTeam(GeneralTeam<AionObject, ITeamMember<AionObject>> team)
    {
        if (registeredTeam != null)
            throw new InvalidOperationException("A team for instance " + instanceId + " of map " + GetMapId() + " is already registered");
        registeredTeam = team;
        Register(team.GetTeamId());
    }

    public void Register(int objectId)
    {
        registeredObjects.Add(objectId);
    }

    public ISet<int> GetRegisteredObjects()
    {
        return registeredObjects;
    }

    /// <summary>
    /// Count of all registered objects with this instance. Since objects can be players or teams, it does not resemble registered player count.
    /// </summary>
    public int GetRegisteredCount()
    {
        return registeredObjects.Count;
    }

    public bool IsRegistered(int objectId)
    {
        return registeredObjects.Contains(objectId);
    }

    public ScheduledTask GetEmptyInstanceTask()
    {
        return emptyInstanceTask;
    }

    public void SetEmptyInstanceTask(ScheduledTask emptyInstanceTask)
    {
        this.emptyInstanceTask = emptyInstanceTask;
    }

    public GeneralTeam<AionObject, ITeamMember<AionObject>> GetRegisteredTeam()
    {
        return registeredTeam;
    }

    public ISet<int> GetQuestIds()
    {
        return questIds;
    }

    public InstanceHandler GetInstanceHandler()
    {
        return instanceHandler;
    }

    public void SetStartPos(WorldPosition startPos)
    {
        this.startPos = startPos;
    }

    public WorldPosition GetStartPos()
    {
        return startPos;
    }

    protected ZoneInstance[] FilterZones(int mapId, int regionId, float startX, float startY, float minZ, float maxZ)
    {
        RegionZone regionZone = new RegionZone(startX, startY, minZ, maxZ);
        return zones.Values.Where(zoneInstance =>
        {
            if (zoneInstance.GetAreaTemplate().IntersectsRectangle(regionZone))
                return true;
            if (zoneInstance.GetZoneTemplate().GetZoneType() == ZoneClassName.DUMMY)
                log.LogError("Region " + regionId + " should intersect with whole map zone!!! (map=" + mapId + ")");
            return false;
        }).ToArray();
    }

    public bool IsInsideZone(VisibleObject obj, ZoneName zoneName)
    {
        ZoneInstance zoneTemplate = zones.GetValueOrDefault(zoneName);
        return zoneTemplate != null && IsInsideZone(obj.GetPosition(), zoneName);
    }

    public bool IsInsideZone(WorldPosition pos, ZoneName zoneName)
    {
        MapRegion mapRegion = this.GetRegion(pos.GetX(), pos.GetY(), pos.GetZ());
        return mapRegion.IsInsideZone(zoneName, pos.GetX(), pos.GetY(), pos.GetZ());
    }

    public int GetMaxPlayers()
    {
        return maxPlayers;
    }

    public bool IsFull()
    {
        return maxPlayers > 0 && GetPlayerCount() >= maxPlayers;
    }

    public long GetLastPlayerLeaveTime()
    {
        return lastPlayerLeaveTime;
    }

    public void SetDoorState(int staticId, bool open)
    {
        foreach (VisibleObject v in worldMapObjects.Values)
        {
            if (v is StaticDoor staticDoor && v.GetSpawn().GetStaticId() == staticId)
            {
                staticDoor.SetOpen(open);
                return;
            }
        }
        log.LogWarning(new Exception(), "Door (ID: " + staticId + ") doesn't exist");
    }

    public IEnumerator<VisibleObject> GetEnumerator()
    {
        return worldMapObjects.Values.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public void ForEachNpc(Action<Npc> consumer)
    {
        CollectionUtil.ForEach(worldMapNpcs.Values, consumer);
    }

    public void ForEachPlayer(Action<Player> consumer)
    {
        CollectionUtil.ForEach(worldMapPlayers.Values, consumer);
    }

    public void ForEachObject(Action<VisibleObject> consumer)
    {
        CollectionUtil.ForEach(worldMapObjects.Values, consumer);
    }

    public void ForEachDoor(Action<StaticDoor> consumer)
    {
        CollectionUtil.ForEach(worldMapObjects.Values, o =>
        {
            if (o is StaticDoor staticDoor)
                consumer(staticDoor);
        });
    }

    public override string ToString()
    {
        return "WorldMapInstance " + GetMapId() + " [" + instanceId + "]";
    }
}
