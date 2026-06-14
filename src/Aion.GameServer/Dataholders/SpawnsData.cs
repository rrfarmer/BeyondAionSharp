using System.Collections.Concurrent;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Event;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Model.Templates.Spawns.Basespawns;
using Aion.GameServer.Model.Templates.Spawns.Mercenaries;
using Aion.GameServer.Model.Templates.Spawns.Panesterra;
using Aion.GameServer.Model.Templates.Spawns.Riftspawns;
using Aion.GameServer.Model.Templates.Spawns.Siegespawns;
using Aion.GameServer.Model.Templates.Spawns.Vortexspawns;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Utils.Xml;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/SpawnsData.</summary>
[XmlRoot("spawns")]
[XmlType(Namespace = "", TypeName = "SpawnsData")]
public class SpawnsData
{
    private static readonly ILogger<SpawnsData> Log = NullLogger<SpawnsData>.Instance;

    [XmlElement("spawn_map", Type = typeof(SpawnMap))]
    public List<SpawnMap>? Templates { get; set; }

    // Runtime-built lookup maps — XmlIgnore / equivalent of @XmlTransient
    [XmlIgnore] private readonly ConcurrentDictionary<int, Dictionary<int, List<SpawnGroup>>> _allSpawnMaps = new();
    [XmlIgnore] private readonly Dictionary<int, List<SpawnGroup>> _baseSpawnMaps    = [];
    [XmlIgnore] private readonly Dictionary<int, List<SpawnGroup>> _riftSpawnMaps    = [];
    [XmlIgnore] private readonly Dictionary<int, List<SpawnGroup>> _siegeSpawnMaps   = [];
    [XmlIgnore] private readonly Dictionary<int, List<SpawnGroup>> _vortexSpawnMaps  = [];
    [XmlIgnore] private readonly Dictionary<int, MercenarySpawn>   _mercenarySpawns  = [];
    [XmlIgnore] private readonly Dictionary<int, List<SpawnGroup>> _ahserionSpawnMaps = []; // Ahserion's Flight

    // ── Java afterUnmarshal ──────────────────────────────────────────────────
    /// <summary>
    /// Call after XML deserialization. Pass the parent object so that event
    /// spawn templates are kept (parity: when parent is EventTemplate, templates
    /// are not nulled out).
    /// </summary>
    public void Initialize(object? parent = null)
    {
        if (Templates == null) return;
        foreach (var map in Templates)
        {
            AddRegularSpawns(map);
            AddBaseSpawns(map);
            AddRiftSpawns(map);
            AddSiegeSpawns(map);
            AddVortexSpawns(map);
            AddMercenarySpawns(map);
            AddAhserionSpawns(map);
        }
        if (parent is not EventTemplate)
            Templates = null;
    }

    // ── spawn-map builders ───────────────────────────────────────────────────
    public void AddRegularSpawns(SpawnMap map)
    {
        _allSpawnMaps.AddOrUpdate(
            map.GetMapId(),
            _ => BuildRegularSpawnMap(map, []),
            (_, existing) => BuildRegularSpawnMap(map, existing));
    }

    private static Dictionary<int, List<SpawnGroup>> BuildRegularSpawnMap(
        SpawnMap map, Dictionary<int, List<SpawnGroup>> mapSpawns)
    {
        var customs = new List<int>();
        foreach (var spawn in map.GetSpawns())
        {
            if (customs.Contains(spawn.GetNpcId())) continue;
            if (spawn.IsCustomSpawn() && spawn.GetEventTemplate() == null)
            {
                mapSpawns.Remove(spawn.GetNpcId());
                customs.Add(spawn.GetNpcId());
            }
            if (!mapSpawns.TryGetValue(spawn.GetNpcId(), out var groups))
            {
                groups = new List<SpawnGroup>(1);
                mapSpawns[spawn.GetNpcId()] = groups;
            }
            groups.Add(new SpawnGroup(map.GetMapId(), spawn));
        }
        return mapSpawns;
    }

