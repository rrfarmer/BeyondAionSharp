# Hollow DataManager holder census

Branch: feature/object-spine-bigbang. "Hollow" = the `DataManager.*_DATA` accessor returns an empty /
self-instantiated object that is never populated from XML at boot, so consumers silently get no data.

## Status

All `DataManager.*_DATA` accessors except one delegate to `StaticData` (`SD.*`) and are populated at boot by
`StaticData.LoadLeafHoldersFromFiles` (leaf holders) or the streaming static_data loader. The proven Big-3
(NPC/ITEM/SKILL) load real XML; QUEST/AI deferred-but-tracked elsewhere.

### HOLLOW (1 remaining)
- **SPAWNS_DATA** — `DataManager.cs:32  public static SpawnsData SPAWNS_DATA { get; } = new();`
  - Self-instantiated empty `SpawnsData`; `Templates`/`_allSpawnMaps` never populated at boot.
  - Only runtime writer: `Event.cs AddRegularSpawns` (event spawns), not boot.
  - Faithful `SpawnsData` class is fully implemented (Initialize + all Add*/queries) — only the loader is
    missing. A reworked parallel (`NpcSpawnTable`/`NpcRiftSpawnTable`/`NpcVortexSpawnTable`) IS loaded at
    boot (StaticData.cs:2475-2478) but is orphaned (no live consumer).
  - IMPACT: `SpawnEngine.SpawnAll()` -> `SpawnInstance` -> `GetSpawnsByWorldId()` returns [] => ZERO regular
    NPC/gatherable spawns at boot. See next-slop-targets.md PART A for the scoped re-port.

### NOT hollow (verified populated at boot)
- TOWN_SPAWNS_DATA (SD.TownSpawns, merged-holder load).
- FLY_RING_DATA / ROAD_DATA / CURING_OBJECTS_DATA (SD.*Dh, TryLoadHolder) — confirmed during PART B wiring;
  return empty (not null) lists when their file is absent.
- WORLD_MAPS_DATA, RIFT_DATA, VORTEX_DATA, SIEGE_LOCATION_DATA, BASE_DATA, etc. — all `SD.*`.

## Note on the SPAWNS_DATA reworked parallel
`NpcSpawn*Table` + `*Summary` records (NpcSpawnTable.cs) and the StaticData streaming spawn-builder block are
loaded but dead. They are the deletion target once the faithful `SpawnsData` loader is wired (path (a):
StaticData.Spawns property + `DataManager.SPAWNS_DATA => SD.Spawns`, mirroring TOWN_SPAWNS_DATA).
