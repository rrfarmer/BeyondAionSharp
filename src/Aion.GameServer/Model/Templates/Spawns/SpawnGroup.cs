using Aion.GameServer.Model.Base;
using Aion.GameServer.Model.Siege;
using Aion.GameServer.Model.Templates.Event;
using Aion.GameServer.Model.Templates.Spawns.Basespawns;
using Aion.GameServer.Model.Templates.Spawns.Panesterra;
using Aion.GameServer.Model.Templates.Spawns.Riftspawns;
using Aion.GameServer.Model.Templates.Spawns.Siegespawns;
using Aion.GameServer.Model.Templates.Spawns.Vortexspawns;
using Aion.GameServer.Model.Vortex;
using Aion.GameServer.Services.Panesterra.Ahserion;
using Aion.GameServer.SpawnEngine;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Model.Templates.Spawns;

/// <summary>Java parity: model/templates/spawns/SpawnGroup.</summary>
public class SpawnGroup
{
    private static readonly ILogger<SpawnGroup> Log = NullLogger<SpawnGroup>.Instance;

    private readonly int               _worldId;
    private readonly int               _npcId;
    private readonly int               _pool;
    private readonly int               _respawnTime;
    private readonly byte              _difficultId;
    private readonly SpawnHandlerType? _handlerType;
    private readonly TemporarySpawn?   _temporarySpawn;
    private readonly List<SpawnTemplate> _spots;
    private readonly Dictionary<int, HashSet<SpawnTemplate>> _poolUsedTemplates;
    private readonly EventTemplate?    _eventTemplate;

    // ── public constructors (match Java overloads) ───────────────────────────

    public SpawnGroup(int worldId, int npcId, int respawnTime, EventTemplate? eventTemplate)
        : this(worldId, npcId, 0, respawnTime, 0, null, null, new List<SpawnTemplate>(1), eventTemplate) { }

    public SpawnGroup(int worldId, Spawn spawn)
        : this(worldId, spawn, new List<SpawnTemplate>(spawn.GetSpawnSpotTemplates().Count))
    {
        foreach (var template in spawn.GetSpawnSpotTemplates())
            _spots.Add(new SpawnTemplate(this, template));
    }

    public SpawnGroup(int worldId, Spawn spawn, int id, BaseOccupier occupier)
        : this(worldId, spawn, new List<SpawnTemplate>(spawn.GetSpawnSpotTemplates().Count))
    {
        foreach (var template in spawn.GetSpawnSpotTemplates())
        {
            var st = new BaseSpawnTemplate(this, template);
            st.SetId(id);
            st.SetOccupier(occupier);
            _spots.Add(st);
        }
    }

    public SpawnGroup(int worldId, Spawn spawn, int id)
        : this(worldId, spawn, new List<SpawnTemplate>(spawn.GetSpawnSpotTemplates().Count))
    {
        foreach (var template in spawn.GetSpawnSpotTemplates())
        {
            var st = new RiftSpawnTemplate(this, template);
            st.SetId(id);
            _spots.Add(st);
        }
    }

    public SpawnGroup(int worldId, Spawn spawn, int id, VortexStateType type)
        : this(worldId, spawn, new List<SpawnTemplate>(spawn.GetSpawnSpotTemplates().Count))
    {
        foreach (var template in spawn.GetSpawnSpotTemplates())
        {
            var st = new VortexSpawnTemplate(this, template);
            st.SetId(id);
            st.SetStateType(type);
            _spots.Add(st);
        }
    }

    public SpawnGroup(int worldId, Spawn spawn, int siegeId, SiegeRace race, SiegeModType mod)
        : this(worldId, spawn, new List<SpawnTemplate>(spawn.GetSpawnSpotTemplates().Count))
    {
        foreach (var template in spawn.GetSpawnSpotTemplates())
            _spots.Add(new SiegeSpawnTemplate(siegeId, race, mod, this, template));
    }