    private void AddBaseSpawns(SpawnMap map)
    {
        foreach (var baseSpawn in map.GetBaseSpawns())
        {
            var baseId = baseSpawn.GetId();
            if (!_baseSpawnMaps.TryGetValue(baseId, out var list))
                _baseSpawnMaps[baseId] = list = [];
            foreach (var occupierTemplate in baseSpawn.GetOccupierTemplates() ?? [])
                foreach (var spawn in occupierTemplate.GetSpawns() ?? [])
                    list.Add(new SpawnGroup(map.GetMapId(), spawn, baseId, occupierTemplate.GetOccupier()));
        }
    }

    private void AddRiftSpawns(SpawnMap map)
    {
        foreach (var rift in map.GetRiftSpawns())
        {
            if (!_riftSpawnMaps.TryGetValue(rift.GetId(), out var list))
                _riftSpawnMaps[rift.GetId()] = list = [];
            foreach (var spawn in rift.GetSpawns())
                list.Add(new SpawnGroup(map.GetMapId(), spawn, rift.GetId()));
        }
    }

    private void AddSiegeSpawns(SpawnMap map)
    {
        foreach (var siegeSpawn in map.GetSiegeSpawns())
        {
            var siegeId = siegeSpawn.GetSiegeId();
            if (!_siegeSpawnMaps.TryGetValue(siegeId, out var list))
                _siegeSpawnMaps[siegeId] = list = [];
            foreach (var race in siegeSpawn.GetSiegeRaceTemplates() ?? [])
                foreach (var mod in race.GetSiegeModTemplates() ?? [])
                {
                    if (mod.GetSpawns() == null) continue;
                    foreach (var spawn in mod.GetSpawns()!)
                        list.Add(new SpawnGroup(map.GetMapId(), spawn, siegeId, race.GetSiegeRace(), mod.GetSiegeModType()));
                }
        }
    }

    private void AddVortexSpawns(SpawnMap map)
    {
        foreach (var vortexSpawn in map.GetVortexSpawns())
        {
            var id = vortexSpawn.GetId();
            if (!_vortexSpawnMaps.TryGetValue(id, out var list))
                _vortexSpawnMaps[id] = list = [];
            foreach (var typeTemplate in vortexSpawn.GetStateTemplates() ?? [])
                foreach (var spawn in typeTemplate.GetSpawns() ?? [])
                    list.Add(new SpawnGroup(map.GetMapId(), spawn, id, typeTemplate.GetStateType()));
        }
    }

    private void AddMercenarySpawns(SpawnMap map)
    {
        foreach (var mercenarySpawn in map.GetMercenarySpawns())
        {
            var id = mercenarySpawn.GetSiegeId();
            _mercenarySpawns[id] = mercenarySpawn;
            foreach (var mrace in mercenarySpawn.GetMercenaryRaces() ?? [])
                foreach (var mzone in mrace.GetMercenaryZones() ?? [])
                {
                    mzone.SetWorldId(map.GetMapId());
                    mzone.SetSiegeId(mercenarySpawn.GetSiegeId());
                }
        }
    }

    private void AddAhserionSpawns(SpawnMap map)
    {
        foreach (var ahserionSpawn in map.GetAhserionSpawns())
        {
            var teamId = (int)ahserionSpawn.GetFaction();
            if (!_ahserionSpawnMaps.TryGetValue(teamId, out var list))
                _ahserionSpawnMaps[teamId] = list = [];
            foreach (var stageTemplate in ahserionSpawn.GetStageSpawnTemplate() ?? [])
            {
                if (stageTemplate.GetSpawns() == null) continue;
                foreach (var spawn in stageTemplate.GetSpawns()!)
                    list.Add(new SpawnGroup(map.GetMapId(), spawn, stageTemplate.GetStage(), ahserionSpawn.GetFaction()));
            }
        }
    }