    /// <summary>For Ahserion's Flight.</summary>
    public SpawnGroup(int worldId, Spawn spawn, int stage, PanesterraFaction faction)
        : this(worldId, spawn, new List<SpawnTemplate>(spawn.GetSpawnSpotTemplates().Count))
    {
        foreach (var template in spawn.GetSpawnSpotTemplates())
        {
            var st = new AhserionsFlightSpawnTemplate(this, template);
            st.SetStage(stage);
            st.SetPanesterraTeam(faction);
            _spots.Add(st);
        }
    }

    // ── private chain constructors ───────────────────────────────────────────
    private SpawnGroup(int worldId, Spawn spawn, List<SpawnTemplate> spots)
        : this(worldId, spawn.GetNpcId(), spawn.GetPool(), spawn.GetRespawnTime(), spawn.GetDifficultId(),
               spawn.GetSpawnHandlerType(), spawn.GetTemporarySpawn(), spots, spawn.GetEventTemplate()) { }

    private SpawnGroup(int worldId, int npcId, int pool, int respawnTime, byte difficultId,
        SpawnHandlerType? handlerType, TemporarySpawn? temporarySpawn, List<SpawnTemplate> spots,
        EventTemplate? eventTemplate)
    {
        _worldId        = worldId;
        _npcId          = npcId;
        _pool           = pool;
        _respawnTime    = respawnTime;
        _difficultId    = difficultId;
        _handlerType    = handlerType;
        _temporarySpawn = temporarySpawn;
        _spots          = spots;
        _eventTemplate  = eventTemplate;
        _poolUsedTemplates = HasPool() ? [] : new Dictionary<int, HashSet<SpawnTemplate>>(0);
    }

    // ── SpawnHandlerType nullable bridge ─────────────────────────────────────
    // Java SpawnHandlerType is non-nullable; C# is nullable due to optional XML attr.

    // ── methods ──────────────────────────────────────────────────────────────
    public List<SpawnTemplate>  GetSpawnTemplates() => _spots;

    public void AddSpawnTemplate(SpawnTemplate spawnTemplate)
    {
        lock (_spots) { _spots.Add(spawnTemplate); }
    }

    public int             GetWorldId()      => _worldId;
    public int             GetNpcId()        => _npcId;
    public TemporarySpawn? GetTemporarySpawn() => _temporarySpawn;
    public int             GetPool()         => _pool;
    public bool            HasPool()         => _pool > 0;
    public byte            GetDifficultId()  => _difficultId;
    public int             GetRespawnTime()  => _respawnTime;
    public bool            IsTemporarySpawn() => _temporarySpawn != null;
    public SpawnHandlerType? GetHandlerType() => _handlerType;
    public EventTemplate?  GetEventTemplate() => _eventTemplate;

    public SpawnTemplate? ReserveRandomFreePoolSpot(int instanceId)
    {
        lock (_poolUsedTemplates)
        {
            if (!_poolUsedTemplates.TryGetValue(instanceId, out var occupied))
                _poolUsedTemplates[instanceId] = occupied = new HashSet<SpawnTemplate>(_pool);

            // Java: Rnd.get(filteredList) — pick a random element, null if empty
            var free = _spots.Where(s => !occupied.Contains(s)).ToList();
            if (free.Count == 0)
            {
                Log.LogWarning("All spots are used, could not get random spot for npcId: {NpcId}, worldId: {WorldId}", _npcId, _worldId);
                return null;
            }
            var freeSpot = free[Random.Shared.Next(free.Count)]; // Java: Rnd.get(list)
            occupied.Add(freeSpot);
            return freeSpot;
        }
    }

    public void ResetPoolSpot(int instanceId, SpawnTemplate template)
    {
        lock (_poolUsedTemplates)
        {
            if (_poolUsedTemplates.TryGetValue(instanceId, out var set))
                set.Remove(template);
        }
    }

    /// <summary>Call before each randomization to unset all template use.</summary>
    public void ResetPoolSpots(int instanceId)
    {
        lock (_poolUsedTemplates)
        {
            if (_poolUsedTemplates.TryGetValue(instanceId, out var set))
                set.Clear();
        }
    }
}