    // ── queries ──────────────────────────────────────────────────────────────
    public List<SpawnGroup> GetSpawnsByWorldId(int worldId)
    {
        if (!_allSpawnMaps.TryGetValue(worldId, out var byNpc))
            return [];
        return byNpc.Values.SelectMany(g => g).ToList();
    }

    public List<SpawnGroup> GetSpawnsForNpc(int worldId, int npcId)
    {
        if (!_allSpawnMaps.TryGetValue(worldId, out var byNpc))
            return [];
        return byNpc.TryGetValue(npcId, out var groups) ? groups : [];
    }

    public List<SpawnGroup>?  GetBaseSpawnsByLocId(int id)      => _baseSpawnMaps.GetValueOrDefault(id);
    public List<SpawnGroup>?  GetRiftSpawnsByLocId(int id)      => _riftSpawnMaps.GetValueOrDefault(id);
    public List<SpawnGroup>?  GetSiegeSpawnsByLocId(int siegeId) => _siegeSpawnMaps.GetValueOrDefault(siegeId);
    public List<SpawnGroup>?  GetVortexSpawnsByLocId(int id)    => _vortexSpawnMaps.GetValueOrDefault(id);
    public MercenarySpawn?    GetMercenarySpawnBySiegeId(int id) => _mercenarySpawns.GetValueOrDefault(id);
    public List<SpawnGroup>?  GetAhserionSpawnByTeamId(int id)  => _ahserionSpawnMaps.GetValueOrDefault(id);
    public List<SpawnMap>?    GetTemplates()                    => Templates;
    public int                Size()                            => _allSpawnMaps.Count;

    public void RemoveEventSpawnObjects(EventTemplate eventTemplate)
    {
        foreach (var byNpc in _allSpawnMaps.Values)
        {
            foreach (var key in byNpc.Keys.ToList())
            {
                byNpc[key].RemoveAll(sg => eventTemplate.Equals(sg.GetEventTemplate()));
                if (byNpc[key].Count == 0)
                    byNpc.Remove(key);
            }
        }
    }

    public void AddAllNpcIdsToSet(ISet<int> npcIds)
    {
        foreach (var byNpc in _allSpawnMaps.Values)
            foreach (var id in byNpc.Keys)
                npcIds.Add(id);

        foreach (var ms in _mercenarySpawns.Values)
            foreach (var mr in ms.GetMercenaryRaces() ?? [])
                foreach (var mz in mr.GetMercenaryZones() ?? [])
                    foreach (var s in mz.GetSpawns() ?? [])
                        npcIds.Add(s.GetNpcId());

        foreach (var map in new[] { _baseSpawnMaps, _riftSpawnMaps, _siegeSpawnMaps, _vortexSpawnMaps, _ahserionSpawnMaps })
            foreach (var list in map.Values)
                foreach (var sg in list)
                    npcIds.Add(sg.GetNpcId());
    }

    // ── spawn search (Java parity: getNearestSpawnByNpcId / getFirstSpawnByNpcId) ──
    public SpawnSearchResult? GetNearestSpawnByNpcId(Player? player, int npcId, int worldId)
    {
        List<SpawnGroup> spawns = GetSpawnsForNpc(worldId, npcId);
        if (spawns.Count == 0)
        {
            // search all maps of player's race
            foreach (var template in DataManager.WORLD_MAPS_DATA)
            {
                if (template.GetMapId() == worldId)
                    continue;
                if ((template.GetWorldType() == WorldType.ELYSEA && player?.GetRace() == Race.ELYOS)
                    || (template.GetWorldType() == WorldType.ASMODAE && player?.GetRace() == Race.ASMODIANS))
                {
                    spawns = GetSpawnsForNpc(template.GetMapId(), npcId);
                    if (spawns.Count != 0)
                    {
                        worldId = template.GetMapId();
                        break;
                    }
                }
            }

            // search all other maps
            if (spawns.Count == 0)
            {
                foreach (var template in DataManager.WORLD_MAPS_DATA)
                {
                    if ((template.GetMapId() == worldId)
                        || (template.GetWorldType() == WorldType.ELYSEA && player?.GetRace() == Race.ELYOS)
                        || (template.GetWorldType() == WorldType.ASMODAE && player?.GetRace() == Race.ASMODIANS))
                        continue;
                    spawns = GetSpawnsForNpc(template.GetMapId(), npcId);
                    if (spawns.Count != 0)
                    {
                        worldId = template.GetMapId();
                        break;
                    }
                }
            }
        }

        return GetNearestSpawn(player?.GetPosition(), spawns, worldId);
    }

    private SpawnSearchResult? GetNearestSpawn(WorldPosition? position, List<SpawnGroup> spawnGroups, int worldId)
    {
        if (position == null || spawnGroups.Count == 0)
            return null;

        if (worldId != position.GetMapId())
        {
            var spawnGroup = spawnGroups[0];
            return spawnGroup.GetSpawnTemplates().Count == 0 ? null : ToSpawnSearchResult(worldId, spawnGroup.GetSpawnTemplates()[0]);
        }

        SpawnTemplate? temp = null;
        float distance = 0;
        foreach (var spawnGroup in spawnGroups)
        {
            foreach (var spot in spawnGroup.GetSpawnTemplates())
            {
                if (temp == null)
                {
                    temp = spot;
                    distance = (float)PositionUtil.GetDistance(position.GetX(), position.GetY(), position.GetZ(), spot.GetX(), spot.GetY(), spot.GetZ());
                    if (distance <= 1f)
                        return ToSpawnSearchResult(worldId, temp);
                }
                else
                {
                    float dist = (float)PositionUtil.GetDistance(position.GetX(), position.GetY(), position.GetZ(), spot.GetX(), spot.GetY(), spot.GetZ());
                    if (dist < distance)
                    {
                        distance = dist;
                        temp = spot;
                        if (distance <= 1f)
                            return ToSpawnSearchResult(worldId, temp);
                    }
                }
            }
        }

        return temp == null ? null : ToSpawnSearchResult(worldId, temp);
    }

    private static SpawnSearchResult ToSpawnSearchResult(int worldId, SpawnTemplate spot)
        => new(worldId, new SpawnSpotTemplate(spot.GetX(), spot.GetY(), spot.GetZ(), spot.GetHeading(),
            spot.GetRandomWalkRange(), spot.GetWalkerId(), spot.GetWalkerIndex()));

    public SpawnSearchResult? GetFirstSpawnByNpcId(int worldId, int npcId)
    {
        List<SpawnGroup> spawnGroups = GetSpawnsForNpc(worldId, npcId);
        if (spawnGroups.Count == 0)
        {
            foreach (var template in DataManager.WORLD_MAPS_DATA)
            {
                if (template.GetMapId() == worldId)
                    continue;
                spawnGroups = GetSpawnsForNpc(template.GetMapId(), npcId);
                if (spawnGroups.Count != 0)
                {
                    worldId = template.GetMapId();
                    break;
                }
            }
            if (spawnGroups.Count == 0)
                return null;
        }
        List<SpawnTemplate> spawnSpots = spawnGroups[0].GetSpawnTemplates();
        return spawnSpots.Count == 0 ? null : ToSpawnSearchResult(worldId, spawnSpots[0]);
    }

    // TODO-backlog: saveSpawn(VisibleObject, bool) — needs VisibleObject, WorldMapInstance, JAXBUtil.
    // TODO-backlog: getRelativePath(VisibleObject) — needs VisibleObject, Gatherable, WorldMapInstance.
    // TODO-backlog: loadSpawnsFromTemplateFiles / findSpawnTemplate / positionMatches — helpers for above.
}
