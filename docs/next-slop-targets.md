# Next slop / gated targets

Branch: feature/object-spine-bigbang. Faithful 1:1, all-green-or-revert.

## 🎉 OBJECT-SPINE BIG-BANG + REWORKED-SLOP RETIREMENT = COMPLETE (confirmed 2026-06-17, HEAD ba6a80160).
Definitive source-level confirmation this tick: `World/World.cs` has a SINGLE object store `_allObjects` (ConcurrentDictionary<int,VisibleObject>, 1:1 Java `allObjects`) — NO `_objects`/`_housesByObjectId`/`_housesByAddress`/`TryAddObject` dual-store remnant. The object-spine is fully unified to the faithful spine; ZERO residual.
Program scorecard: packet front 126→0 reworked duplicates (GameServerPacket + SerializeFrame dropped, faithful hierarchy unified on AionServerPacket); object store unified + `_objects` deleted; WorldNpc/Kisk/Rift/drop slop retired; ALL StaticData summary-projections retired (8 holders + WorldMapSummary + NpcSpawnTable + FlightZone); Housing subsystem retired; SPAWNS_DATA/CronJobService/DatabaseCleaningService confirmed faithful + wired; 3 latent runtime fidelity bugs fixed (RiftManager fan-out, SmDialogWindow flat-write, abyss silent-no-send). All-green + golden 196/196 byte-exact throughout ~27 batches.
REMAINING = NOT slop, all user-gated: (a) integration-harness sub-project to golden the ~104 live-object SM_* packets (diminishing-return fidelity mining); (b) Front-A real-client enter-world test (needs the USER's Aion client — environment-gated); (c) any new user-directed goal. RECOMMEND: pause the loop, or pick (a)/(b)/(c).

## ✅ SPAWNS_DATA — ALREADY WIRED + LOADED + REGRESSION-GUARDED; NOT slop, NOT broken, NOT a placeholder (scoped 2026-06-17, PLAN-ONLY, no code change).

**VERDICT: FINE. The memory hollow-holder-census "SPAWNS heavy/reworked" note is STALE.** `DataManager.SPAWNS_DATA` was wired to the faithful boot loader in commit **`ae2e25a54`** ("Wire SPAWNS_DATA boot loader: world NPCs now spawn (was hollow -> 0 NPCs)"), which followed `4cc039a41` (retyped the accessor off the reworked `*Summary` projection onto the faithful `dataholders/SpawnsData` + ported `GetNearestSpawnByNpcId`/`GetFirstSpawnByNpcId` 1:1 + repointed 13 spawn consumers). No reworked `SpawnsTable`/`SpawnsSummary` projection survives — grep finds none.

**Definitive state (evidence):**
- **Holder** `Dataholders/SpawnsData.cs` is a faithful 1:1 port of Java `dataholders/SpawnsData.java`: `[XmlRoot("spawns")]` of `[XmlElement("spawn_map")]` rows; `MergePending` + `AfterUnmarshal`->`Initialize(parent)` build the full runtime maps (`_allSpawnMaps`/`_baseSpawnMaps`/`_riftSpawnMaps`/`_siegeSpawnMaps`/`_vortexSpawnMaps`/`_mercenarySpawns`/`_ahserionSpawnMaps`) then null `Templates` unless parent is `EventTemplate` — Java parity. All 7 builders (regular/base/rift/siege/vortex/mercenary/ahserion) + all queries (`GetSpawnsByWorldId`/`GetSpawnsForNpc`/`GetBaseSpawnsByLocId`/`GetRiftSpawnsByLocId`/`GetSiegeSpawnsByLocId`/`GetVortexSpawnsByLocId`/`GetMercenarySpawnBySiegeId`/`GetAhserionSpawnByTeamId`/`AddAllNpcIdsToSet`/`RemoveEventSpawnObjects`/`GetNearestSpawnByNpcId`/`GetFirstSpawnByNpcId`) ported. Only deferred items are 4 TODO-backlog helpers (`saveSpawn`/`getRelativePath`/`loadSpawnsFromTemplateFiles`/`findSpawnTemplate`) — editor/admin-save plumbing, not boot-spawn.
- **Boot load** `StaticData.cs:394`: `SpawnsDh = TryLoadMergedHolder<SpawnsData>(.../spawns, (m,p)=>m.MergePending(p), logger)` — recursively merges every `<spawns>` file under `data/static_data/spawns/` (Npcs/Instances/Bases/Rifts/Sieges/Mercenaries/Statics/Gather/AhserionsFlight) then runs `AfterUnmarshal` once. `DataManager.SPAWNS_DATA => SD.SpawnsDh` (DataManager.cs:40). Same proven merged-holder shape as the other live holders.
- **Live consumers (~20, all faithful):** `SpawnEngine.SpawnInstance` (`GetSpawnsByWorldId` — the boot whole-world spawn path), `RiftService` (`GetRiftSpawnsByLocId`), `SiegeService`/`AgentSiege` (`GetSiegeSpawnsByLocId`), `VortexService` (`GetVortexSpawnsByLocId`), `AhserionRaid` (`GetAhserionSpawnByTeamId`), `Base` (`GetBaseSpawnsByLocId`), `MercenaryLocation` (`GetMercenarySpawnBySiegeId`), `TeleportService`/`MoveTo`/`CM_OBJECT_SEARCH`/`QuestTasks`/`KillSpawned`/`ConquestOfferingPortalAI`/2 event quests (`GetFirstSpawn`/`GetNearestSpawn`), `QuestSpawnAnalyzer` (`AddAllNpcIdsToSet`), `Event` (`AddRegularSpawns`/`RemoveEventSpawnObjects`).
- **Boot proof (regression test):** `GameServerBootstrapTests.GameServerBootstrap_RealSpawnDataMaterializesNpcsIntoWorld` boots the REAL `game-server/data` through `DataManager.LoadAsync(repoRoot)`, asserts `SPAWNS_DATA.GetSpawnsByWorldId(110010000)` (Sanctum) is NON-empty, drives `SpawnEngine.SpawnObject` per spawn template, and asserts real `Npc` instances materialize into the `World` `_allObjects` store (incl. known Sanctum NPC Euterpe npc 798173). The test comment explicitly documents the prior bug: *"before the SPAWNS_DATA fix, DataManager.SPAWNS_DATA was a hollow singleton returning [] for every world, so zero NPCs spawned at boot. It guards that the fix actually turns spawn templates into live world NPCs."* The DB-backed full-boot test (`GameServerBootstrap_DbBackedFullBoot_*`) runs the whole `SpawnEngine.SpawnAll()` against live MySQL.

**No re-port / wire needed.** No bounded sub-step exists (nothing to wire, nothing dead to delete). The only spawn-adjacent work remaining is the WorldNpc-spawn-cluster §7c.1 unification (a SEPARATE coordinated big-bang about the dual object store, NOT SPAWNS_DATA) and the 4 SpawnsData TODO-backlog save/editor helpers (need VisibleObject/WorldMapInstance/JAXBUtil-save surface; not gameplay-load-bearing). **RECOMMENDATION: close SPAWNS_DATA as a target; it is done.**

### Next gated items assessed (read-only, 2026-06-17): CronJobService + DatabaseCleaningService are BOTH already complete faithful ports.
- **`Services/CronJobService.cs`** = faithful 1:1 of Java `CronJobService.java` (Moltenus spawn / Ahserion flight / IdianDepthPortal spawner / weekly LegionDominion calc; anonymous-Runnable persistent-field idiom -> nested classes with fields; `ThreadPoolManager.Schedule(Func<CT,ValueTask>,TimeSpan)` async idiom). **Already wired at boot** — `GameServerBootstrapService.cs:298` `CronJobService.GetInstance()` (Java parity GameServer.main:158, after AtreianPassportService, after CronService.InitSingleton). The "cron-config-transform" seam is RESOLVED: `SiegeConfig.MOLTENUS_SPAWN_SCHEDULE`/`AHSERION_START_SCHEDULE` cron strings are initialized from the Java `@Property defaultValue` via `CronExpressions.GetOrCreate`, so `CronService.Schedule` no longer gets a null CronExpression (see bootstrap comment L322-323). DONE.
- **`Services/DatabaseCleaningService.cs`** = faithful 1:1 of Java `DatabaseCleaningService.java` (delete inactive-account players, delete empty legions, maintain brigade generals, add leave history, optimize FK tables). The "thread-1 seam" is just the faithful `Thread.CurrentThread.ManagedThreadId != 1` guard (1:1 of Java `Thread.currentThread().threadId() != 1`). **Correctly NOT wired at boot** because Java `GameServer.initUtilityServicesAndConfig` calls `deletePlayersOnInactiveAccounts()` ONLY under `if (CleaningConfig.CLEANING_ENABLE)` and `CleaningConfig.CLEANING_ENABLE = false` by default (matches Java default) — so the bootstrap faithfully omits the call. If a future tick wants the call present: gate it `if (CleaningConfig.CLEANING_ENABLE)` in the C# bootstrap's utility-init phase (after `PlayerDAO.SetAllPlayersOffline()`, before ThreadPool/CronService init, GameServer.java:226-227); but with the default-false flag this is a behavioral no-op and the service body is already complete. No bounded all-green code step that changes behavior.

---

## ✅ 8 reworked StaticData holder projections — ALL 8 RETIRED (2026-06-17). 7 in commit 02dc02c30; WorldMapSummary (the last) below.

The 8 reworked `*Table`/`*Summary` projections paralleling faithful DataManager holders. 7 were **dead-islands** (0 live consumers — only StaticData ctor/prop/builder/reader self-references); their faithful holder/template was already LOADED at boot, so the projections were pure parser slop. Retired in the proven per-table shape: strip ctor param + assignment + property + StaticData-ctor-call arg + the interwoven reader branches in the streaming parser + the Builders.cs builder + any now-orphaned Helpers + delete the table file:
- **PetTemplateTable** (PetTemplateSummary/PetFunctionSummary) — faithful PET_DATA/PetData live (pets/pets.xml). Orphaned helper `ReadPetFunctionTypeAttribute` deleted.
- **RecipeTemplateTable** (RecipeTemplateSummary/RecipeComponent*) — faithful RECIPE_DATA/RecipeData live (recipe/recipe_templates.xml). Kept separate WorkOrderRecipeTable.
- **StorageExpansionTemplateTable** (cube+warehouse, StorageExpansionTemplateSummary/Price) — faithful CubeExpandData/WarehouseExpandData live (storage_expander/*.xml).
- **TitleTemplateTable** (TitleTemplateSummary) + dead `DataManager.TITLE_TEMPLATE_TABLE` accessor — faithful TITLE_DATA/TitleData live (player_titles.xml). Kept shared ItemStatModifier + IsStatModifierElement.
- **WalkerTemplateTable** (WalkerTemplateSummary/WalkerRouteStepSummary) — faithful WALKER_DATA/WalkerData live (npc_walker/). KEPT WalkerVersionTable (live via DataManager.WALKER_VERSIONS_DATA → InstanceWalkerFormations/WalkerTemplate) + its walk_parent/version reader; renamed file WalkerTemplateTable.cs → WalkerVersionTable.cs.
- **HousingTemplateTable** (HousingAddress/Building/PartSummary + HousingDecorLine) — faithful HOUSE_DATA + HOUSING_OBJECT_DATA live. Orphaned helpers GetHouseTypeId/GetDefaultBuildingId/IsHousingBuildingPartElement/SplitHousePartTags + HousingBuildingBuilder deleted. Largest (land/building/address/sale/fee/part reader blocks).
- **PlayerBrokerSettlementSummary** — standalone orphan record (0 consumers, not in parser).

−1283 lines. Build 0, golden 196/196 byte-exact, full suite 459/0, bootstrap 9/9.

### ✅ RETIRED (8/8): WorldMapSummary — was a DEAD ISLAND, not a coordinated seam (2026-06-17, 1 commit rrfarmer).
**The earlier "live coordinated runtime-instance seam" framing was STALE.** Grep proved the entire reworked `WorldMapSummary`/`WorldMapRuntimeState*` cluster had ZERO live consumers (production OR test):
- `World/Zone/ZoneInstance.cs` + `Handlers/AdminCommands/Zone.cs` (the alleged consumers) actually run on the FAITHFUL `World.GetInstance().GetWorldMap(_mapId)` → `WorldMap`/`ZoneAttributes` (full worldOptions/HasOverridenOption/IsFlightAllowed/CanGlide/CanRide/... + faithful WorldMapInstance lifecycle via WorldMapInstanceFactory/GeneralInstanceHandler), NOT on the runtime state.
- `GameServerRuntimeContext.WorldMapStates` was SET in SetDataManager but NEVER READ; `StaticData.WorldMaps` had no readers besides that dead set; `FlightZoneTable.CanFly/CanGlide(WorldMapSummary, WorldZoneAttributes)` had no production/test callers; `PlayerZoneStateService` (the claimed wirer) does not exist in code. Same dead-island delete shape as the other 7, just larger.
- **Deleted:** `Dataholders/WorldMapSummary.cs` (struct + the reworked `WorldZoneAttributes [Flags]` enum — faithful side uses `ZoneAttributes`), `World/WorldMapRuntimeState.cs`, `World/WorldMapRuntimeStateTable.cs`, `World/WorldMapInstanceRuntimeState.cs` (+ WorldMapNearbyQuestRefreshSchedulePlan/Status), `World/IInstanceLifecycleHandler.cs` (reworked GeneralInstanceLifecycleHandler — faithful is Instance.Handlers.GeneralInstanceHandler).
- **Stripped/repointed:** `GameServerRuntimeContext` (dropped WorldMapStates/SetWorldMapStates + the SetDataManager assignment + `using World`); `FlightZoneTable` (dropped the 2 dead CanFly/CanGlide methods + ShouldUseWorldMapOption/HasFlag helpers; KEPT the table + Contains/ZonePoint2D/FlightZoneType — a separate flight-zone projection, also currently unread but not part of this island); `StaticData` (dropped `worldMaps` list + ctor param + assignment + `WorldMaps` property + the `<map>` reader branch + ctor-call arg); test `RealStaticDataLoadIntegrationTests` count-assert repointed `sd.WorldMaps.Count` → `sd.WorldMaps2.Size()` (faithful WORLD_MAPS_DATA holder). Build 0, golden 196/196 byte-exact 0-skipped, full 459/0, bootstrap 9/9.

### ✅ RESIDUAL FLIGHT-ZONE SEAM — RETIRED (2026-06-17, 2 commits rrfarmer: 41876a4bf prep + 67baa6250 excision).
Confirmed `StaticData.FlightZones` = 0 readers (production + test). The seam was ENTANGLED with the LIVE `CreaturePvpZoneTable` because `ZonePoint2D` (record struct) was DEFINED in FlightZoneTable.cs but consumed by CreaturePvpZoneSummary.Points, and the StaticData streaming reader shared the depth-3 `points`/depth-4 `point` branches between `currentFlightZone` and `currentCreaturePvpZone` via null-conditional calls.
- **PREP (41876a4bf):** relocated `ZonePoint2D` to new neutral `Dataholders/ZonePoint2D.cs` (same namespace), no behavior change — so CreaturePvpZone keeps compiling once FlightZone is deleted. Safe fallback substep.
- **EXCISION (67baa6250):** deleted `Dataholders/FlightZoneTable.cs` (FlightZoneTable + FlightZoneSummary + FlightZoneType), the `FlightZoneBuilder` (StaticData.Builders.cs), and from StaticData.cs: ctor param + assignment + `FlightZones` property + `flightZones` list + `currentFlightZone` var + the zone-close branch + the `FlightZoneBuilder.TryCreate` call + the `currentFlightZone?.SetVerticalBounds/AddPoint` null-conditional calls in the shared reader + the `new FlightZoneTable(...)` ctor-call arg.
- **CreaturePvpZone byte-identical (verified):** FLY/NO_FLY (FlightZone) and PVP/FORT (CreaturePvpZone) `zone_type` values are mutually exclusive, so dropping the FlightZone `TryCreate` cannot change which zones CreaturePvpZone accepts. The points/point branches now call `currentCreaturePvpZone` directly (was already what ran for PVP/FORT zones). Golden 196/196 byte-exact unchanged; CreaturePvpZone golden green.
- Build 0, full suite 459/0, golden 196/196 byte-exact 0-skipped, bootstrap 9/9.

**With this, ALL StaticData reworked summary-projection dead-islands are retired** (the 8 holder projections + WorldMapSummary + NpcSpawnTable[-family, commit ec289dc00] + now FlightZone). No NpcSpawnTable remains (already gone). Reworked-slop-zero for StaticData projections is effectively reached; remaining slop seams live outside the StaticData projection family.

---

## ✅ GameServerPacket BASE-UNIFICATION — COMPLETE (BATCH 21, 2026-06-17, 1 commit c7067d74d rrfarmer). PACKET-FRONT OBJECT-SPINE BIG-BANG DONE.

**SmPet + SmPetEmote RETIRED; `GameServerPacket` base + `SerializeFrame`/`WritePayload` DROPPED. grep `: GameServerPacket` in `src/Aion.GameServer/Network/Aion/ServerPackets` = 0.** Every GS->client packet now extends faithful `AionServerPacket : BaseServerPacket`; the dual-serialization-path slop debt is fully retired (the live client wire was ALWAYS faithful-only — `GameServerPacket.SerializeFrame` was a C#-test-only invention, never on the wire).

**The deferred "Pet data-model big-bang" turned out trivial — there was NO real Java pet byte oracle.** `PetJavaVectorArtifactReaderTests.cs` is a SCHEMA/GUARD test, not a byte oracle: its only artifact is an inline design-vector JSON with ALL hex fields (`bodyHex`/`canonicalPayloadHex`/`wireFrameHex`) = `null`, and `parity-artifacts/known-list-pet/java/` does not exist on disk (the disk-scan test early-returns "Needs Verification"). The two byte-compare methods iterate `Packets.Where(hex != null)` = EMPTY, so `CreateCSharpPacketFromArtifact` (the SOLE constructor of the reworked SmPet/SmPetEmote, via `SerializeFrame`) was DEAD CODE that never asserted a byte. The long-planned uninitialized-Pet + MoveController + PetCommonData/template/master fixture was NEVER NEEDED — nothing to byte-verify against, and faking bytes is forbidden.

**Migration (faithful, no faked bytes):** the test's only load-bearing coupling was `SmPet.PacketOpCode` (101) / `SmPetEmote.PacketOpCode` (187); replaced with local `const int SmPetOpCode = 101 / SmPetEmoteOpCode = 187` (canonical NCSoft opcodes from `ServerPacketsOpcodes.AddPacketOpcode(101, typeof(SM_PET))` / `(187, typeof(SM_PET_EMOTE))`, identical values), kept the schema/semantics guard, deleted the dead `AssertGenerated*WhenPresent` / `CreateCSharpPacketFromArtifact` / `SerializeUnencryptedBody` / `SerializeCanonicalPayload` / `NormalizeHex` / `Required*` machinery (the last `SerializeFrame` callers). A doc-comment now points future real-oracle work at the `GoldenPacketFixtureTests.CaptureWriteImplPayload` path with an uninitialized-Pet fixture. Deleted `SmPet.cs` (+9 `SmPet*Snapshot` records) + `SmPetEmote.cs` (+`SmPetEmoteSnapshot`) + `GameServerPacket.cs`. 0 production consumers (grep-confirmed). The faithful `SM_PET`/`SM_PET_EMOTE : AionServerPacket` live 1:1 ports unchanged.

**MASKING WARNING HEEDED:** `build-server shutdown` + `rm -rf` GameServer+Tests obj/bin, clean full-solution rebuild = 0 errors GENUINE (heterogeneous, not CS0115-homogeneous; test project also force-rebuilt `--no-incremental` = 0). golden 196/196 byte-exact 0-skipped / full 459/0 / bootstrap 9/9. NOTE: ChatServer/LoginServer `GameServerPacket`/`GsServerPacket`/`SerializeFrame` are SEPARATE base families on their own wires (different namespaces) — out of scope.

**NEXT VEIN:** the 8 reworked holder projections (HousingTemplateTable / PetTemplateTable / RecipeTemplateTable / StorageExpansionTemplateTable / TitleTemplateTable / WalkerTemplateTable / WorldMapSummary / PlayerBrokerSettlementSummary) — separate StaticData-projection cleanup; OR faithful base-unification cleanup of residual reworked layers.

---

## GameServerPacket -> AionServerPacket BASE-UNIFICATION — BATCH 20: SmLegionDominionRank + SmLegionHistory FULLY RETIRED (2026-06-17, 1 commit rrfarmer). 4 -> 2 GameServerPacket survivors.

Both reworked packets were **dead flat-snapshot slop** — confirmed by grep: each was referenced ONLY in its own `.cs` + its dedicated `*Tests.cs` + docs. The faithful `SM_LEGION_DOMINION_RANK.cs` / `SM_LEGION_HISTORY.cs` ALREADY EXIST as 1:1 Java ports and ARE the live production path:
- `SM_LEGION_DOMINION_RANK(LegionDominionLocation, Legion)` — wired in `LegionDominionService.cs:172`, `LegionDominionLocation.cs:142` (UpdateRanking), `CM_LEGION_DOMINION_REQUEST_RANKING.cs:32`.
- `SM_LEGION_HISTORY(List<LegionHistoryEntry>, [page,] Type)` — wired in `LegionService.cs:687` (AddHistory broadcast), `CM_LEGION_HISTORY.cs:35`.

No Java `*_GoldenTest.java` exists for either packet; the reworked `*Tests.cs` (hand-built byte assertions, NOT ported from a Java oracle) were the sole C# byte coverage. MIGRATION (per the BATCH 19 SmFindGroup recipe): rewrote both `*Tests.cs` to drive the LIVE faithful `SM_LEGION_*` packets via the `WriteImpl(null)`-into-LITTLE_ENDIAN-`ByteBuffer` harness + a `HexReader`, asserting the SAME byte layout (now field-decoded, not opcode-const). Deleted `SmLegionDominionRank.cs` + `SmLegionHistory.cs` + the orphaned `Data/LegionDominionParticipantRow.cs` DTO (consumed only by the reworked packet) + the inline `LegionDominionRankEntry`/`LegionHistoryEntryRow` snapshot records (in the deleted packet files).

**Fixture notes (reusable):**
- `SM_LEGION_DOMINION_RANK`: build live `LegionDominionParticipantInfo` via setters (`SetLegionId/SetPoints/SetTime/SetDate`); `SetDate(DateTimeOffset.FromUnixTimeSeconds(epoch))` round-trips exactly through `GetDate()` (ms/1000). Allocate `LegionDominionLocation` + `LegionDominionLocationTemplate` via `RuntimeHelpers.GetUninitializedObject` (the ctor builds zoneName we don't need) + poke `template`/`id`/`participantInfo` (SortedDictionary). Names come from REAL cached `Legion` instances registered into `LegionService.GetInstance()`'s private `legionsById` ConcurrentDictionary via reflection — `GetLegion(int)` returns the cache hit so `GetLegionName()` resolves WITHOUT touching `LegionDAO`/DB. Ranking order (`GetLegionRanking(false)` = `OrderByDescending(Points).ThenBy(Date)`) and the rank>25 last-row replacement match Java byte-for-byte.
- `SM_LEGION_HISTORY`: build live `LegionHistoryEntry(id, epochSeconds, LegionHistoryAction.KICK/CREATE/..., name, description)`; actionId = `action.GetId()`; type ordinal = `(int)LegionHistoryAction.Type.{LEGION=0,REWARD=1,WAREHOUSE=2}`. No reflection needed — all public.

Build0/golden196 byte-exact 0-skipped/full 461->459 (-2 redundant reworked `OpcodeIs*` const assertions; the const no longer exists)/bootstrap9.

**2 GameServerPacket survivors remain: SmPet + SmPetEmote** (DEFER — see below). Once those land the `GameServerPacket` base + `SerializeFrame`/`WritePayload` DROP entirely.

### DEFERRED: SmPet + SmPetEmote — the LAST 2, ONE coupled Pet data-model big-bang (NOT one bounded commit)
Faithful `SM_PET.cs` / `SM_PET_EMOTE.cs` exist and are 1:1 Java ports BUT both ctors take a LIVE `Pet` and read its graph: `SM_PET_EMOTE` reads `pet.GetObjectId/GetX/GetY/GetZ/GetHeading()` + `pet.GetMoveController().GetTargetX2/Y2/Z2()`; `SM_PET` reads the full Pet/PetCommonData/template/master graph. The reworked `SmPet`/`SmPetEmote` (flat `SmPetSpawnSnapshot`/`SmPetEmoteSnapshot` records) are constructed by NOTHING in production — only by **`PetJavaVectorArtifactReaderTests.cs`**, which is the sole byte oracle for BOTH packets (a real Java-vector artifact reader, NOT slop) and builds them via the snapshot ctors + `SerializeFrame`. EXACT SEAM to retire: (1) build an uninitialized `Pet` fixture (`RuntimeHelpers.GetUninitializedObject(typeof(Pet))`) + poke `_objectId`/position/heading + a `MoveController` whose `GetTargetX2/Y2/Z2()` return the artifact's target coords; (2) build the SM_PET-side Pet/PetCommonData/template fixture (heavier — spawn packet writes name/templateId/master/decoration); (3) switch the test harness from `SerializeFrame`/`SerializeUnencryptedBody` to the faithful `WriteImpl(null)` ByteBuffer path; (4) delete the reworked packets + their snapshot records. This touches BOTH packets + the shared artifact reader in one go (can't split — the reader's `CreateCSharpPacketFromArtifact` switch builds both), and needs a Pet+MoveController fixture not yet built → larger than one bounded commit. Defer to a dedicated Pet-fixture increment.

## GameServerPacket -> AionServerPacket BASE-UNIFICATION — BATCH 19: SmGameTime + SmFindGroup FULLY RETIRED (2026-06-17, 2 commits rrfarmer). 6 -> 4 GameServerPacket survivors.

**(1) SmGameTime — the "singleton-vs-DI seam" was a FALSE concern; SAME-SOURCE byte-identical repoint.** Faithful `SM_GAME_TIME()` is parameterless and writes `WriteD(GameTimeService.GetInstance().GetGameTime().GetTime())`; the reworked `SmGameTime(GameMinutes)` wrote `WriteD(GameMinutes)`. The C# `GameTimeService` ctor sets `_instance = this` (Java SingletonHolder parity), so `GetInstance()` returns the SAME live instance that schedules the broadcast — `GetGameTime()` => `new GameTime(GameMinutes)`, `GetTime()` => that exact int. Byte-identical (both opcode 38). Java truth: `GameTimeService.startClock` broadcasts `new SM_GAME_TIME()` (parameterless), confirming the reworked parameterized packet was the slop. Sole production consumer = `GameTimeService.StartClock` save-task; repointed `new SmGameTime(GameMinutes)` -> `new SM_GAME_TIME()`. Migrated the bootstrap test `GameServerBootstrapTests.GameTimeService_LoadsAndPeriodicallyStoresServerVariable` (`SmGameTime`->`SM_GAME_TIME`). Deleted `SmGameTime.cs` (no DI reg).

**(2) SmFindGroup — data-model big-bang was SMALLER than feared; reworked packet had ZERO production consumers (pure dead slop).** The faithful `SM_FIND_GROUP.cs` ALREADY EXISTS as a full 1:1 Java port, wired into the production `FindGroupService.cs` (`new SM_FIND_GROUP(action, recruitments/applications/instanceGroups/player)`) over the LIVE `GroupRecruitment`/`GroupApplication`/`ServerWideGroup`/`Player` model graph. The reworked `SmFindGroup` (flat-snapshot records + static factories) was constructed by NOTHING in production — only by its own dedicated `SmFindGroupTests`, which was the SOLE C# byte oracle (the Java `SM_FIND_GROUP_GoldenTest.java` had never been ported to C#).
- **Migration:** ported `SM_FIND_GROUP_GoldenTest.java` -> rewrote `SmFindGroupTests.cs` as 13 faithful golden cases driving the live model exactly as FindGroupService does, byte-exact vs the SAME Java oracle hex. Deleted `SmFindGroup.cs` + its 8 orphaned `FindGroup*Snapshot` records (same file).
- **Fixture gotchas (mirror Java's Unsafe approach), reusable for the 4 remaining data-model packets:**
  - Build `Player` via `RuntimeHelpers.GetUninitializedObject(typeof(Player))` + reflect-poke `_objectId`/`playerAccountData`/`playerAccount`/`Position` — the REAL Player ctor builds `AbsoluteStatOwner` which touches the un-wired `DataManager.ABSOLUTE_STATS_DATA` singleton (Java sidesteps identically with `Unsafe.allocateInstance`).
  - Poke private `level` on `PlayerCommonData` (`SetLevel` touches `PLAYER_EXPERIENCE_TABLE`) and private `lastUpdate` on the entries (`SetLevel`/timestamp avoidance, exactly like Java's `setField`).
  - The faithful packet's internal `(List<GroupRecruitment>)entries` downcast (Java erased-cast) requires passing the CONCRETE `List<GroupRecruitment>`/`List<ServerWideGroup>`/`List<GroupApplication>` (the covariant `IReadOnlyList<FindGroupEntry>` param binds it), NOT a `List<FindGroupEntry>` (which throws `InvalidCastException`).
  - Serialize via the faithful `WriteImpl(null)`-into-`ByteBuffer` harness (precedent `CaptureWriteImplPayload`), NOT `SerializeFrame` (AionServerPacket has none).
  - Toggle `AdminConfig.NAME_TAGS = []` (for `GetName(true)`) + `NetworkConfig.GAMESERVER_ID = 1` (recruitment case) with try/finally restore.
- Build0/golden196 byte-exact 0-skipped/full 462->461 (one redundant reworked-opcode-constant assertion dropped)/bootstrap9.

**4 GameServerPacket survivors remain** (all DEFER, all reworked data-model duplicates): SmLegionDominionRank/SmLegionHistory/SmPet/SmPetEmote — dedicated *Tests sole byte oracle, no golden InlineData. Each needs the SAME recipe SmFindGroup just proved: port the Java golden test to drive the faithful SM_* over its live model graph (Unsafe-equivalent fixture per the gotchas above), then delete the reworked Sm* + snapshot DTOs + slop test. **Once those 4 land, the `GameServerPacket` base + `SerializeFrame`/`WritePayload` DROP entirely.**

## GameServerPacket -> AionServerPacket BASE-UNIFICATION — BATCH 18: SmAttackStatus MIGRATED off GameServerPacket onto AionServerPacket.WriteImpl (2026-06-17, 1 commit rrfarmer). 7 -> 6 GameServerPacket survivors.

The first FINAL-base-unification step landed. `SmAttackStatus` was the lone faithful-content packet still on the test-only `GameServerPacket` base (it IS the 1:1 port — batch-12 finding, NOT a duplicate). Migrated 1:1 with ZERO byte change:
- Base `: GameServerPacket` -> `: AionServerPacket` (kept the explicit `: base(PacketOpCode)` int ctor — opcode stays pinned, no reliance on the `ServerPacketsOpcodes.GetOpcode(GetType())` registry path).
- `protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)` -> `protected override void WriteImpl(AionConnection con)`; body byte-writes IDENTICAL — only swapped the `buffer.WriteD/WriteC/WriteH(...)` calls for the inherited instance `WriteD/WriteC/WriteH(...)` on `BaseServerPacket` (same `buf.PutInt/Put/PutShort` underneath). Switched the `using Aion.Commons.Network;` (PacketBuffer/GameCrypt) for `using Aion.GameServer.Network.Aion;` (AionConnection), mirroring template `SM_FLY_TIME.cs`.
- **Golden case migrated theory1 -> theory2:** moved the `SM_ATTACK_STATUS.json` InlineData out of `CsharpPayloadMatchesJavaGoldenFixture` (SerializeUnencryptedPayload / `SerializeFrame(crypt)[7..]`) into `FaithfulCsharpPayloadMatchesJavaGoldenFixture` (`CaptureWriteImplPayload` — LITTLE_ENDIAN ByteBuffer, reflective WriteImpl, no opcode/crypt). `ReconstructAttackStatus` (return type now an `AionServerPacket`) moved from the `Reconstruct` switch to the `ReconstructFaithful` switch. theory1 (`CsharpPayloadMatchesJavaGoldenFixture` + the `Reconstruct` switch + `SerializeUnencryptedPayload` helper) is now **fully empty and DELETED** — no faithful-content packet uses the SerializeFrame golden path anymore.
- **MASKING-TRAP cleared:** `dotnet build-server shutdown` + `rm -rf` GameServer/Tests obj+bin, clean rebuild = **0 errors (genuine, not CS0115-homogeneous)**. golden 196/196 byte-exact 0-skipped / full 462/0 / no regressions.

**6 GameServerPacket survivors remain** (all DEFER, cheap veins EXHAUSTED): SmFindGroup/SmLegionDominionRank/SmLegionHistory/SmPet/SmPetEmote (data-model big-bang, dedicated *Tests sole byte oracle, no golden InlineData), SmGameTime (singleton-vs-DI). NONE is a faithful-content packet anymore — every one is a reworked duplicate whose retirement is gated on a subsystem port; once those 6 are retired the `GameServerPacket` base + `SerializeFrame`/`WritePayload` can be DROPPED entirely (no faithful consumers left). **The GameServerPacket drop is now one un-gated step closer: it is blocked ONLY by those 6 reworked-duplicate retirements, not by any faithful packet.**

## Sm* DUPLICATE-PACKET RETIREMENT — BATCH 17: ABYSS-POINTS BIG-BANG — SmAbyssRank + SmLegionEdit FULLY RETIRED (2026-06-17, 1 commit 85558d4ac rrfarmer). 9 -> 7 remaining.

**Turned out NOT to be a hard big-bang — the faithful services ALREADY EXISTED.** `Services/Abyss/AbyssPointsService.cs` + `Services/Abyss/GloryPointsService.cs` are exact 1:1 with Java `services/abyss/AbyssPointsService.java`/`GloryPointsService.java`: live `player.GetAbyssRank().AddAp(amount)` / `AddGp(amount,addToStats)` mutation + direct `new SM_ABYSS_RANK(player[,pos])` / `new SM_ABYSS_RANK_UPDATE(0,player)` / `new SM_LEGION_EDIT(0x03, player.GetLegion())` / `SM_SYSTEM_MESSAGE.STR_*` sends (incl. >30000 BIG-AP warn, SiegeService.OnAbyssPointsAdded, isLegionMember contribution, AbyssRankingCache rank-position). The slop was a PARALLEL reworked pair in namespace `Aion.GameServer.Services` (vs faithful `Aion.GameServer.Services.Abyss`).

**Deleted (8 files, 927 lines):** `Services/AbyssPointsService.cs` + `Services/GloryPointsService.cs` (returned `AbyssPointsAddPlan`/`GloryPointsAddPlan` DTOs — no Java counterpart), `Model/GameObjects/PlayerAbyssRank.cs` (record carrying AddAp/AddGp rank-math + AbyssRanks[18] table + GetRankL10n, fed via `FromAbyssRank` snapshot), `Network/Aion/ServerPackets/SmAbyssRank.cs` + `SmLegionEdit.cs` (by-value dup packets), `Data/AbyssRankRepository.cs` (reworked DI DAO abstraction; faithful static `Dao/AbyssRankDAO.AddGp` is the real 1:1 Java port for offline GP), tests `SmLegionEditTests.cs` + `AbyssRankRepositoryTests.cs` (tests-of-slop).

**WHY pure deletion sufficed — build 0 on FIRST try, ZERO repoints:** every bare-name caller (`TradeService`/`QuestService`/`PvpService`/`ItemPurificationService`/`ItemChargeService`/`ApExtractAction`/`PlayerTeamDistributionService`/`AbyssRankUpdateService`) calls `AbyssPointsService.AddAp(player,amount)` / `(player,obj,amount)` or `GloryPointsService.AddGp(objId,amount)` — signatures present on BOTH services. C# same-namespace-wins meant they bound to the reworked service while it existed (and its returned plan was IGNORED at the call sites -> the reworked path was actually SENDING NO PACKETS, a latent runtime fidelity bug). Deleting the reworked pair rebinds every caller via the `using Aion.GameServer.Services.Abyss;` import to the faithful service, which DOES send. The reworked plan-DTOs + PlayerAbyssRank + SmAbyssRank had ZERO consumers outside the two reworked files (grep-confirmed). The GP callers already used FQ `Aion.GameServer.Services.Abyss.GloryPointsService`.

**Real coverage preserved:** all 3 packets are golden-covered against the Java oracle on the FAITHFUL types — SM_ABYSS_RANK / SM_ABYSS_RANK_UPDATE in `GoldenPlayerInfoFixtureTests` (`SM_ABYSS_RANK.json` / `SM_ABYSS_RANK_UPDATE.json`, pinned-AbyssRank scalars), SM_LEGION_EDIT in `GoldenPacketFixtureTests` (`SM_LEGION_EDIT.json`). The deleted `SmLegionEditTests` (8 facts) only re-asserted the reworked static-factory byte shapes; `AbyssRankRepositoryTests` (~5 facts) tested the orphaned repo SQL-plan covered by faithful AbyssRankDAO. Build0/golden196 byte-exact 0-skipped/full475->462 (-13 slop tests)/bootstrap9.

**REMAINING 7 (all DEFER, cheap veins EXHAUSTED):** SmFindGroup/SmLegionDominionRank/SmLegionHistory/SmPet/SmPetEmote (data-model big-bang, dedicated *Tests sole byte oracle, no golden InlineData), SmGameTime (singleton-vs-DI), SmAttackStatus (NOT a duplicate — IS the faithful port; base-unification must move it onto AionServerPacket.WriteImpl). Each needs its own dedicated increment.

## Sm* DUPLICATE-PACKET RETIREMENT — BATCH 16: SmKey + SmPong FULLY RETIRED via a faithful GameCrypt Write-seam harness (2026-06-17, 1 commit rrfarmer). 11 -> 9 remaining.

Built the long-deferred uninitialized-`AionConnection` crypt harness — the "single highest-leverage base-unification unblock" from batch 15. `GameCryptTests` now exercises the FAITHFUL `SM_KEY` / `SM_PONG` (extend `AionServerPacket`) through their real `AionServerPacket.Write(con, buffer)` framing + `con.Encrypt` path, NOT the reworked `GameServerPacket.SerializeFrame(GameCrypt)` shortcut. Both reworked `SmKey.cs` / `SmPong.cs` DELETED (grep-confirmed 0-ref outside the test's own local method/var names; no DI regs).

**THE SEAM (precedent: the Golden* fixture harnesses' `RuntimeHelpers.GetUninitializedObject(typeof(AionConnection))`):**
- `NewConnectionWithFreshCrypt()` allocates an uninitialized `AionConnection` (no socket / Dispatcher / PacketProcessor) and reflectively pins ONLY its private readonly `crypt` field to a fresh faithful `Crypt` — the only field `Write` + `Encrypt` touch (`AionConnection.Encrypt(buf)` -> `crypt.Encrypt(buf)`; `EnableCryptKey()` -> `crypt.EnableKey()`).
- `WriteFaithful(packet, con)` allocates a LITTLE_ENDIAN `ByteBuffer.Allocate(MAX_CLIENT_SUPPORTED_PACKET_SIZE)`, calls `packet.Write(con, buffer)` (faithful framing: `putShort(0)` placeholder + `writeOP()` + `writeImpl` + flip + `putShort(limit)` + `slice()@pos2` + `con.Encrypt(slice)`), then copies `[arrayOffset, arrayOffset+limit)` of the backing array as the framed bytes.
- **Determinism gap closed faithfully (NEVER faked bytes):** faithful `Crypt.EnableKey()` seeds its `EncryptionKeyPair` from `Rnd.NextInt()` (non-deterministic), unlike the reworked `GameCrypt(Func<int>)`. So each SM_KEY/SM_PONG case lets the real flow run, then CAPTURES the actual baseKey via `Crypt.packetKey.GetBaseKey()` (reflection) and INDEPENDENTLY recomputes the expected enciphered key (`(key ^ 0xCD92E4DF) + 0x3FF2CCCF`) and expected encrypted frame from that SAME baseKey using verbatim copies of Java `EncryptionKeyPair`'s server/client keystream (the retained `ServerPayloadEncryptor`/`ClientPayloadEncryptor` test mirrors). Crypt output is asserted byte-exact against an independent recomputation keyed on the crypt's own baseKey — faithful to Java `network/Crypt` + `network/EncryptionKeyPair`.
- The two client-decrypt tests build a faithful `Crypt` via `NewCryptWithBaseKey(0x01020304)` (reflectively install `EncryptionKeyPair(baseKey)` into `packetKey` + set `isEnabled=true`, mirroring `EnableKey()`'s post-state deterministically) and drive `crypt.Decrypt(ByteBuffer)`.

Byte-exactness verified: faithful `Crypt`/`EncryptionKeyPair` are bit-identical to the reworked `GameCrypt`/`GameEncryptionKeyPair` (same baseKey->key[8] expansion, same `nKO/Wct...` staticKey, same XOR-chain + 64-bit key-advance, same `EncodeServerPacketOpcode`/enciphered-key formula). SM_KEY (first packet) is sent UNencrypted (`Crypt.Encrypt` flips `isEnabled` and returns); SM_PONG (second) is encrypted over the slice-at-pos-2 (== reworked `AsSpan(2)`, the 2-byte length prefix stays clear). 4 GameCrypt tests green. Build0/golden196 byte-exact/full475/bootstrap9.

**REMAINING 9 (all DEFER — no bounded one-commit production repoint, abyss/data-model/singleton big-bangs off-limits):** SmAbyssRank + SmLegionEdit (abyss points-service big-bang), SmFindGroup/SmLegionDominionRank/SmLegionHistory/SmPet/SmPetEmote (data-model big-bang, dedicated *Tests are sole byte oracle), SmGameTime (singleton-vs-DI), SmAttackStatus (NOT a duplicate — IS the faithful port; base-unification just needs to move it onto AionServerPacket.WriteImpl). **The crypt-harness seam bucket is now CLOSED.**

## Sm* DUPLICATE-PACKET RETIREMENT — BATCH 15: SmAutoGroup FULLY RETIRED (2026-06-17, 1 commit ecaeb6889 rrfarmer). 12 -> 11 remaining.

Un-reworked the 3 `new SmAutoGroup(AutoGroupSummary,...)` call sites in `Services/PeriodicInstanceRegistrationService.cs` to the faithful `SM_AUTO_GROUP(int maskId,...)` ctors (twin at opcode 122, `WriteImpl` byte-identical to the deleted `WritePayload`). The faithful ctor re-derives mapId/messageId/titleId from `AutoGroupTypeExtensions.GetAGTByMaskId(maskId)` -> `agt.GetTemplate()` -> `DataManager.AUTO_GROUP` (the faithful `AutoGroupData` holder, which IS loaded at boot via `StaticData.TryLoadHolder` line 375). **Byte-verified IDENTICAL:** faithful `AutoGroup.GetL10nId()` returns `nameId`, `GetTitleId()` returns `titleId`, `GetInstanceMapId()` returns `instanceId` — the exact same XML attributes (`name_id`/`title_id`/`instanceId`) the reworked `AutoGroupSummary.NameId`/`TitleId`/`InstanceMapId` carried. So `SM_AUTO_GROUP(maskId)` emits the same bytes as `SmAutoGroup(summary)`.
- Repoints: L207 `new SmAutoGroup(autoGroup, EntryIconWindowId, false)` -> `new SM_AUTO_GROUP(maskId, SM_AUTO_GROUP.WND_ENTRY_ICON, false)`; L229 `new SmAutoGroup(autoGroup)` -> `new SM_AUTO_GROUP(maskId)`; L262 `new SmAutoGroup(autoGroup, EntryIconWindowId, isClosed)` -> `new SM_AUTO_GROUP(maskId, SM_AUTO_GROUP.WND_ENTRY_ICON, isClosed)` (`maskId` already in scope at every site). Return types `IReadOnlyList<SmAutoGroup>`/`SmAutoGroup?` -> `SM_AUTO_GROUP` (faithful IS-A `AionServerPacket`, broadcast `List<AionServerPacket>` path unchanged).
- **`AutoGroupSummary` is NOT deleted** — it is the live `AutoGroupTable` dataholder DTO (StaticData parses `auto_group` XML into it; AutoGroupTable indexes it for `GetTemplateByInstanceMaskId`/level-range/portal-cooldown lookups, which the service still uses for filtering). Only the *packet construction* moved off it.
- Deleted `SmAutoGroup.cs` (grep-confirmed self-refs only; not DI-registered; no slop test). Build0/golden196 byte-exact/full475/bootstrap9.

**NEXT SEAM ASSESSED -> all remaining 11 DEFER (no bounded one-commit production repoint left, abyss big-bang off-limits this tick).** SmFindGroup + SmLegionDominionRank re-checked: both genuinely **test-only** (the `ConquerorAndProtectorService` "LegionDominionRank" hit is an unrelated field/type, not the `SmLegionDominionRank` packet). Their dedicated `*Tests` are the ONLY byte oracle (no golden Java-oracle InlineData for opcodes), and the faithful twins take a live object graph (`FindGroupEntry`/`Player`; `LegionDominionLocation`+`Legion`) vs the test's flat snapshot records — migrating = data-model big-bang. The only remaining *production* repoints (SmAbyssRank/SmLegionEdit) are the abyss points-service big-bang, explicitly off-limits this tick.

### GameServerPacket -> AionServerPacket base-unification FINAL STEP — exact scope of the 7 survivors
All 7 remaining `Sm*` extend the test-only `GameServerPacket` base (gives `WritePayload(PacketBuffer,GameCrypt)` + `SerializeFrame`/`SerializeUnencryptedPayload`); the faithful family extends `AionServerPacket` (`WriteImpl(AionConnection)` + `Write(AionConnection)`/`Encrypt`). Buckets (abyss + crypt buckets now CLOSED — batches 17 + 16):
- **Data-model big-bang, dedicated-test-is-sole-byte-oracle (5):** `SmFindGroup`, `SmLegionDominionRank`, `SmLegionHistory`, `SmPet`, `SmPetEmote`. Faithful twins need a live graph the flat-snapshot tests don't build; deleting the tests would orphan the only byte coverage (no golden Java-oracle InlineData for these opcodes).
- **Singleton-vs-DI seam (1):** `SmGameTime` — faithful `SM_GAME_TIME()` is parameterless, reads `GameTimeService.GetInstance()` singleton; reworked is DI-fed. Verify the singleton == the DI instance source before repointing, else defer.
- **NOT a duplicate (1):** `SmAttackStatus.cs` IS the faithful 1:1 port (batch-12 finding); it just still extends `GameServerPacket`. The base-unification's job here is to move IT (and the other golden-theory1 faithful-content packets) onto `AionServerPacket.WriteImpl`, NOT to retire it.

**RECOMMENDED NEXT:** all cheap dead-island / repoint veins are now EXHAUSTED. The 5 data-model packets wait on their subsystem ports (FindGroup/LegionDominion/LegionHistory/Pet live-graph models); SmGameTime needs the GameTimeService singleton-vs-DI source reconciled first; SmAttackStatus needs the WriteImpl-migration (move the faithful-content theory1 packets onto AionServerPacket, then drop GameServerPacket/SerializeFrame entirely) — the true FINAL base-unification step. None is a one-commit repoint; each is its own coordinated increment.

## Sm* DUPLICATE-PACKET RETIREMENT — BATCH 14: SmSystemMessage FULLY RETIRED (the deferred last file) (2026-06-17, 1 commit 5c27bc8ef rrfarmer). 13 -> 12 remaining.

Closed the single seam left from batch 13. The last consumer `Services/PeriodicInstanceRegistrationService.cs` was un-reworked FAITHFULLY to the live `SM_SYSTEM_MESSAGE` (no reworked `.MessageId` accessor — Java has no such getter):
- `CreateOpeningMessageForMaskId(int) : SM_SYSTEM_MESSAGE?` now returns the faithful catalog factories `SM_SYSTEM_MESSAGE.STR_MSG_INSTANCE_OPEN_IDAB1_DREADGION()` / `_IDDREADGION_02/03()` / `_IDKamar()` / `_IDLDF5_Under_01_War()` / `_IDF5_TD_war()` / `_IDLDF5_Fortress_Re()` for mask ids 1/2/3/107/108/109/111 — byte-verified msgIds 1400252/1400628/1401398/1401730/1401947/1402032/1402192 IDENTICAL to the deleted reworked friendly factories. Matches Java `PeriodicInstanceManager` ctor which passes exactly these `SM_SYSTEM_MESSAGE.STR_MSG_INSTANCE_OPEN_*()` objects to `scheduleRegistration`.
- All 4 `SmSystemMessage?` param/field types -> `SM_SYSTEM_MESSAGE?` (faithful IS-A `AionServerPacket`, so the existing `List<AionServerPacket>` broadcast path is unchanged).
- The reworked record `PeriodicInstanceRegistrationScheduleEntry.OpeningMessageId` now derives from `openingMessage.GetId()` (faithful packet's `GetId()` returns `msgId`, == the deleted `.MessageId`).
Then DELETED `SmSystemMessage.cs` (grep-confirmed 0 remaining C# refs; not DI-registered; no slop test). Build0/golden196 byte-exact/full475/bootstrap9. **The opcode-25 heavy web is fully CLOSED.**

**SmAbyssRank RE-ASSESSED THIS TICK -> still DEFER (NOT bounded to one commit).** Reworked `SmAbyssRank(PlayerAbyssRank)` is fed a `PlayerAbyssRank` record — a parallel reworked abyss-rank MODEL (carries `AddAp`/`AddGp` rank-math + the `AbyssRanks` table + `GetRankL10n`, NO single Java counterpart class), not a mere packet snapshot. Faithful `SM_ABYSS_RANK(Player)` reads the LIVE `player.GetAbyssRank()` (faithful `AbyssRank`) + `AbyssRankingCache.GetInstance().GetRankingListPosition(player)`. Consumers `AbyssPointsService`/`GloryPointsService` mutate via the immutable `PlayerAbyssRank` record + reward-plan DTOs (AbyssPointsService L40 `PlayerAbyssRank.FromAbyssRank(rank)` -> L51 `new SmAbyssRank(updatedRank)`), so retiring `SmAbyssRank` requires migrating those whole services off the `PlayerAbyssRank` record onto direct `AbyssRank.addAp/addGp` LIVE mutation (the Java way) = the **abyss points-service big-bang**, not a packet repoint. EXACT blocker: the `PlayerAbyssRank` projection-vs-live-`AbyssRank` seam spans 3 prod files (`AbyssPointsService`, `GloryPointsService`, `PlayerAbyssRank`) + reward-plan DTOs — un-bounded for one all-green commit. Left `SmAbyssRank.cs` alive.

## Sm* DUPLICATE-PACKET RETIREMENT — BATCH 13: retired the 3 remaining heavy webs SmDialogWindow + SmItemUsageAnimation (FULLY) + SmSystemMessage (all but 1 file) (2026-06-17, 4 commits rrfarmer). 15 -> 13 remaining.

All 3 had a SEPARATE faithful `SM_*` twin registered at the same opcode (60 / 183 / 25) — CLASSIFIED DUPLICATE (not faithful-content).

**SmDialogWindow (opcode 60) — DUPLICATE, FULLY RETIRED, 109 sites repointed.** Faithful `SM_DIALOG_WINDOW.cs` is byte-EXACT 1:1 Java: writes player mailbox-state on the MAIL page / town-id on TOWN_CHALLENGE_TASK / else 0. The reworked Sm* always wrote a flat `WriteH(_dialogContextId)` (default 0) — a latent simplification. ALL 109 consumer sites pass dialogContextId=0 (2-arg `(objId,pageId)` or 3-arg `(objId,pageId,questId)`), and no site used the `.MailPageId`/`.LegionWarehousePageId`/`.NoRightPageId` consts, so the pure token rename `SmDialogWindow`->`SM_DIALOG_WINDOW` is faithful and CORRECTS the latent gap. No DI/test. Deleted def.

**SmItemUsageAnimation (opcode 183) — DUPLICATE, FULLY RETIRED, ~107 sites repointed + golden migrated.** Faithful `SM_ITEM_USAGE_ANIMATION.cs` byte-EXACT 1:1 Java (all 5 ctor overloads identical, incl. the `time>0` World.GetPlayer/SetUsingItem branch). Token-renamed consumers; MIGRATED the golden case from theory1 (`CsharpPayloadMatchesJavaGoldenFixture` / GameServerPacket / SerializeFrame) to theory2 (`FaithfulCsharpPayloadMatchesJavaGoldenFixture` / AionServerPacket / CaptureWriteImplPayload): retyped `ReconstructItemUsageAnimation` return GameServerPacket->SM_ITEM_USAGE_ANIMATION, moved the `[InlineData("SM_ITEM_USAGE_ANIMATION.json")]` + the switch case (Reconstruct->ReconstructFaithful). Generator uses time==0 paths only, so the World.GetPlayer branch never fires in tests. Deleted def.

**SmSystemMessage (opcode 25, the 3635-line web) — DUPLICATE, all-but-1-file retired.** Faithful `SM_SYSTEM_MESSAGE.cs` WriteImpl is byte-IDENTICAL to the Sm* WritePayload: `WriteC(chatType)` (faithful `ChatType.GOLDEN_YELLOW.GetId()`==25 == Sm* hardcoded `GoldenYellowChatType=25`), `WriteC(0)`, `WriteD(senderObjId=0)`, `WriteD(msgId)`, `WriteC(paramCount)`+`WriteS` each, `WriteC(specialCount)`+`WriteS` each. 284 refs / 67 consumer files. Done in 2 commits:
- **3a (55 STR_-only files):** 199 `SmSystemMessage.STR_*` calls; all 168 distinct STR_ names used exist on faithful with matching signatures (the 4 apparent signature "divergences" were head-grab artifacts on multi-overload methods — both overloads present on faithful). Pure token rename.
- **3b (11 friendly-helper files):** mapped 68 distinct friendly names (`InventoryCantExtendMore`, `CompoundSuccess`, `GetPollRewardItemMulti`, `InstanceOpenIdKamar`, `MsgLdf4AdvanceKillerV*`, etc.) 1:1 to their documented `STR_*` via the `// Java parity: SM_SYSTEM_MESSAGE.STR_X` comment in each friendly def. Byte-verified each friendly's msgId + positional args == faithful STR_'s (same order; the long-vs-int arg-type cases UseAbyssPoint/CombatMyAbyssPointGain/GloryPoint* are int-range-safe and `.ToString()` culture matches the established faithful baseline).

**1 file DEFERRED: `Services/PeriodicInstanceRegistrationService.cs`** — reworked C# service (record `PeriodicInstanceRegistrationScheduleEntry` with `OpeningMessageId`, no Java counterpart) reads `openingMessage.MessageId`, a public accessor SmSystemMessage exposes but the faithful SM_SYSTEM_MESSAGE does NOT (Java has no such getter). The faithful fix is to rewrite this reworked service to thread message-IDs instead of packet objects (a slop-service remediation, not a packet repoint). Reverted this 1 file; **SmSystemMessage.cs stays alive with EXACTLY 1 consumer.** NEXT BOUNDED STEP: un-rework PeriodicInstanceRegistrationService to carry the int message-id (have `CreateOpeningMessageForMaskId` return the id, or build the SM_SYSTEM_MESSAGE at send time) — then the last consumer is gone and `SmSystemMessage.cs` can be deleted.

Build0/golden196 byte-exact/full475/bootstrap9 after EACH of the 4 commits. **Heavy webs are now CLOSED except the single PeriodicInstanceRegistrationService seam.**

## Sm* DUPLICATE-PACKET RETIREMENT — BATCH 12: SmAttackStatus "heavy web" was a FALSE ALARM; retired only SmAttackStatusEnums.cs + a dead ctor (2026-06-17, 1 commit 0b7a8ff52 rrfarmer).

**KEY FINDING: there is NO separate faithful `SM_ATTACK_STATUS.cs` — `SmAttackStatus.cs` IS the faithful 1:1 port.** Its nested `TYPE` class-enum + `LOG` enum are byte-exact to `SM_ATTACK_STATUS.java`'s nested enums; its 3 `(Creature, TYPE, skillId, value[, LOG])` ctors + `WritePayload` switch are 1:1 with Java `writeImpl`; it is exactly the packet the golden case `SM_ATTACK_STATUS.json` validates against the Java oracle (`ReconstructAttackStatus`). The "~51 SmAttackStatus sites" ALL consume the faithful nested `SmAttackStatus.TYPE`/`.LOG` (commonly via `using TYPE=...SmAttackStatus.TYPE; using LOG=...SmAttackStatus.LOG; using SM_ATTACK_STATUS=...SmAttackStatus`) — already faithful, NOT churned.

The ONLY slop: (1) `SmAttackStatusEnums.cs` — standalone PascalCase duplicate enums `SmAttackStatusType`/`SmAttackStatusLog` (a subset of nested TYPE/LOG; Java integer values verbatim), consumed ONLY by (2) the reworked `_rawObjectIdMode` ctor `SmAttackStatus(int creatureObjectId, SmAttackStatusType, ..., int hpOrMpPercentage, SmAttackStatusLog, bool? usesNegativeValue)` + its `UsesNegativeValue` helper + 7 `_raw*` fields — a reworked-WorldNpc objectId-snapshot path with ZERO consumers (grep src+tests: 0 `new SmAttackStatus(int,SmAttackStatusType,...)`, 0 refs to `SmAttackStatusType`/`SmAttackStatusLog`/`_rawObjectIdMode` outside the 2 files; orphaned by the WorldNpc spawn-cluster retirement). FIX: deleted `SmAttackStatusEnums.cs`, removed the dead ctor + helper + `_raw*` fields, leaving `SmAttackStatus.cs` == Java. No DI reg, no slop test to delete; the golden case stays on the faithful Creature ctor unchanged. Build0/golden196 byte-exact/full475/bootstrap9.

NOTE: `SmAttackStatus` still extends the test-only `GameServerPacket` base (like the other golden-theory1 faithful-content packets); full base-class unification onto `AionServerPacket.WriteImpl` is the separate later step in this same plan, not part of this batch.

## Sm* DUPLICATE-PACKET RETIREMENT — BATCH 11: first heavy web SmEmotion retired (2026-06-17, 1 commit rrfarmer). 17 -> 16 remaining.

**SmEmotion** (~11 prod sites, NO test/golden/DI consumers — reworked SmEmotion was wire-DEAD; only faithful SM_EMOTION registered at opcode 37) — RETIRED via straight production repoint. Byte-verified: `SM_EMOTION.WriteImpl` == `SmEmotion.WritePayload` IDENTICAL switch (all branches: DIE/loot/CHAIR/FLYTELEPORT/WINDSTREAM/RIDE/RESURRECT/EMOTE/CHANGE_SPEED/default); `(int)_emotionType` == `emotionType.GetTypeId()` (== `(int)type`). Consumers used only 2 of the reworked 6 ctor overloads, both 1:1 with the faithful twin and matching Java exactly: `(Creature,EmotionType)` and `(Creature,EmotionType,int emotion,int targetObjectId)` (Player IS-A Creature -> RideAction/Equipment/FlyController Player-calls bind the Creature ctor, same as Java `new SM_EMOTION(player, EmotionType, 0, 0/npcId)`). The 4 unused reworked overloads (5-arg int-snapshot, Player+speed-default, Player+coords+speed) were slop with no consumer — dropped with the file. 9 repoint sites: SummonsService, RideAction(x2), Equipment, FlyController(x3 STOP_GLIDE/LAND/FLY), AethericFieldBlaststoneAI, EternalBastionAssaulterNpcAI(x2), quest handlers (pandaemonium/sanctum/morheim x2/eltnen). Deleted SmEmotion.cs (no DI/test). Build0/golden196 byte-exact/full475/bootstrap9.

**REMAINING (after batch 12):** SmAbyssRank/SmAutoGroup/SmLegionEdit/SmFindGroup/SmLegionDominionRank/SmLegionHistory/SmPet/SmPetEmote/SmGameTime/SmKey/SmPong (deferred-with-reason below) + 3 remaining heavy webs SmDialogWindow/SmSystemMessage/SmItemUsageAnimation (SmAttackStatus+Enums retired batch 12, SmEmotion batch 11).

## Sm* DUPLICATE-PACKET RETIREMENT — BATCH 10: 6 more survivors retired (2026-06-17, 2 commits rrfarmer). 23 -> 17 remaining.

Production faithful repoints (byte-verified WriteImpl==WritePayload, opcodes confirmed in ServerPacketsOpcodes.cs):
- **SmAbyssRankUpdate** (1 consumer, AbyssPointsService) -> `new SM_ABYSS_RANK_UPDATE(0, player)` (player is a faithful Player; action-0 path writes `player.GetAbyssRank().GetRank().GetId()` == the precomputed Sm* value). Also retyped the orphan plan-DTO field `RankUpdatePacket` to `SM_ABYSS_RANK_UPDATE?` (never read).
- **SmIconInfo** (2, Legion.cs) -> `SM_ICON_INFO(int,bool)` — identical ctor + WriteImpl.
- **SmShowBrand** (2, TemporaryPlayerTeam.cs) -> `SM_SHOW_BRAND(int,int)` + `SM_SHOW_BRAND(IDictionary<int,int>)` (ConcurrentDictionary boxes to IDictionary). Identical.
- **SmTitleInfo** (5, NpcFactions.cs + TitleList.cs) -> `SM_TITLE_INFO` — used ctors `(int)`/`(Player,int)`/`(bool)`/`(Player,bool)`/`(int,int)` all present + byte-identical; the action-0 `IReadOnlyList<PlayerTitle>` ctor was unused.
- **SmMotion** (5, MotionList.cs + AnimationAddAction.cs) -> `SM_MOTION` — the consuming MotionList is the FAITHFUL one using faithful `Motion`/`Dictionary<int,Motion>`/`GetActiveMotions():IDictionary<int,Motion>`, so the action-2/5/6/7 ctors line up exactly (PlayerMotion proxy was only on the Sm* side).
- **SmQuestionWindow** (6, Equipment.Part4/NpcFactions/CubeExpandService + QuestionResponseRegistryTests) -> `SM_QUESTION_WINDOW`. Faithful ctor is `params object[]` (vs Sm* `params string[]`); consumers pass strings (l10n / ToString()) which box cleanly and WriteImpl calls `.ToString()` -> byte-identical. Reworked PascalCase alias consts `WarehouseExpandWarning`/`UnionInviteMe`/`BuddyListAddBuddyRequest` repointed to the FAITHFUL Java names `STR_WAREHOUSE_EXPAND_WARNING`/`STR_MSGBOX_UNION_INVITE_ME`/`STR_BUDDYLIST_ADD_BUDDY_REQUEST` (verified against Java SM_QUESTION_WINDOW.java). The registry test uses these only as opaque question-id ints (behavior test, not a byte test) — clean repoint, no orphan.

Build0/golden196 byte-exact/full475/bootstrap9 each batch. No slop tests deleted this batch (all deletions were production-consumer packets).

**REMAINING 17 — DEFERRED with exact reason (data-model seam or no-twin / heavy webs):**
- **SmAbyssRank** (2 prod: AbyssPointsService/GloryPointsService) — DEFER. Consumers pass a reworked `PlayerAbyssRank.FromAbyssRank(rank)` PROJECTION record; faithful `SM_ABYSS_RANK(Player)` reads `player.GetAbyssRank()` directly. Crosses the reworked-projection-vs-faithful-AbyssRank data-model seam (would need the AbyssPointsService plan-service un-reworked). Not a clean repoint.
- **SmAutoGroup** (3 calls in PeriodicInstanceRegistrationService) — DEFER. Consumer passes a reworked `AutoGroupSummary`; faithful `SM_AUTO_GROUP(int maskId,...)` re-derives mapId/messageId/titleId from `AutoGroupTypeExtensions.GetAGTByMaskId(maskId)` (the faithful AutoGroupType static table). Reworked-Summary-vs-faithful-table seam.
- **SmLegionEdit** (1 prod static-factory AbyssPointsService.Contribution + dedicated test) — DEFER. Faithful `SM_LEGION_EDIT` has NO by-value contribution ctor; type 0x03 reads `legion.GetContributionPoints()` from a live `Legion`. The reworked `.Contribution(long)` precomputes the value. Value-vs-Legion seam.
- **SmFindGroup / SmLegionDominionRank / SmLegionHistory** (test-only, dedicated *Tests files) — DEFER. Each uses flat snapshot records (`FindGroup*Snapshot` / `(int,legionId,participants)` / `LegionHistoryEntryRow`) while the faithful twins take live graph (`FindGroupEntry`/`Player`, `LegionDominionLocation`+`Legion`, `List<LegionHistoryEntry>`+enum Type). NO golden Java-oracle InlineData covers these opcodes, so the dedicated tests are the ONLY byte-coverage — deleting would orphan coverage; migrating needs building the live graph (data-model big-bang).
- **SmPet / SmPetEmote** (test-only, PetJavaVectorArtifactReaderTests) — DEFER. Faithful `SM_PET` spawn ctors take a live `Pet`/`PetCommonData` and `SM_PET_EMOTE` takes a live `Pet`; the artifact test reconstructs from flat decoded fields (`SmPetSpawnSnapshot`/`SmPetEmoteSnapshot`) with no live Pet graph. The test is a REAL Java-captured-vector byte oracle (BodyHex/CanonicalPayloadHex), not slop — cannot orphan; needs the live-Pet graph.
- **SmGameTime** — DEFER (singleton-vs-DI seam; faithful SM_GAME_TIME parameterless reads GameTimeService.GetInstance() singleton, reworked is DI-fed).
- **SmKey / SmPong** — DEFER (GameCryptTests crypt-harness change; faithful SM_KEY/SM_PONG have no SerializeFrame, only Write(AionConnection)+Encrypt needing a live/uninitialized AionConnection).
- **HEAVY WEBS (LAST):** SmDialogWindow / SmSystemMessage / SmItemUsageAnimation. (SmAttackStatus+SmAttackStatusEnums RETIRED batch 12 — was a false alarm, SmAttackStatus.cs is itself the faithful 1:1 port; SmEmotion RETIRED batch 11.)

## Sm* DUPLICATE-PACKET RETIREMENT — BATCH 8-9: 14 more survivors retired (2026-06-17, 2 commits rrfarmer). 37 -> 23 remaining.

Batch8 (9): repoint+delete the 9 lowest-consumer Sm* to faithful SM_* twins, byte-verified identical:
SmQuitResponse SmDeleteWarehouseItem SmBlockResponse SmFriendResponse SmCloseQuestionWindow SmStatUpdateDp
SmFriendNotify SmBindPointTeleport SmSkillCancel. Production repoints to Java forms: SocialService (SM_BLOCK_RESPONSE
const codes / SM_FRIEND_RESPONSE.TARGET_ADDED|TARGET_REMOVED static factories / SM_FRIEND_NOTIFY.DELETED); FriendList
(SM_FRIEND_NOTIFY.LOGIN|LOGOUT); AutoBan + PunishmentService (new SM_QUIT_RESPONSE() via Close(packet)); PlayerCommonData
(new SM_STATUPDATE_DP(dp)). 9 golden fixtures migrated legacy-theory -> faithful-theory (SM_DELETE_WAREHOUSE_ITEM reuses
ResolveItemDeleteType; SM_SKILL_CANCEL uses PacketHarnessCreature; SM_CLOSE_QUESTION_WINDOW faithful factories; SM_FRIEND_RESPONSE
uses the (string,int) ctor). The legacy theory1 now holds only the 2 heavy webs SM_ITEM_USAGE_ANIMATION + SM_ATTACK_STATUS.
Batch9 (5): retire 5 zero-prod test-only Sm* whose dedicated slop-tests merely re-assert the byte shape the golden faithful
theory already validates against the Java oracle — SmPosition SmPositionSelf SmWeather SmLookAtObject SmForcedMove. Deleted
the Sm*.cs (+ orphaned ObjectPositionSnapshot/PositionSelfSnapshot/LookAtObjectSnapshot/ForcedMoveSnapshot records) and the 4
tests-of-slop (SmPositionPacketsTests/SmWeatherPacketTests/SmLookAtObjectPacketTests/SmForcedMovePacketTests). Suite 483->475
(net -8 slop tests), golden stays 196 byte-exact. Build0/golden196/bootstrap9 each batch.

**REMAINING 23 (SmAttackStatusEnums is the enum support file, not a packet):** low-count production repoints still to do —
SmAbyssRank(3) SmAbyssRankUpdate(2) SmAutoGroup(7) SmIconInfo(2) SmShowBrand(2) SmTitleInfo(5) SmLegionEdit(2,static-factory)
SmLegionDominionRank(1) SmLegionHistory(test-only) SmFindGroup(test-only) SmPet(test-only) SmPetEmote(test-only)
SmMotion(5) SmQuestionWindow(6). DEFERRED this run: **SmKey + SmPong** — both test-only but bound to GameCryptTests, which
tests `GameCrypt` server-side framing via the reworked `GameServerPacket.SerializeFrame(GameCrypt)` shortcut. The faithful
SM_KEY/SM_PONG (AionServerPacket) have NO SerializeFrame — their only write path is `Write(AionConnection,ByteBuffer)` +
`con.Encrypt`, which needs a live/uninitialized AionConnection the GameCrypt test harness doesn't build. Migrating these is a
crypt-infra harness change (route GameCryptTests through the faithful Write seam), not a packet repoint — defer to that increment.
**SmGameTime** still DEFERRED (singleton-vs-DI seam, unchanged below). HEAVY WEBS (LAST): SmDialogWindow / SmSystemMessage /
SmItemUsageAnimation / SmAttackStatus(+SmAttackStatusEnums) / SmEmotion.

## Sm* DUPLICATE-PACKET RETIREMENT — BATCH 6-7: faithful repoint of 21 low-consumer survivors (2026-06-17, 2 commits dbcbfa32a + d10a34b75 rrfarmer). 58 -> 37 remaining.

Batch6 (14, commit dbcbfa32a): 12 test-only golden-harness migrations (SmFlyTime SmWindstream SmUnwrapItem SmCraftAnimation SmGfWebshopTokenResponse SmDeleteHouse SmDeleteHouseObject SmDeleteCharacter SmRestoreCharacter SmNicknameCheckResponse SmStatUpdateHp SmStatUpdateMp) + 2 production repoints (SmActionAnimation<-ClassChangeService -> SM_ACTION_ANIMATION(ActionAnimation.CLASS_CHANGE), faithful per Java; SmCustomSettings<-TransformModel x2 -> SM_CUSTOM_SETTINGS).
Batch7 (7, commit d10a34b75): production repoints SmSummonPanelRemove+SmSummonOwnerRemove (SummonsService), SmDpInfo + SmStatUpdateExp x2 (PlayerCommonData), SmLearnRecipe + SmRecipeDelete (RecipeList), SmDeleteItem (Equipment.Part3). All byte-verified vs SM_* twin, golden cases migrated to faithful theory, Sm*.cs deleted. Build0/golden196/full483 each batch.

**THE DURABLE RETIREMENT RECIPE (golden harness has TWO theories in GoldenPacketFixtureTests.cs):** theory1 `CsharpPayloadMatchesJavaGoldenFixture` serializes Sm* via `SerializeUnencryptedPayload`/SerializeFrame (the GameServerPacket-typed `Reconstruct` switch); theory2 `FaithfulCsharpPayloadMatchesJavaGoldenFixture` serializes faithful SM_* via `CaptureWriteImplPayload` (the AionServerPacket-typed `ReconstructFaithful` switch). Both read the SAME `parity-artifacts/golden/packets/SM_FOO.json` Java-oracle fixture. To retire an Sm* whose only consumer is the harness: (1) byte-verify SM_* WriteImpl == Sm* WritePayload + ctor args match; (2) MOVE `[InlineData("SM_FOO.json")]` theory1->theory2; (3) MOVE+rename the `Reconstruct` case to `ReconstructFaithful` using the SM_* ctor, adapting enum/class-enum args faithfully (SM_DELETE_ITEM: int mask -> ItemDeleteType instance via the new reflection `ResolveItemDeleteType` helper; SM_ACTION_ANIMATION: int -> ActionAnimation enum); (4) repoint any production `new SmFoo`->`new SM_FOO`; (5) delete Sm*.cs. CENSUS GOTCHA: use `grep -rE "new ([A-Za-z0-9_.]*\.)?$base\("` — many consumers fully-qualify the ctor so a bare `new SmFoo` regex undercounts to 0; static-factory consumers (`SmFoo.Method(...)`, e.g. SmLegionEdit/SmCloseQuestionWindow) won't match `new` at all.

**REMAINING 37 — next batch candidates (all have faithful SM_* twins; do heavy webs LAST):** SmQuitResponse(2) SmBlockResponse(2) SmFriendResponse(2) SmFriendNotify(3) SmCloseQuestionWindow(static-factory, test+1prod) SmBindPointTeleport(2) SmSkillCancel(test-only, SM_SKILL_CANCEL faithful needs a Creature ctor -> use PacketHarnessCreature/PositionedHarness) SmDeleteWarehouseItem(test-only, ItemDeleteType-mask via ResolveItemDeleteType helper) SmStatUpdateDp(2). DEFER-CHECK SmGameTime: faithful SM_GAME_TIME is PARAMETERLESS (reads GameTimeService.GetInstance() singleton) but reworked SmGameTime(GameMinutes) is fed by the DI GameTimeService instance — repointing crosses the singleton-vs-DI seam; verify the singleton is the same source before repointing or defer.
HEAVY WEBS (LAST): SmDialogWindow / SmSystemMessage / SmItemUsageAnimation / SmAttackStatus (+SmAttackStatusEnums.cs, the enum file, retire together) / SmEmotion.

## Sm* DUPLICATE-PACKET RETIREMENT — zero-consumer dead-island sweep DONE (2026-06-17, 5 commits rrfarmer)

Java has NO `Sm*` packets — the faithful family is `SM_*` (underscore), registered in ServerPacketsOpcodes.cs. The 126 reworked
PascalCase `Sm*` were duplicate twins of an `SM_*` (golden proved 138/142 byte-identical), consumed by the now-retired
WorldNpc/Kisk/Rift/drop slop. **Retired ALL 68 zero-consumer dead-islands this run (126 -> 58 remaining).** Each verified to have a
faithful `SM_*` twin registered at the same opcode (`Sm*` inline `const int PacketOpCode = N` == `AddPacketOpcode(N, typeof(SM_FOO))`;
`Sm*` are not themselves registered), 0 production+test refs, then deleted. All-green-or-revert each batch (build 0 / golden 196 /
full 483). Batches: B1 (12) B2 (15) B3 (15) B4 (19) B5 (7 final).

**REMAINING 58 Sm* — ALL have >=1 LIVE consumer (faithful repoint required, NOT dead-island delete):**
- **Test-only consumers (~8)** — golden harness reconstructs them via the C#-only SerializeFrame path (GoldenPacketFixtureTests:
  SmBindPointTeleport/SmCloseQuestionWindow/SmFlyTime/...; GameCryptTests: SmKey/SmPong; SmWeatherPacketTests). Migration needs the
  golden harness to serialize a faithful `AionServerPacket` via the Write seam (the documented uninitialized-AionConnection harness
  gap; precedent SM_PLAYER_INFO/SM_STATS_INFO), OR repoint + re-baseline the fixture.
- **Low-count (1-2) production-service consumers** — straightforward faithful repoint `new SmFoo(args)` -> `new SM_FOO(args)` after
  verifying ctor args + byte-identical Write/WriteImpl, then delete. E.g. SmAbyssRankUpdate<-AbyssPointsService,
  SmActionAnimation<-ClassChangeService, SmAutoGroup<-PeriodicInstanceRegistrationService.
- **Heavy live webs (do LAST)** — SmDialogWindow (109), SmSystemMessage (67), SmItemUsageAnimation (54), SmAttackStatus (51),
  SmEmotion (11). NOTE SmAttackStatusEnums.cs is NOT a packet (enum file SmAttackStatusType/SmAttackStatusLog) consumed by
  SmAttackStatus.cs — retire it together with SmAttackStatus.

NEXT BATCH RECOMMENDATION: the ~8 test-only + the 1-2-count production-service repoints (small byte-verify-then-repoint units), then
the 5 heavy-consumer packets.

## GOLDEN SUITE 194 -> 195 (2026-06-17) — equippable seam REUSE: ENCHANT_INFO SUB-OBJECT writers (socketed ManaStone + GodStone)

Extended the equippable-item/ItemInfoBlob seam from per-type-blob coverage to the FIRST populated-sub-object path. **Byte-exact
on first capture, 0 fidelity bugs** (EnchantInfoBlobEntry.cs socketed-manastone + godstone branches + ManaStone/GodStone/
ItemStone/Item all faithful 1:1). ONE NEW fixture **SM_INVENTORY_ADD_ITEM_SUBOBJECT.json** (3 cases, distinct objectIds
268700201-203, no clobber), all on the proven 1H-SWORD base:
- (a) **socketedManastones** — two ManaStones at slots 0 + 2 (distinct itemIds 167000001/167000002) via
  `item.getItemStones().add(new ManaStone(objId,itemId,slot,NEW))`. The ENCHANT_INFO `createManastoneMap` -> slot->stone map;
  the `Item.MAX_BASIC_STONES`(6) loop writes `stone.getItemId()` at populated slots, 0 elsewhere.
- (b) **godStone** — `item.setGodStone(new GodStone(item,0,godStoneId,null,NEW))` -> `getGodStoneId()`==168000123.
- (c) **manastonesAndGodStone** — BOTH branches on one item.

**Why BOUNDED**: ManaStone ctor's only dep is `DataManager.ITEM_DATA.getItemTemplate(itemId)` (empty non-null ItemData -> null,
tolerated; writer reads only ItemStone base scalars slot/itemId). GodStone ctor takes godstoneInfo DIRECTLY (null OK; writer
reads only getItemId()) — DataManager-free. `setGodStone(non-null)` just assigns the field (DAO path is null-arg only). NEW
bounded seam: empty ITEM_DATA (`DataManager.ITEM_DATA = new ItemData()` / `SetAutoProperty(...ItemDataDh, new ItemData())`).

**REMAINING sub-object writers — CONDITIONING/COMPOSITE/POLISH all GOLDEN'D 2026-06-17 (fixture SM_INVENTORY_ADD_ITEM_SUBOBJECT2.json, 3 cases, distinct objectIds 268700301-303, byte-exact first capture, 0 bugs):**
- **CONDITIONING_INFO** — DONE. `getConditioningInfo() != null` via a `ChargeInfo(chargePoints,item)` pinned on the private
  `conditioningInfo` field (ctor reads `getImprovement()`==null -> deterministic); writer = `writeD(getChargePoints())` (== ctor arg).
- **COMPOSITE_ITEM (fusion)** — DONE. `setFusionedItem(fusionedTemplate, bonusStatsId=0, optionalSockets)` -> `hasFusionedItem()`;
  bonusStatsId 0 short-circuits `setFusionedItemBonusStats` (NO `fusionedItemTemplate.getStatBonusSetId()` deref) so it's bounded
  WITHOUT the StatBonusSet data. Writer = `writeD(getFusionedItemId())` + 24 zero fusion-stone bytes (no fusion stones) +
  `writeC(optionalSockets)` + `writeC(0)`. Fusion stones (the populated branch) reuse the ManaStone seam if ever needed.
- **POLISH_INFO** — DONE. Fires when `template.isCanPolish()` (CAN_POLISH mask bit 1<<17). Writer = `writeD(idian==null?0:getPolishCharge())`;
  with a null idian stone it writes 0 deterministically — so POLISH_INFO is golden'able WITHOUT an IdianStone.
- **IdianStone (idian-polished, in ENCHANT_INFO + the non-null POLISH path)** — STILL UNBOUNDED for the unit harness: ctor derefs
  `getItemTemplate(itemId).getActions().getPolishAction()` AND `template.getIdianAction().getBurnDefend()` -> NREs on an empty
  ItemData. Needs a populated ItemTemplate with IdianAction + PolishAction (a heavier ITEM_DATA seam). DEFER.

The bounded item/ItemInfoBlob sub-object vein is now EXHAUSTED (only IdianStone remains, unbounded). NEXT major vein = the
live-World increment (SM_PLAYER_SPAWN / SM_DIE), still blocked on the Java static-final World singleton.

## GOLDEN SUITE 193 -> 194 (2026-06-17) — equippable seam REUSE: SHIELD + WING + PLUME per-type blobs + TEMPERED-plume ENCHANT_INFO branch

Reused the SAME equippable-item/ItemInfoBlob seam for the THREE remaining per-type blob writers (selected BEFORE isArmor()/
isWeapon() in `getFullBlob`) + the tempered-plume ENCHANT_INFO branch. **Byte-exact on first capture, 0 fidelity bugs**
(ShieldInfoBlobEntry.cs + WingInfoBlobEntry.cs + PlumeInfoBlobEntry.cs + EnchantInfoBlobEntry.cs plume branch all faithful
1:1). ONE NEW fixture **SM_INVENTORY_ADD_ITEM_PERTYPE.json** (5 cases, distinct objectIds 268700101-105, no clobber):
- (a) **SHIELD** -> **SLOTS_SHIELD** = `writeQ(getSlotFor(getItemSlot()).getSlotIdMask())` [SHIELD -> ItemSlot.SUB_HAND] +
  `writeQ(0)` + `writeDyeInfo(getItemColor())` (null -> 4 zero bytes). SHIELD subType -> ArmorType.GENERAL -> isArmor() true,
  != ACCESSORY/BELT -> **isCloth() true** -> host trailing byte 1.
- (b) **WING** -> **SLOTS_WING** = `writeQ(getSlotFor(getItemSlot()).getSlotIdMask())` [WING -> ItemSlot.WINGS] + `writeQ(0)`.
  WING subType -> ArmorType.GENERAL -> isArmor() true -> **isCloth() true** -> byte 1.
- (c) **PLUME (untempered)** -> **PLUME_INFO** = `writeQ(getSlotFor(getItemSlot()).getSlotIdMask())` [PLUME -> ItemSlot.PLUME] +
  `writeQ(0x100000)` + `writeD(0)`x4. PLUME subType -> EquipType.PLUME (NOT armor) -> **isCloth() false** -> byte 0.
- (d)(e) **TEMPERED PLUME** (`tempering>0 && itemGroup==PLUME`) exercising the **ENCHANT_INFO plume branch**: pins
  `template.temperingName` (so `getTemperingName().equals("TSHIRT_PHYSICAL")` doesn't NPE) + `item.setTempering(5)` +
  `setRndPlumeBonusValue(17)`. (d) name "TSHIRT_PHYSICAL" -> PLUM_PHISICAL_ATTACK (id 30, boost 4*5=20+17=37); (e) non-match
  name -> PLUM_BOOST_MAGICAL_SKILL (id 35, boost 20*5=100+17=117). 1st stat always PLUM_HP (id 42, boost 150*5=750). Both
  PlumStatEnum branches verified byte-exact.

**Per-type blob coverage COMPLETE**: SLOTS_WEAPON + SLOTS_ARMOR + SLOTS_ACCESSORY + SLOTS_SHIELD + SLOTS_WING + PLUME_INFO +
EQUIPPED_SLOT/ENCHANT_INFO(inc. dyed + tempered-plume)/PREMIUM_OPTION/GENERAL_INFO all golden'd via the bounded simple-ctor
seam. Remaining heavier sub-paths (socketed-manastone / godstone / idian-polished / conditioned / fusioned-COMPOSITE) each
populate ONE more Item sub-object 1:1 both sides — bounded but not yet golden'd. NEXT vein: the live-World increment
(SM_PLAYER_SPAWN / SM_DIE) per the integration-harness plan.

## GOLDEN SUITE 191 -> 193 (2026-06-17) — equippable seam REUSE: ARMOR + ACCESSORY per-type blobs + DYED branch + SM_VIEW_PLAYER_DETAILS

Reused the equippable-item/ItemInfoBlob seam for the OTHER per-type blob writers + a 2nd packet, extending the SAME
`GoldenWorldPacketFixtureGeneratorTest`+`GoldenWorldPacketFixtureTests`. **Byte-exact on first capture, 0 fidelity bugs**
(ArmorInfoBlobEntry.cs + AccessoryInfoBlobEntry.cs + SM_VIEW_PLAYER_DETAILS.cs all faithful 1:1). Two NEW fixtures (distinct
objectIds 268700002/3/4 + 268900001, no clobber of the weapon fixture):
- **SM_INVENTORY_ADD_ITEM_VARIANTS.json** (3 cases): (a) **PL_TORSO armor undyed** -> **SLOTS_ARMOR** (isArmor, PLATE !=
  ACCESSORY); writer = `writeQ(getSlotFor(getItemSlot()).getSlotIdMask())` [PL_TORSO -> ItemSlot.TORSO, single slot] +
  `writeQ(0)` + `writeDyeInfo(getItemColor())` (null -> 4 zero bytes); isCloth() true -> host trailing byte 1. (b) **RING
  accessory** -> **SLOTS_ACCESSORY**; reads `getSlotsFor(getItemSlot())` [RING -> RING_LEFT|RING_RIGHT, length 2] -> two-slot
  branch writeQ(slots[0])+writeQ(slots[1]); isCloth false -> byte 0. (c) **PL_TORSO armor DYED** (itemColor 0x3399CC,
  colorExpireTime stays 0 -> getColorTimeLeft()==0, NO clock) -> dye-populated branch fires in BOTH SLOTS_ARMOR AND
  ENCHANT_INFO writeDyeInfo (`013399CC` appears twice, verified). `getItemSlot()` = `itemGroup.getValidEquipmentSlots()` so
  ALL slot bytes derive from the pinned itemGroup (same principle as the SWORD weapon pin).
- **SM_VIEW_PLAYER_DETAILS.json** (1 case, 2-item view: weapon + armor): ctor reads ONLY `player.getObjectId()` + items.size();
  writeImpl = targetObjId + const 11 + itemSize + per-item (writeD(0) + templateId + getL10n() + getFullBlob(player,item).writeMe()).
  Player passed to getFullBlob ONLY as blob owner (never dereferenced for deterministic items) -> NO live Player/Legion/
  appearance/equipment graph. Allocate an UNINITIALIZED Player (Unsafe.allocateInstance / RuntimeHelpers.GetUninitializedObject,
  the SM_REPURCHASE/SM_FIND_GROUP precedent) with ONLY AionObject.objectId pinned; items reuse the seam's EXACT weapon+armor
  builders so per-item blobs are byte-identical to the SM_INVENTORY_ADD_ITEM fixtures.

**REUSABLE**: the per-type-blob + dyed-branch seam now covers SLOTS_WEAPON/SLOTS_ARMOR/SLOTS_ACCESSORY + undyed/dyed.
**SLOTS_SHIELD** (== armor writer, SHIELD group), **SLOTS_WING** (WingInfoBlobEntry), **PLUME_INFO** (PlumeInfoBlobEntry — note
ENCHANT_INFO has a PLUME tempering>0 branch) are the remaining per-type variants, each one more itemGroup pin, no new substrate.
The heavier sub-paths (socketed-manastone / godstone / idian-polished / conditioned / fusioned-composite / tempered-plume) each
populate ONE more Item sub-object 1:1 both sides. The uninitialized-Player-as-blob-owner seam is reusable for any item-list
packet reading only player.getObjectId() (SM_WAREHOUSE_*, etc.). Build 0, golden 193, suite 480/0, bootstrap 9/9.

## GOLDEN SUITE 190 -> 191 (2026-06-17) — EQUIPPABLE-item blob path (SM_INVENTORY_ADD_ITEM, weapon blob)

Extended the item/ItemInfoBlob seam from the GENERAL_INFO-only path to the **EQUIPPABLE-item blob path** — the first
golden driving the full equippable-weapon `ItemInfoBlob.getFullBlob` chain. **Byte-exact on first capture, 0 fidelity bugs**
(SM_INVENTORY_ADD_ITEM.cs + ItemInfoBlob.cs + EquippedSlotBlobEntry.cs + WeaponInfoBlobEntry.cs + EnchantInfoBlobEntry.cs +
PremiumOptionInfoBlobEntry.cs + GeneralInfoBlobEntry.cs + Item.cs all faithful 1:1):
- **SM_INVENTORY_ADD_ITEM** (1 fixture / 1 case: equippable 1H sword bought from npc, `ItemAddType.BUY` mask 0x1C so the
  ITEM_COLLECT slot branch is skipped). writeImpl writes objectId + templateId + `getL10n()`, then
  `getFullBlob(player,item).writeMe()`, then `(equipmentSlot & 0xFFFF)` + `isCloth()?1:0`. Pinning the template to
  **`ItemGroup.SWORD`** (ONE_HAND weapon: `getEquipType()==WEAPON`, isWeapon true, **isTwoHandWeapon false**, valid equip
  slots = MAIN_OR_SUB), no fusion / no stones / packCount 0 / not STIGMA_SHARD, makes getFullBlob add EXACTLY:
  **EQUIPPED_SLOT** (writeQ `isEquipped?equipmentSlot:0` -> 0, unequipped) + **SLOTS_WEAPON** (`getSlotsFor(MAIN_OR_SUB)` ->
  [MAIN_HAND, SUB_HAND], length 2, non-2H else branch -> writeQ mask 1, writeQ mask 2) + **ENCHANT_INFO** + **PREMIUM_OPTION**
  + **GENERAL_INFO**. NOT WING/SHIELD/PLUME/armor/accessory (no SLOTS_* variant), conditioningInfo null (no CONDITIONING_INFO),
  mask has no CAN_POLISH bit (no POLISH_INFO), modifiers null (no STAT_BONUSES), not COMPOSITE (no fusion / not 2H).

Seam details (both sides identical): every ENCHANT_INFO / PREMIUM_OPTION read is deterministic on the bare simple-ctor
weapon (mirroring the GENERAL_INFO seam) — isSoulBound false, enchantLevel 0, `getItemSkinTemplate()==itemTemplate` (skin
null) -> templateId, **isIdentified() true** (`maxTuneCount` pinned 0 -> tuneCount stays 0) -> optionalSockets / enchantBonus
/ bonusStatsId / tuneCount all 0, hasManaStones false, godStoneId 0, `getColorTimeLeft()` 0 (colorExpireTime 0, no clock) ->
writeDyeInfo(itemColor null), idianStone null, tempering 0 (not PLUME branch), isAmplified false (enchantType 0), buffSkill 0,
isCloth false (weapon). Reuses the EXISTING ITEM_CLEAN_UP holder seam (GENERAL_INFO reads it). NO live Player deref (player
arg null), NO manastone/godstone/idian/conditioning/fusion sub-object cascade triggered.

**REUSABLE** for the rest of the equipped-item family: the per-type blob is the only thing that varies — armor adds
`ArmorInfoBlobEntry` (writeQ slot + writeQ 0 + writeDyeInfo itemColor [null -> same dye bytes]), accessory/shield/wing/plume
their own SLOTS_* variant; all read the SAME bare-item enchant/premium/general state already proven here. The heavier
sub-paths (manastone-socketed / godstone / idian-polished / conditioned / fusioned / dyed / tempered-plume items) each
populate one more Item sub-object 1:1 both sides — incremental, not a new substrate. Packets that reuse this seam directly:
SM_VIEW_PLAYER_DETAILS / SM_INVENTORY_UPDATE_ITEM (EQUIP_UNEQUIP/CHARGE/POLISH single-blob branches) / SM_WAREHOUSE_*.
Build 0, golden 191, suite 478/0, bootstrap 9/9.

## GOLDEN SUITE 189 -> 190 (2026-06-17) — FIRST item/ItemInfoBlob seam (SM_INVENTORY_UPDATE_ITEM, GENERAL_INFO blob)

Built the deferred **item/ItemInfoBlob integration-harness increment** — the first golden driving a packet through a
live `Item` game-object + the `ItemInfoBlob` blob-writer family. **Byte-exact on first capture, 0 fidelity bugs**
(SM_INVENTORY_UPDATE_ITEM.cs + ItemInfoBlob.cs + GeneralInfoBlobEntry.cs + Item.cs all faithful 1:1):
- **SM_INVENTORY_UPDATE_ITEM** (1 fixture / 2 cases: with-creator "Daeva" / null-creator). Default ctor uses
  `ItemUpdateType.DEC_ITEM_USE` -> `ItemInfoBlob.getFullBlob(player,item)`. Pinning the template to **`ItemGroup.NONE`**
  (`getValidEquipmentSlots()==0`, isWeapon/isArmor/isTwoHandWeapon false), no fusion, packCount 0, not STIGMA_SHARD ->
  getFullBlob adds EXACTLY ONE entry: **GENERAL_INFO**, which reads ONLY Item+ItemTemplate scalars
  (mask / count / creator / secondsUntilExpiration [expireTime 0 -> 0, no clock] / temporaryExchangeTimeRemaining [0] /
  itemId) + `DataManager.ITEM_CLEAN_UP.hasAccountOrLegionWhStorabilityDisabled` (empty bplist -> false -> writeH 0). Host
  packet also writes objectId + `template.getL10n()` (= `ChatUtil.l10n(desc)`, pure scalar) + the DEC_ITEM_USE mask 0x16.

Seam details (both sides identical): the simple `Item(objId, template)` ctor is deterministic (expireTime 0 / enchantType
0 / improvement null -> no ChargeInfo, no clock). **`canTune()` = `maxTuneCount != 0` and the field default is -1**, so a
reflectively-built template (no JAXB afterUnmarshal) would have canTune true — pinned `maxTuneCount = 0` both sides (what
afterUnmarshal sets for a slot-0 item; GENERAL_INFO doesn't read it anyway). **NEW bounded seam: ITEM_CLEAN_UP** =
`ItemRestrictionCleanupData` with an EMPTY (non-null) `bplist` (the method does `bplist.stream()/.Any()` which NPEs on the
null default — the uninitialized-StaticData C# bridge skips field initializers). NO live Player deref (player arg null
both sides), NO World/stones/enchant/godstone cascade, NO DataManager beyond ITEM_CLEAN_UP.

**REUSABLE** for the item family's GENERAL_INFO-only path (SM_INVENTORY_ADD_ITEM single non-equip item, SM_WAREHOUSE_*).
The **EQUIPPABLE path** (weapon/armor/accessory/wing/plume/shield) is the heavier next item increment: getFullBlob adds
EQUIPPED_SLOT + per-type blob + ENCHANT_INFO + PREMIUM_OPTION, whose blob-writers read enchant/manastone/godstone/idian/
conditioning state on the Item (needs those sub-objects populated 1:1 both sides). Build 0, golden 190, suite 477/0,
bootstrap 9/9.

## GOLDEN SUITE 187 -> 189 (2026-06-17) — real-Npc-ctor seam reuse (SM_MOVE + SM_SELL_ITEM); Npc-reader family EXHAUSTED

Reused the bounded real-`Npc(controller,spawn,template)` ctor seam (SM_NPC_INFO) for the LAST TWO not-yet-golden'd
Npc/Creature-reading packets — **byte-exact on first capture, 0 fidelity bugs**:
- **SM_MOVE** (1 fixture / 2 cases: mask 0; POSITION|MANUAL|ABSOLUTE=224). WriteImpl reads objectId + x/y/z/heading
  (the un-spawned Npc's WorldPosition == 0, identical both sides) + movementMask; NpcMoveController is a plain
  CreatureMoveController (`pmc==null`) so the POSITION|MANUAL branch writes getTargetX2/Y2/Z2 (TargetDest* default 0).
  No glide/vehicle bits set -> those branches unreachable. Pure seam reuse, NO new stub.
- **SM_SELL_ITEM** (1 fixture / 1 case). Added a bounded **TRADE_LIST_DATA holder seam** (one purchase template under
  the npc id: NORMAL type / buyPriceRate 115 / two trade tabs, via the private npcPurchaseTemplateData index, mirrored
  both sides). The npc template has NO talkInfo -> SupportsAction(..) false -> canSell/canBuy/canPurchase all false
  (showBuyTab=showSellTab=0). The purchase template being present means tradeNpcType/buyPriceRate/tabs come from the
  template, so `PricesService.getVendorSellModifier()` (config) is deliberately NOT reached — important because the
  C# `PricesConfig.VENDOR_SELL_MODIFIER` has a field initializer (=20) while the Java field is config-loaded (==0 in a
  no-config test), so the null-template path would be a HARNESS mismatch, not a real bug. Sidestepped by the template.

Also added `rating = NORMAL` to the shared `buildNpcTemplate` (Npc.getSeeState() needs a non-null rating); verified
SM_NPC_INFO bytes UNCHANGED (it reads getVisualState() only, never getRating()).

**Npc/Creature-reading SM_* family is now EXHAUSTED on this seam.** Census: every ServerPacket whose ctor takes an
Npc/Creature is golden'd — SM_PLAYER_STATE / SM_SKILL_CANCEL / SM_EMOTION / SM_FORCED_MOVE / SM_RESURRECT /
SM_CASTSPELL / SM_ABNORMAL_EFFECT / SM_ATTACK / SM_MANTRA_EFFECT / SM_TARGET_UPDATE / SM_TRANSFORM via the older
PacketHarnessCreature harness; SM_TRADE_IN_LIST (uninitialized-Npc); SM_NPC_INFO / SM_MOVE / SM_SELL_ITEM (real-Npc
ctor). No Npc-reader remains that needs only the seam. The NEXT increment requires a live **Player** or live **World**
graph (SM_PLAYER_SPAWN/SM_DIE et al., the Java static-final World singleton — the unchanged blocker below), OR the
item/ItemInfoBlob seams (SM_TRADELIST/SM_LOOT_ITEMLIST). Build 0, golden 189, suite 476/0, bootstrap 9/9.

## GOLDEN SUITE 186 -> 187 (2026-06-17) — the REAL-Npc-ctor seam (SM_NPC_INFO), maximal Npc reader, 0 bugs

Built the bounded **real `Npc(controller,spawn,template)` ctor golden** — the first golden driving a packet through a
fully-constructed live Npc with real stat containers (NpcGameStats/NpcLifeStats/NpcMoveController), vs the
SM_TRADE_IN_LIST uninitialized-Npc. Golden'd **SM_NPC_INFO** (1 fixture / 2 cases: PEACE/ATTACKABLE FLAG npc) by
EXTENDING the same `GoldenWorldPacketFixtureGeneratorTest` (Java) + `GoldenWorldPacketFixtureTests` (C#) seam.
**Byte-exact on FIRST capture, 0 fidelity bugs** (SM_NPC_INFO.cs faithful 1:1).

- **Why the real ctor is BOUNDED** (no World/Knownlist/SkillEngine/DataManager-cascade — the open question from the
  SM_TRADE_IN_LIST tick): trace `Npc ctor -> setupStatContainers() -> NpcLifeStats ctor -> getGameStats().getMaxHp()
  .getCurrent() -> NpcGameStats.getStat(MAXHP, statsTemplate.getMaxHp()) -> super.getStat (empty function map, no
  StatCapUtil pass) + owner.getAi().modifyOwnerStat(s)`. The two crux deps resolve cheaply:
  1. **AI = DummyAI.** With BOTH `NpcTemplate.ai == null` AND `SpawnTemplate.aiName == null`, the Creature ctor's
     `AIEngine.newAI(null, this)` returns a `DummyAI` (the `name == null` branch) — NO AIEngine registration needed.
     `DummyAI.modifyOwnerStat(Stat2)` is the AbstractAI base no-op. (Use the plain `SpawnGroup(worldId,npcId,0,null)` +
     `SpawnTemplate(spawnGroup,x,y,z,h,0,null,0)` ctors; SpawnTemplate.aiName defaults null.)
  2. **Stats = populated StatsTemplate.maxHp only.** The stats-function map is empty (no effects) so `getStat` returns
     the raw base value with NO StatCapUtil/time/random. `getMovementSpeedFloat()` reads `statsTemplate.getRunSpeed()`
     which is 0 when `speeds == null` (deterministic). So a StatsTemplate with just `maxHp` set is enough.
- **The other live reads are all deterministically 0 / pinnable:** `NpcSkillList(this)` reads
  `DataManager.NPC_SKILL_DATA.getNpcSkillList(npcId)` (empty holder -> null -> empty skill list);
  `TownService.getInstance().getTownIdByPosition(npc)` returns 0 (npc not spawned, plain SpawnTemplate) but the
  singleton ctor reads `DataManager.HOUSE_DATA.getLands()` -> seed an EMPTY HouseData (lands = empty list);
  the Npc position is `new WorldPosition(spawnTemplate.getWorldId())` so x/y/z/heading are ALL 0 (spawn coords do NOT
  reach the unspawned Npc) -> getX/Y/Z + mc.getTargetX2/Y2/Z2 + getHeading all write 0 (faithful, identical both sides).
- **Determinism pins (mirrored both sides):** objectId from `IDFactory.nextId()` is non-deterministic -> OVERWRITE the
  final `AionObject.objectId` field with a pinned value AFTER the ctor; `getType(player)` is computed in the SM_NPC_INFO
  ctor -> pin the `npc.type` field so it short-circuits and the player arg can be **null** (TribeRelationService never
  reached); FLAG template type -> `isFlag()`==true -> the time-dependent `isNewSpawn()` byte is unreachable (writeC 0x13).
- **NEW Java DB stub (reusable):** the prior throwing-`getConnection` stub made `IDFactory.getUsedIDs()` return NULL ->
  NPE in `lockIds`. Replaced with a **full empty-ResultSet JDBC proxy chain** (DataSource->Connection->PreparedStatement
  ->ResultSet: next()/last()/first() false, getRow() 0, close()/beforeFirst() no-op) so `getUsedIDs()` returns int[0]
  (the IDFactory lazy SingletonHolder ctor completes) and TownDAO.load returns empty maps. C# side:
  `IDFactory.RegisterInstance(new IDFactory())` if unbound; the bridge now seeds `NpcSkillDataDh` + `HouseDataDh` empty
  holders too (the uninitialized StaticData skips field initializers).

**REUSABLE for the stat-reading Npc family — YES.** The real-Npc-ctor seam now constructs a deterministic live Npc with
real NpcGameStats/NpcLifeStats/NpcMoveController + a populated StatsTemplate + DummyAI. Directly reusable for any
Npc-reading packet that needs the live stat containers / move controller / template (the heavier Npc packets beyond
objectId-only). To extend: populate the StatsTemplate attrs that packet reads + pin any extra Npc scalar (state/visual
state/level via template/target/equipment). **NEXT VEIN:** (a) more Npc-family packets on this seam, or (b) the still-
blocked live-World increment (SM_PLAYER_SPAWN/SM_DIE — Java static-final World singleton, unchanged blocker below).
Build 0, golden 187, suite 474/0, bootstrap 9/9.

## GOLDEN SUITE 185 -> 186 (2026-06-17) — FIRST live-Npc OBJECT seam (SM_TRADE_IN_LIST), uninitialized-Npc precedent

Built the lightest bounded **live-Npc game-object golden seam** — the first golden that drives a packet through a live
`Npc` instance rather than only scalar/holder/template state. Golden'd **SM_TRADE_IN_LIST** (1 fixture / 4 cases) by
EXTENDING the same `GoldenWorldPacketFixtureGeneratorTest` (Java) + `GoldenWorldPacketFixtureTests` (C#) seam.
**Byte-exact on FIRST capture, 0 fidelity bugs** (SM_TRADE_IN_LIST.cs faithful 1:1).

- **Why SM_TRADE_IN_LIST is the LIGHTEST live-Npc reader:** `writeImpl` reads ONLY `npc.getObjectId()` from the live
  object (NO template/stats/AI/World/Knownlist/MoveController/Spawn), plus a directly-constructed `TradeListTemplate`
  (NOT a DataManager read — the template object is passed into the ctor). The other Npc-typed ctors all pull heavy
  graphs: SM_NPC_INFO reads `npc.getType(player)` + `getMoveController()` + `getGameStats()` + `getLifeStats()` +
  `TownService` + `getSpawn()` + `getNpcObjectType()`; SM_MESSAGE(Npc) `writeImpl` needs `con.getActivePlayer()`;
  SM_SELL_ITEM pulls TRADE_LIST_DATA + tradelist items.
- **The bounded live Npc = an UNINITIALIZED instance.** The heavy single `Npc(controller,spawn,template)` ctor
  (BOTH sides — neither has an alternate ctor) runs `setupStatContainers()` -> `NpcLifeStats` ctor EAGERLY calls
  `owner.getGameStats().getMaxHp()` -> `NpcGameStats.getStat` -> `owner.getAi().modifyOwnerStat(s)`, so a real Npc
  needs a populated StatsTemplate AND a live AI. Since the packet reads ONLY objectId, the Npc is allocated WITHOUT a
  ctor (Java `Unsafe.allocateInstance(Npc.class)` / C# `RuntimeHelpers.GetUninitializedObject(typeof(Npc))` — the
  established AionConnection/AbyssRank harness precedent) with only the final `AionObject.objectId`/`_objectId` field
  pinned. TradeListTemplate built by reflectively setting `npcId`/`tradeNpcType`/`tradeTablist` (TradeTab.id) both sides.
- **4 cases cover both writeImpl branches:** full list (NORMAL type index 1, 3 tabs -> full payload), ABYSS single-tab
  (type index 2, different buy modifier), count==0 empty-tab-list -> early-return (empty payload), npcId==0 ->
  early-return (empty payload).

**Reusable for the rest of the Npc family?** PARTIALLY. The uninitialized-Npc seam is reusable for ANY Npc-reading
packet whose `writeImpl` reads ONLY pinnable scalar fields of the live object (objectId, and any field settable
without running the ctor). It is NOT enough for the stat/AI/template-reading Npc packets (SM_NPC_INFO etc.) — those
still need the real Npc ctor (StatsTemplate-populated NpcTemplate + a live/stub AI + NpcGameStats/NpcLifeStats), which
is the next (heavier) live-Npc increment. **NEXT VEIN:** either (a) the real-Npc-ctor increment (populate an
NpcTemplate with a StatsTemplate + stub the AI so NpcGameStats/NpcLifeStats build -> unlocks SM_NPC_INFO and the
stat-reading Npc family), or (b) the still-blocked live-World increment (SM_PLAYER_SPAWN/SM_DIE — Java static-final
World singleton, unchanged blocker below). Build 0, golden 186, suite 473/0, bootstrap 9/9.

## GOLDEN SUITE 183 -> 185 (2026-06-17) — holder-seam reuse: SKILL_DATA + QUEST_DATA single-template readers (SM_SKILL_COOLDOWN, SM_QUEST_ACTION)

Reused the bounded DataManager-holder seam (introduced for SM_TELEPORT_LOC) for two MORE single-DataManager-template-reader
SM_* packets, extending the SAME `GoldenWorldPacketFixtureGeneratorTest` (Java) + `GoldenWorldPacketFixtureTests` (C#)
seam (both in the GoldenDataManager serial collection — the ONE bridge now seeds AbsoluteStatsData + PLAYER_EXPERIENCE_TABLE
+ WorldMaps2 + SkillDataDh + Quests so whichever serial class wins the singleton registration is fully populated).
**Byte-exact on FIRST capture, 0 fidelity bugs** (both .cs faithful 1:1):

- **SM_SKILL_COOLDOWN** (SKILL_DATA holder, 1 case): the scalar ctor `(int skillId, long expirationTimeMillis)`. With
  `expirationTimeMillis = 0`, `getRemainingSeconds()` short-circuits to 0 (NO `System.currentTimeMillis()` read) so the
  packet is deterministic; the only DataManager read is `getDurationMillis()` = `SKILL_DATA.getSkillTemplate(skillId)
  .getCooldown() * 100`. Seam = a SkillData carrying ONE template (skillId 1968, raw cooldown 250 -> wire 25000), built
  by reflectively populating the private `skillTemplateById` map both sides (no JAXB/file/AfterUnmarshal). The OTHER two
  ctors (Player + cooldown map / Player + resettable ids) read a live Player skill list -> NOT golden'able this way.
- **SM_QUEST_ACTION** (QUEST_DATA holder, 5 cases): the scalar ctors `(questId)`->UNK, `(questId,timer)`->TIMER,
  `(questId,sharerId,shareInAlliance)`->SHARE (covers the UNK/TIMER/SHARE-alliance/SHARE-group switch branches) PLUS the
  extra-category early-return (empty payload). Its ONLY DataManager read is `QUEST_DATA.getQuestById(questId)
  .getExtraCategory()`; if `!= NONE` writeImpl returns before writing anything. Seam = a QuestsData carrying TWO templates
  (id 1006 extraCategory=NONE -> full payload; id 1007 extraCategory=COIN_QUEST -> empty payload), built by reflectively
  populating the private `questTemplates` map both sides. The 4th ctor `(ActionType, QuestState)` (ADD/UPDATE) needs a
  live QuestState -> NOT golden'able with the scalar seam (ADD/UPDATE branches uncovered, deferred).

**SINGLE-DATAMANAGER-TEMPLATE-READER VEIN NOW ESSENTIALLY EXHAUSTED.** Surveyed all DataManager-reading SM_*.cs
(11 total). The clean single-holder-template + scalar readers are now ALL golden'd: SM_TELEPORT_LOC (WORLD_MAPS_DATA),
SM_SKILL_COOLDOWN (SKILL_DATA), SM_QUEST_ACTION (QUEST_DATA). The remaining DataManager-reading packets ALL also pull in
a LIVE object alongside the template, so they belong to the heavier integration-harness increments, not this bounded vein:
- **SM_SELL_ITEM** -> TRADE_LIST_DATA template + a live `Npc` (getNpcId) and its tradelist items.
- **SM_LOOT_ITEMLIST** -> ITEM_DATA template + live `DropNpc`/`DropItem`/`Player`.
- **SM_SKILL_LIST** -> SKILL_DATA + live `Player.getSkillList()`.
- **SM_TRADELIST** -> TRADE_LIST_DATA + live `Npc`/`Player`/price calc.
- **SM_INSTANCE_INFO** -> INSTANCE_COOLTIME_DATA + live `Player`/activePlayer + `System.currentTimeMillis()`.
- **SM_PET** -> PET_DATA + live `Pet`/`Player`. **SM_UPGRADE_ARCADE** -> 4 DataManager reads + live state.
- **SM_L2AUTH_LOGIN_CHECK** -> account/login state (not a clean holder-template read).
**NEXT VEIN = the deferred LIVE-WORLD integration increment** (SM_PLAYER_SPAWN/SM_DIE, blocked on the Java static-final
World singleton — see the increment-1 section below for the exact blocker), OR the item/ItemInfoBlob + Npc seams that
unlock SM_SELL_ITEM/SM_LOOT_ITEMLIST/SM_TRADELIST/SM_NPC_INFO. Build 0, golden 185, suite 472/0, bootstrap 9/9.

## GOLDEN SUITE 182 -> 183 (2026-06-17) — integration-harness INCREMENT 1: the bounded WORLD_MAPS_DATA holder seam (SM_TELEPORT_LOC)

First step of the deferred integration-harness sub-project. Golden'd **SM_TELEPORT_LOC** (1 fixture / 3 cases:
regular-map / instance-map / regular-NONE-anim) via a NEW bounded **DataManager.WORLD_MAPS_DATA holder seam** —
the first golden that drives a packet through a World-family DataManager holder rather than only scalar/ctor state.
New Java generator `GoldenWorldPacketFixtureGeneratorTest` + C# `GoldenWorldPacketFixtureTests` (both join the
GoldenDataManager non-parallel collection). **Byte-exact on FIRST capture, 0 fidelity bugs** (SM_TELEPORT_LOC.cs is
faithful 1:1). Seam: a WorldMapsData carrying exactly TWO templates (one `instance=false` Morheim 220020000, one
`instance=true` Draupnir Cave 320080000), built identically both sides — C# via an uninitialized StaticData with the
`WorldMaps2` backing field set + the DataManager test ctor; Java via `DataManager.WORLD_MAPS_DATA` reflectively
populated (mapsById index, no JAXB/file). SM_TELEPORT_LOC.writeImpl is pure scalar; its ONLY non-ctor read is the
ctor's `WORLD_MAPS_DATA.getTemplate(mapId).isInstance()` branch (selects instanceId vs mapId for the channel field) —
so the two templates exercise both branches. `isInstance()` reads the raw `instance` field (NO twin-clamp), so
WorldConfig is irrelevant here.

**IMPORTANT — the LIVE-World seam (SM_PLAYER_SPAWN / SM_DIE) is NOT bounded this tick; exact blocker documented:**
SM_PLAYER_SPAWN.writeImpl reads `World.getInstance().getWorldMap(worldId).getTemplate().getBeginnerTwinCount()` and
SM_DIE reads `player.getWorldMapInstance().getInstanceHandler()`. The blocker is ALL on the **Java** side: Java's
`World` is a `static final SingletonHolder.instance = new World()` (World.java:345-347). It CANNOT be reflectively
overridden (Unsafe.allocateInstance + setting the static-final field is blocked, and just touching `SingletonHolder`
triggers the real `new World()` which NPEs on the unset `DataManager.WORLD_MAPS_DATA`). To make the real
`World.getInstance()` usable in the harness you MUST let the real `World()` ctor run, which:
(1) needs `DataManager.WORLD_MAPS_DATA` populated, then (2) builds a real `new WorldMap(template)` whose ctor runs the
instance-creation loop (getInstanceCount() >= 1 always), each iteration calling
`WorldMapInstanceFactory.createWorldMapInstance` -> `new WorldMapInstance(...)` whose ctor calls
`ZoneService.getInstance().getZoneInstancesByWorldId(mapId)` (ZoneService's instance field eagerly reads
`DataManager.ZONE_DATA.getZones()`, and getZoneInstancesByWorldId builds WorldZoneTemplate/PolyArea/ZoneInstance +
getNewZoneHandler) and `InstanceEngine.getInstance().getNewInstanceHandler`. So the live-World seam unavoidably pulls
in real `WORLD_MAPS_DATA` + `ZONE_DATA` + the zone/instance-handler graph = the heavier integration harness.
**What the live-World seam needs (for a later tick):** populate `DataManager.WORLD_MAPS_DATA` (one template, twin
clamps pinned identically both sides since `getBeginnerTwinCount()` is WorldConfig-clamped — Java generator leaves
`WORLD_MAX_TWINS_BEGINNER` at the uninitialized `0` => raw value, C# defaults it to `-1` => 0, so they DIVERGE unless
pinned) + `DataManager.ZONE_DATA` (at least an empty `ZoneData` so `ZoneService.getInstance()` doesn't NPE) + init
`ZoneService`/`InstanceEngine`, then either (a) Java: pre-populate those holders before the FIRST `World.getInstance()`
so the real `new World()` builds, or (b) build a real `World` via the C# `RegisterInstance` bridge equivalent and find
a Java equivalent (the static-final SingletonHolder is the crux — may need `--add-opens`/Unsafe.putObjectVolatile on
the holder's static field AFTER forcing its init with WORLD_MAPS_DATA already set, which still runs the real ctor).
Net: SM_PLAYER_SPAWN/SM_DIE remain the live-World integration-harness increment; SM_TELEPORT_LOC (this tick) is the
bounded holder-only down-payment. Build 0, golden 183, suite 470/0, bootstrap 9/9.

## DEFERRED FIDELITY BUG #2 (packet dual-serialization) — RE-ASSESSED: NOT a wire bug (2026-06-17, HEAD 1ad63f41c)

Re-assessed deferred fidelity bug #2 (packet-base-unification / dual serialization path). **It is NOT a
runtime wire-fidelity bug.** The GameServer live client send path is exclusively the faithful
`AionServerPacket.Write/WriteImpl` (via `AionConnection.WriteData`); `GameServerPacket.SerializeFrame/
WritePayload` is a C#-test-only invention with no Java counterpart, called only by the golden harness + unit
tests, never on the GS client wire. (The 184 `SerializeFrame/WritePayload` grep hits are the `Sm*` override
declarations plus the SEPARATE LoginServer/ChatServer packet families, which legitimately frame on their own
wires.) The skipped golden case `SM_GROUP_DATA_EXCHANGE` (a faithful `SM_*:AionServerPacket`) is skipped only
because the harness serialized via `SerializeFrame`, which faithful-only packets lack — a test-harness gap,
not a packet bug. Runtime wire-correctness is already faithful. Full unification (re-point ~138 duplicate-twin
`Sm*` -> faithful `SM_*`, delete `Sm*`, drop `GameServerPacket`) is **gated on the reworked-worldnpc-spawn-
cluster big-bang** (held for user go-ahead). **BOTH build-zero "real src fidelity bugs" are now resolved-or-
understood: #1 (RiftManager fan-out) fixed by pillar-a; #2 (this) wire-faithful, only cosmetic slop debt.**

**UN-SKIP DONE (2026-06-17):** `SM_GROUP_DATA_EXCHANGE` is no longer skipped. The bounded, test-tree-only
faithful-`WriteImpl` seam `CaptureWriteImplPayload` (GoldenPacketFixtureTests.cs:448-460) mirrors the Java
`capture()` (GoldenPacketFixtureGeneratorTest.java:755-771) 1:1 — `ByteBuffer.Allocate(8192).Order(LITTLE_ENDIAN)`
+ `SetBuf` + reflective `WriteImpl(null)` + read `Position()` bytes. NO uninitialized-AionConnection needed:
this packet's `WriteImpl` reads only ctor args (action/unk2/byteData), so `con` is passed as `null`. The
Java-oracle fixture (`parity-artifacts/golden/packets/SM_GROUP_DATA_EXCHANGE.json`, `"source":"Java"`, opcode
178, generator entries lines 45-52) drives 2 byte-exact cases: `nearbyBroadcast`=`01030000000102FF`,
`groupBroadcast`=`0207040000000A0B0C0D`. These are validated through the real faithful `SM_GROUP_DATA_EXCHANGE`
(`AionServerPacket`) write path, NOT a C# snapshot. The skipped case is now an active passing golden case.

## DEFERRED FIDELITY BUG #1 (RiftManager instance fan-out) — RESOLVED & VERIFIED (2026-06-17, HEAD cc67e1fe6)

Re-assessed the long-deferred `RiftManagerService` instance fan-out bug (old failing
`SpawnRift_FansOutPortalPairsAcrossWorldMapInstances`: Position.InstanceId expected [1,2,3], reworked path
produced [1,1,1]). **CLOSED, no source change needed.** The reworked `RiftManagerService` (parallel
single-path reimpl that built `WorldPosition` directly with no MapRegion, so InstanceId always defaulted to 1)
is **deleted** — retired by pillar-a; grep finds 0 source/test files, only docs reference it. Its slop test
was deleted with it. The faithful `Services/Rift/RiftManager.cs` (1:1 Java RiftManager.java) is the sole live
path: wired via `SpawnEngine.cs:174` (AddRiftSpawnTemplate at boot), `RiftService.cs:222` (SpawnRift),
`VortexService.cs:83` (SpawnVortex). Its fan-out loop `for i=1..instanceCount { SpawnInstance(i,...) }`
(RiftManager.cs:64-88 == Java RiftManager.java:63-82) threads `i` through
`World.SetPosition(npc, worldId, instance=i, ...)` -> `CreatePosition(...,instance)` -> resolves the REAL
`map.GetWorldMapInstance(instanceId).GetRegion(x,y,z)` MapRegion (World.cs:236-257), so Position.InstanceId
derives from the real per-instance region -> distinct [1,2,3]. Pillar-a making the live
World/WorldMapInstance/CreatePosition graph available is what closed it. Boot-time fan-out is exercised by the
DB-backed `GameServerBootstrapTests` full-boot (RiftService.InitRifts -> SpawnRift on real maps); no flaky
standalone test re-added. Of the two deferred src fidelity bugs, #1 is closed; #2 (packet dual-serialization,
packet-base-unification) remains.

## GOLDEN SUITE 167 -> 169 (2026-06-17, commit e1a37ea13)

Added 2 NEW Java-derived pure-formula parity cases via the existing mvn oracle
(`GoldenFormulaFixtureGeneratorTest` -> `parity-artifacts/golden/formulas/*.json`, read by
`GoldenFormulaFixtureTests.cs`):
- `StatCapUtil.limitValueForPvpOrPveStat(CombatMode,RatioType,int)` — 18 sub-cases across all 4
  (mode,type) cap buckets (PVP/PVE x ATTACK/DEFENSE) with below-min/in-range/at-edge/above-max
  clamp inputs; the post-aggregation ratio cap used in `StatFunctions.adjustDamageByPvpOrPveModifiers`.
- `SkillElement.getStatForElement()` — 7 sub-cases (NONE->null + 6 *_RESISTANCE); the elemental-defense
  stat lookup in `reduceDamageByElementalDefense`. Both byte/value-exact on first capture, 0 fidelity bugs.
RECIPE for the next pure-formula golden: pick a method that reads ONLY its args (no state/config/random),
add a `generateXxx` in the Java generator (run mvn to emit the fixture), then add a dispatch case +
`[InlineData]` in `GoldenFormulaFixtureTests.cs`. Remaining pure veins are thin (most StatFunctions read
live Creature/Player/config); next golden leverage is the deferred integration harness (live World/DB/conn)
for the ~210 object-reading SM_* packets — a sub-project, not a one-tick add. Build 0, golden 169, suite 456/0.

## GOLDEN SUITE 169 -> 175 (2026-06-17) — deterministic ctor-only packet vein ESSENTIALLY EXHAUSTED

Added 6 NEW deterministic ctor-only SM_* packet goldens via the existing mvn oracle
(`GoldenPacketFixtureGeneratorTest` batch 8 -> `parity-artifacts/golden/packets/*.json`, read by the
`FaithfulCsharpPayloadMatchesJavaGoldenFixture` theory + `ReconstructFaithful` switch in
`GoldenPacketFixtureTests.cs`):
- **SM_GM_BOOKMARK_ADD** (name, worldId, x, y, z) — writeS/writeD/writeF×3
- **SM_ALLIANCE_READY_CHECK** (playerObjectId, statusCode) — writeD/writeC
- **SM_BIND_POINT_INFO** (mapId, x, y, z) — the OBELISK ctor (bindPointType 0, kiskObjId 0); NOT the Kisk ctor
- **SM_CHAT_INIT** (byte[] token) — writeD(len)/writeB
- **SM_RECEIVE_BIDS** (int unk) — writeD
- **SM_CUSTOM_SETTINGS** (objectId, unk, display, deny) — the SCALAR ctor; NOT the Player ctor

ALL byte-exact on FIRST capture, **0 fidelity bugs** (more evidence the faithful SM_* packet pillar is correct).

**CRITICAL mvn GOTCHA (re-confirmed):** the Java poms set `maven.test.skip=true` globally (pom.xml:20), so the
generator is SILENTLY SKIPPED ("Not compiling test sources", BUILD SUCCESS, NO fixture emitted) unless you pass
**`-Dmaven.test.skip=false`**. Full cmd from repo root:
`mvn -pl game-server -am test -Dtest=GoldenPacketFixtureGeneratorTest -Dmaven.test.skip=false -Dsurefire.failIfNoSpecifiedTests=false`.

**DETERMINISTIC-PACKET SURVEY (precise):** of 240 faithful `SM_*.cs`, 130 were golden'd pre-tick. Of the 110
NOT-yet-golden'd, the overwhelming majority read live `Player/Creature/Kisk/Pet/Summon`, singletons
(`SiegeService/Influence/GameTimeService/TownService/Legion`), `DataManager` templates, `ItemInfoBlob`/
`EnchantInfoBlob`, or `System.currentTimeMillis()`/JVM-uptime in their ctor or writeImpl. After this tick the
EASY unit-golden'able ctor-only vein is **essentially exhausted**; the few stragglers are awkward:
- `SM_CUSTOM_PACKET` — builder pattern, no plain value ctor.
- `SM_LOGIN_QUEUE`, `SM_AFTER_SIEGE_LOCINFO_475` — no reachable public ctor (package-private/builder).
- `SM_TELEPORT_LOC` — looked clean but its CTOR calls `DataManager.WORLD_MAPS_DATA.GetTemplate(mapId).IsInstance()`
  (DataManager read) -> needs the integration harness, NOT unit-golden'able.
- `SM_TIME_CHECK` — reads JVM uptime in ctor -> non-deterministic.

**RECOMMENDED NEXT GOLDEN VEIN: the deferred INTEGRATION HARNESS (live World/DB/conn) for the ~110 object-reading
SM_* packets.** The unit-harness precedent already exists and is reusable: `GoldenStatsInfoFixtureTests` /
`GoldenPlayerInfoFixtureTests` (full-Player path: GameTime=0 DB-stub + raw-field-pinned PlayerCommonData + minimal
PLAYER_EXPERIENCE_TABLE fixture + HarnessPlayer running the real faithful Player base ctor with no World/Knownlist)
and the live-object `PacketHarnessCreature`/`PacketHarnessLifeStats` in `GoldenPacketFixtureTests.cs` (SM_PLAYER_STATE/
SM_TARGET_SELECTED/SM_EMOTION/SM_ATTACK_STATUS). Increment-1 candidates that need only a HarnessCreature/HarnessPlayer
(no new singleton stub): SM_PET LOAD/SPAWN branches (need DataManager.PET_DATA + live Pet), SM_SUMMON_UPDATE
(HarnessSummon w/ game-stats), SM_DIE (HarnessPlayer), SM_CUSTOM_SETTINGS Player-ctor / SM_BIND_POINT_INFO Kisk-ctor
(HarnessKisk w/ WorldPosition — PositionedHarness precedent). Build 0, golden 175/175, suite 462/0, bootstrap 9/9.

## GOLDEN SUITE 178 -> 182 (2026-06-17) — 4 more scalar-HarnessPlayer string/position packets (NO new substrate)

Mined 4 MORE Player-reading SM_* whose writeImpl reads ONLY the scalar HarnessPlayer state, reusing the SAME
seam (Java `generateGoldenPlayerStringPacketFixtures` @Test + `noteSpec()`; C#
`CsharpPlayerStringPacketMatchesJavaGoldenFixture` [Theory]). 6 fixture cases total across 4 packets.
**Byte-exact on FIRST capture, 0 fidelity bugs** (all 4 .cs faithful 1:1):

- **SM_UPDATE_NOTE**(player) [2 cases: non-empty + empty note]: writeD(`getObjectId()`) + writeS(`getCommonData().getNote()`).
  Note is a plain pinned PlayerCommonData string (pin via `noteSpec()` Java / `SetNote()` C#).
- **SM_GM_SEARCH**(player) [1 case]: writeS("search " + `getName()` + " " + `getWorldId()` + " " + (int)`getX/Y/Z()`).
  `getWorldId()`==`position.getMapId()`, coords from the pinned WorldPosition — all scalar.
- **SM_TRANSFORM_IN_SUMMON**(player, creatureObjectId) [1 case]: writeD(creatureObjectId) + writeS(`getName()`) +
  writeD(`getObjectId()`). The **int-ctor overload** avoids needing a live Creature.
- **SM_SHOW_NPC_ON_MAP**(player, npcid, worldid, x, y, z) [2 cases: same-map + other-map]: writeD(npcid)+writeD(worldid)+
  writeD(instanceId)+3xwriteF. **instanceId is derived purely from the scalar WorldPosition**: `getPosition().getMapId()`,
  `isInInstance()`==`position.isInstanceMap()`==false (no mapRegion), `getInstanceId()`==1 (no mapRegion). Same-map case
  exercises `worldid + getInstanceId()(==1) - 1 == worldid`; other-map keeps the default `instanceId = worldid`.

**SCALAR-HARNESS PLAYER VEIN now covers:** SM_PLAYER_STANCE/RIDE_ROBOT/PLASTIC_SURGERY/TARGET_UPDATE/ABYSS_RANK_UPDATE/
ABYSS_RANK/PLAYER_SEARCH/PLAYER_REGION/RENAME + **UPDATE_NOTE/GM_SEARCH/TRANSFORM_IN_SUMMON/SHOW_NPC_ON_MAP**.

**SCALAR VEIN STATUS — NEAR-EXHAUSTED.** Surveyed all remaining not-yet-golden'd SM_* (full list below). The pure
scalar-Player-only readers are now drained; every remaining Player-reading SM_* pulls in MORE state than the scalar seam
supplies. Confirmed exclusions (line-by-line):
- **SM_DIE** -> `getWorldMapInstance().getInstanceHandler()` (World/instance) + `getKisk()`.
- **SM_PLAYER_SPAWN** -> `World.getInstance().getWorldMap(...).getTemplate().getBeginnerTwinCount()` (World).
- **SM_GM_SHOW_PLAYER_STATUS** -> `getInventory().getLimit()/.size()` + dozens of `pgs.getPower()/getHealth()/...`
  (PlayerGameStats convenience getters the HarnessStats does NOT override -> real stat graph) + `pcd.getExpNeed()`/
  repose. Heavy; needs the full PlayerGameStats integration path, not the thin HarnessStats. NOT scalar.
- **SM_VIEW_PLAYER_DETAILS** -> `ItemInfoBlob.getFullBlob(player,item)` (item graph). NOT scalar.
- **SM_UPDATE_PLAYER_APPEARANCE** -> `writeEquippedItems(items)` (item graph; no Player at all). NOT scalar.
- **SM_GM_SHOW_PLAYER_SKILLS / SM_SKILL_LIST** -> skill graph. **SM_NPC_INFO/SM_MOVE/SM_OBJECT_USE_UPDATE** -> live
  Creature/Npc/World. **SM_SUMMON_UPDATE/SM_SUMMON_PANEL/SM_PET_EMOTE** -> Summon/Pet graph. **SM_LEGION_*** -> Legion.
  **SM_INVENTORY_*/SM_TRADELIST/SM_BROKER_SERVICE/SM_PRIVATE_STORE/SM_SELL_ITEM/SM_WAREHOUSE_*** -> item graph.
  **SM_TELEPORT_LOC/SM_SKILL_COOLDOWN** -> DataManager. **SM_TIME_CHECK/SM_ITEM_COOLDOWN/SM_GAME_TIME** -> time/singleton.

**RECOMMENDED NEXT VEIN: the deferred INTEGRATION-HARNESS sub-project** (the scalar vein is functionally exhausted).
Bounded increments, each unlocking a packet family:
1. **World/instance seam** (a live `World` with one `WorldMap`+`WorldMapInstance`+`InstanceHandler` resolvable for the
   harness Player's mapId) -> unlocks **SM_DIE, SM_PLAYER_SPAWN, SM_SHOW_NPC_ON_MAP-instance-branch, SM_TELEPORT_LOC**.
2. **Full PlayerGameStats seam** (drive the real stat graph, not the thin HarnessStats override) -> unlocks
   **SM_GM_SHOW_PLAYER_STATUS** and the remaining stat-heavy player packets.
3. **Item/ItemInfoBlob seam** (a live `Item` + equipment) -> unlocks **SM_VIEW_PLAYER_DETAILS,
   SM_UPDATE_PLAYER_APPEARANCE, SM_INVENTORY_*, SM_TRADELIST, SM_SELL_ITEM, SM_WAREHOUSE_***.
4. **Legion seam** -> **SM_LEGION_*** ; **Summon/Pet seam** -> **SM_SUMMON_*/SM_PET_*** ; **Skill-graph seam** ->
   **SM_SKILL_LIST/SM_GM_SHOW_PLAYER_SKILLS/SM_CASTSPELL_RESULT**.
Increment 1 (World/instance) is the highest-leverage next step. Build 0, golden 182, suite 469/0, bootstrap 9/9.

## GOLDEN SUITE 176 -> 178 (2026-06-17) — SM_PLAYER_REGION + SM_RENAME via the PROVEN scalar HarnessPlayer (NO new substrate)

Golden'd **SM_PLAYER_REGION** (2 cases: NONE / `ELYSEA_NORTH`) + **SM_RENAME**(player ctor, 1 case) — the FIRST
Player-reading SM_* family — by **REUSING the already-proven scalar HarnessPlayer seam** in
`GoldenPlayerInfoFixtureGeneratorTest` (Java `scalarSpec()`/`scalarCase()`/`HarnessPlayer`) + `GoldenPlayerInfoFixtureTests`
(C# `BuildScalarPlayer()`). **The previous section's "RECOMMENDED NEXT VEIN: minimal PacketHarnessPlayer" was solved the
EASY way: no new harness type was needed** — the full HarnessPlayer integration seam from
`GoldenStatsInfoFixtureTests`/`GoldenPlayerInfoFixtureTests` ALREADY runs the real faithful Player base ctor (with a DB-stub
swallowing the PetList SQLException) and pins objectId + commonData name, which is everything these two writeImpls read.
Added a new `generateGoldenPlayerZonePacketFixtures` @Test (Java) + `CsharpPlayerZonePacketMatchesJavaGoldenFixture`
[Theory] (C#).

- **SM_PLAYER_REGION**: writeD(`player.getObjectId()`) + 3x writeC(0) + writeD(`subZone.name().hashCode()`). subZone =
  `ZoneName.NONE` / `ZoneName.createOrGet("ELYSEA_NORTH")` (immutable upper-cased name, interned both sides). The wire int
  is Java's `String.hashCode`, reproduced C#-side by SM_PLAYER_REGION.cs's existing `JavaStringHashCode` helper
  (see [[java-string-hashcode-on-wire]]).
- **SM_RENAME**(player,oldName): writeD(0) writeD(0) writeD(`getObjectId()`) writeS(oldName) writeS(`getName()`).
  `getName()` == `getCommonData().getName()` == "Scalarharness" (pinned). No con.

**Byte-exact on FIRST capture, 0 fidelity bugs** (both .cs faithful 1:1). GOTCHA: the `generateGoldenPlayerInfoFixtures`
family logs `PlayerPetsDAO ... SQLException: harness stub: no database` to the mvn console — these are the EXPECTED
stub-swallowed DB errors (logged, caught, no-rows), NOT failures; `| tail` of the log shows the alarming stacktrace —
read the surefire `Tests run: 4, Failures: 0, Errors: 0` summary instead.

**SCALAR-HARNESS PLAYER VEIN now covers:** SM_PLAYER_STANCE/RIDE_ROBOT/PLASTIC_SURGERY/TARGET_UPDATE/ABYSS_RANK_UPDATE/
ABYSS_RANK/PLAYER_SEARCH/PLAYER_REGION/RENAME. **RECOMMENDED NEXT VEIN:** (1) scan the remaining SM_* for any whose
writeImpl reads ONLY the scalar HarnessPlayer state (objectId/name/level/race/position/abyssRank/gameStats/lifeStats) — a
cheap continuation; then (2) the real integration-harness sub-project for the World/instance-reading
(SM_DIE = `getWorldMapInstance().getInstanceHandler()`), Legion-reading (SM_RENAME-legion), and Summon/Pet/Skill-graph
readers. Build 0, golden 178, suite 465/0, bootstrap 9/9.

## GOLDEN SUITE 175 -> 176 (2026-06-17) — SM_ATTACK, last Creature-only reader; existing-harness object vein EXHAUSTED

Golden'd **SM_ATTACK** (1 fixture / 3 cases: normalHit / block / shieldProtect) — the FIRST non-trivial object-reading
SM_* beyond SM_PLAYER_STATE/SM_TARGET_SELECTED/SM_EMOTION/SM_ATTACK_STATUS, using **ONLY the existing
`PacketHarnessCreature` + `PacketHarnessLifeStats`** harness types (NO new substrate). writeImpl reads attacker/target
`getObjectId()`, BOTH `getLifeStats().getHpPercentage()`, `AttackTypeAnimation`/`AttackHandAnimation.getId()`, per-hit
`AttackResult` scalar getters, and `AttackStatus.getId()/isCounterSkill()` — no DataManager/singleton/time/Rnd. The
**target is a non-Player harness Creature**, so the two `instanceof Player` branches (criticalEffect skillId 8218 +
setLastCounterSkill) are never taken, and `criticalEffect=null` skips the Effect read + x/y/z write. Cases exercise the
status switch (NORMALHIT default->writeH(0), BLOCK->writeH(32)) and the shield-type switch (0/2 no-extra, 8 protect:
protectorId/protectedDamage/protectedSkillId). `AttackResult` is a plain value object (setShieldType OR-accumulates),
`AttackStatus`/`AttackTypeAnimation`/`AttackHandAnimation` are enums — all already ported 1:1, all constructible with no
substrate. **Byte-exact on FIRST capture, 0 fidelity bugs** (SM_ATTACK.cs is faithful 1:1). Java harness:
`HarnessAttackCreature` overriding `getLifeStats()` -> `HarnessAttackLifeStats` whose `getMaxHp()` returns fixture maxHp;
C# reuses `PacketHarnessCreature` w/ `{StatEnum.MAXHP=maxHp}` + `PacketHarnessLifeStats(currentHp,0)` (same path
SM_ATTACK_STATUS proved). GOTCHA: the Java `list.add(new Case(name,json,capture(new SM_FOO(...))))` needs **FOUR**
trailing `)` (add/Case/capture/SM_FOO) — a missing one yields a misleading javac `')' or ',' expected` at the LAST arg line.

**EXISTING-HARNESS OBJECT VEIN NOW EXHAUSTED.** SM_ATTACK was the LAST Creature-only (non-Player) reader cleanly
golden'able with existing harness types. Every other not-yet-golden'd SM_* reads a **Player** (SM_DIE / SM_RENAME-player /
SM_TRANSFORM_IN_SUMMON / SM_SHOW_NPC_ON_MAP / SM_PLAYER_REGION), a **Summon/Pet** w/ full gameStats (SM_SUMMON_UPDATE /
SM_SUMMON_PANEL / SM_PET_EMOTE), a **HouseObject/Legion** (SM_OBJECT_USE_UPDATE / SM_RENAME-legion), a **Skill/Effect**
graph (SM_CASTSPELL_RESULT), **DataManager** (SM_TELEPORT_LOC / SM_SKILL_COOLDOWN), or **System.currentTimeMillis**
(SM_ITEM_COOLDOWN) — all need NEW substrate.

**RECOMMENDED NEXT VEIN: a minimal `PacketHarnessPlayer` seam (smallest single increment).** SM_PLAYER_REGION is the
ideal first target — its writeImpl reads ONLY `player.getObjectId()` (stored at ctor) + `subZone.name().hashCode()`
(Java-string-hashcode-on-wire; SM_PLAYER_REGION.cs already has `JavaStringHashCode`). Needs only a Player whose
`getObjectId()` works — a `PacketHarnessPlayer : Player` mirroring `PacketHarnessCreature` (run the Player base ctor w/
null world/conn, override getLevel/getRace). ZoneName is interned both sides (`createOrGet`/`CreateOrGet`). If the Player
base ctor is too heavy to run bare (it pulls PlayerCommonData/appearance/skill-list/effect-controller), fall back to the
full HarnessPlayer integration seam already proven in `GoldenStatsInfoFixtureTests`/`GoldenPlayerInfoFixtureTests`
(GameTime=0 DB-stub + raw-field-pinned PlayerCommonData + minimal PLAYER_EXPERIENCE_TABLE) — that unlocks the whole
Player-reading SM_* family at once. Build 0, golden 176, suite 463/0, bootstrap 9/9.

## QUEST SCRIPT PORT — 1035/1035 COMPLETE (2026-06-17, commit 8fac65d3c)

The last 10 "deferred spawn-AI/flight" quests were ALL false-defers. The supposed blocker — "WalkManager /
flying-ring engine threaded into quest-tasks" — was a PHANTOM gap: every dep was already ported.
Verified present before porting: `WalkManager.StartWalking((NpcAI)npc.GetAi())`,
`QuestTasks.NewFollowingToTargetCheckTask` (ZoneName / 3-float / int-npcTargetId overloads),
`QuestEngine.RegisterOnPassFlyingRings` + `AbstractQuestHandler.OnPassFlyingRingEvent`, `SpawnInFrontOf`,
`TaskId.QUEST_FOLLOW` + `CreatureController.AddTask`, `SpawnTemplate.SetWalkerId`,
`AiEventType.FOLLOW_ME` + `OnCreatureEvent`, `SmEmotion(npc,EmotionType.CHANGE_SPEED,0,objId)`,
`DefaultFollowEndEvent`, `GetLifeStats().IncreaseFp(SmAttackStatus.TYPE.FP_RINGS,7,0,SmAttackStatus.LOG.REGULAR)`,
`SkillEngine.ApplyEffectDirectly`, `AIState.WALKING`/`SetStateIfNot`, `GetMoveController().MoveToTargetObject`,
`KnownList.FindObject`. The 10: flight-ring (_1044TestingFlightSkills, _1354PraticalAerobatics,
_2042TheLastCheckpoint), WalkManager-follow (_2333ARibbitOutOfWater, _2394ADyingWish,
_3212TheMissingCubeCraftsman, _4212MissingSidrunerk, _24053TheMaulingoftheMau, _2634TheDraupnirRedemption),
spawn-AI-walk (_14026ALoneDefense). Build 0, full suite 454/0, golden 167, bootstrap 9.
Gotchas: SkillEngine class self-shadows its namespace -> fully-qualify
`Aion.GameServer.SkillEngine.SkillEngine.GetInstance()`; `HandlerResult` is an ENUM -> use
`HandlerResultExtensions.FromBoolean(...)`; `SmAttackStatus.TYPE`/`.LOG` are nested types;
`WorldMapType.GetId()` is an extension method (needs `using Aion.GameServer.World`); `SM_DIALOG_WINDOW(objId,page)`;
`SM_NPC_INFO(Npc,Player)`. **CONTENT-HANDLER SCRIPT PORT NOW FULLY COMPLETE: quests 1035/1035 + AI 462/462 +
instance 37/37 + zone 3/3.** Next veins = runtime substrate pillars / golden-suite expansion / deferred
chat-command long tail (genuine engine-reflection blockers).

## ZONE-HANDLER PORT — COMPLETE (2026-06-17, commit cec2fa559) — 3/3

Java zone handlers live in `game-server/data/handlers/zone/*.java` = **3 total** (NOT a big set):
`_1012SensoryArea.java` (`: QuestZoneHandler`) + `pvpZones/PvPZone.java` (abstract `: AdvancedZoneHandler`) +
`pvpZones/PvPAreaZone.java` (`: PvPZone`). Base + registration was ALL already ported (same recipe as
instance/quest/AI): `ZoneNameAnnotation` attribute + `ZoneHandlerClassListener` (reflection scan) +
`ZoneService.AddZoneHandlerClass` wired in `ZoneService.Init()` via `ScriptManager.Load(WorldConfig.ZONE_HANDLER_DIRECTORY)`.
Bases present: `GeneralZoneHandler`, `QuestZoneHandler`, `AdvancedZoneHandler` (interface : IZoneHandler),
`IZoneHandler`. Ported into `dotnetConversion/src/Aion.GameServer/Handlers/Zone/` (+ `Zone/PvpZones/`).
All deps pre-present: `AbstractQuestZoneObserver` (override `OnMoved`, NOT onMoved), `PvPZoneInstance`,
`CustomConfig.KEEP_BUFFS_IN_COLISEUM`, `PlayerEffectController.SetKeepBuffsOnDie`, `PlayerReviveService.DuelRevive`
(ns Services.**Players**), `PacketSendUtility.BroadcastToZone`, the 4 PvP SM strings (STR_MSG_PvPZONE_MY_DEATH_TO_B/
HOSTILE_DEATH_TO_ME/HOSTILE_DEATH_TO_B + STR_PvPZONE_OUT_MESSAGE), `TaskId.TELEPORT`, `GetController().AddTask/
GetAndRemoveTask`, `ThreadPoolManager.Schedule(Action,long)`, `ZoneName.Get` (interned -> default ref `==` works).
**Gotchas:** QuestState ns = `Aion.GameServer.QuestEngine.Model` (CAPITAL E, file dir is `Questengine/`);
PlayerEffectController ns = `Controllers.Effects` (plural); `Player.GetEffectController()` does NOT override the
covariant return (returns base `EffectController`) so cast `(PlayerEffectController)player.GetEffectController()`
(Java relies on the covariant override; runtime type IS PlayerEffectController); Java anonymous
`AbstractQuestZoneObserver{ onMoved }` -> C# nested private sealed class capturing `questId` in its ctor (C# can't
extend anonymously). **ZONE-HANDLER SET: 3/3 COMPLETE**, build 0 / 454 / golden 167 / bootstrap 9.

## *ApRewardService STAND-INS RETIRED (2026-06-17, commit after cec2fa559) — category 3 closed

The 6 reworked `*ApRewardService` (PvpApRewardService/PvpInstanceApRewardService/PvpArenaApRewardService/
AturamSkyFortressApRewardService/EternalBastionApRewardService/StonespearReachApRewardService) are DELETED. Verified
dead-islands: each referenced ONLY by Program.cs DI + its own file (0 production/test consumers; every Result/Status
type self-contained, grep = 0 external). NONE has a Java counterpart class (invented Service+Result+Status blow-ups).
The faithful AP-reward logic now lives 1:1 in the ported instance handlers (Aturam/EternalBastion/Stonespear
onDie -> AbyssPointsService.AddAp); the 3 Pvp* ones had no faithful counterpart at all. Removed the 6 DI lines from
Program.cs (replaced with a retirement-note comment). Build 0 / 454 / golden 167 / bootstrap 9. **Capstone category 3
"instance-handler AP-reward reworked services" is now fully CLOSED** (the instance-handler frontier that gated it is
37/37 done).

## INSTANCE-HANDLER PORT — batch 1 (2026-06-16)

Java instance handlers live in `game-server/data/handlers/instance/*.java` = **37 total** (not ~78). Base
`GeneralInstanceHandler` + `[InstanceID(n)]` attribute + `InstanceHandlerClassListener` (reflection scan via
`InstanceEngine.AddInstanceHandlerClass`) are ALL already ported. Port shape = same as quest/AI scripts:
`public class XxxInstance : GeneralInstanceHandler { ctor base(instance); [InstanceID(n)]; override On*/Handle* }`.
Ported into `dotnetConversion/src/Aion.GameServer/Handlers/Instance/`.

**Batch 1 done (13 handlers, all green):** Haramel, FireTemple, AdmaStronghold, TheobomosLab, DraupnirCave,
KromedesTrial, PadmarashkasCave, DanuarSanctuary, DanuarMysticarium, DanuarReliquary (base) + DanuarReliquary_L
+ InfernalDanuarReliquary, LowerUdasTemple. **Running total 13/37.**

**Gotchas:** `SkillEngine.GetInstance()` collides with the `Aion.GameServer.SkillEngine` namespace when imported
— fully-qualify `Aion.GameServer.SkillEngine.SkillEngine.GetInstance()` (same for SpawnEngine, already qualified
in the base). `PlayerClass` needs `using Aion.GameServer.Model`. AtomicBoolean/AtomicInteger → `int` +
`Interlocked.CompareExchange`/`Increment` + `Volatile.Write` (per threadpool-async-idiom memory). `Future<?>` →
`ScheduledTask` (`IsCancelled` property, `Cancel(bool)`). AiEventType ns = `Aion.GameServer.Ai.Event`,
AbnormalState ns = `Aion.GameServer.SkillEngine.Effects`. Java arrow-switch → C# switch statement/expression.

**Batch 2 done (11 handlers, all green, commit 3b2221a2b):** OphidanBridge (+ _L subclass), SeizedDanuarSanctuary,
Beshmundir, InfinityShard, AturamSkyFortress, SauroSupplyBase, RentusBase, OccupiedRentusBase, RaksangRuins,
TalocsHollow. **Running total 24/37.** Made base `GeneralInstanceHandler.IsBoss(Npc)` `virtual` (Java has no
`final`; InfinityShard/Sauro/Rentus/OccupiedRentus override it) — interface-free, no other change. Batch-2
gotchas confirmed: `WalkManager.StartWalking((NpcAI)npc.GetAi())` (StartWalking returns bool, just call it; NpcAI
ns `Aion.GameServer.Ai`, manager ns `Aion.GameServer.Ai.Manager`); `SM_ATTACK_STATUS.TYPE.HP/MP` + `LOG.REGULAR`
→ `using TYPE = ...ServerPackets.SmAttackStatus.TYPE; using LOG = ...SmAttackStatus.LOG;` (class is `SmAttackStatus`
but SM_PLAY_MOVIE/SM_QUEST_ACTION/SM_EMOTION/SM_SYSTEM_MESSAGE keep SCREAMING names); `ItemService` ns is
`Aion.GameServer.Services.**Items**` (plural); `ZoneName.Get(...)` static + `zone.GetAreaTemplate().GetZoneName()` /
`zone.GetZoneTemplate().GetName()`; `Rnd.Get(int[])`/`NextInt`/`NextBoolean`/`NextFloat`; Java `scheduleAtFixedRate`
→ `ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ => {...; return ValueTask.CompletedTask;}, TimeSpan.Zero,
TimeSpan.FromMilliseconds(delay))` (returns ScheduledTask w/ Cancel); FlyRing via `new FlyRing(new FlyRingTemplate(
name, mapId, Point3D(double…)×3, radius), instance.GetInstanceId()).Spawn()`; `Math.toRadians` → `Math.PI/180.0 * x`;
`SummonsService.DoMode(SummonMode.RELEASE, summon, UnsummonType.UNSPECIFIED)`; `Item.GetItemId()` (Item =
`Model.GameObjects.Item`, base `IsRestrictedToInstance(Item)` override matches). Java arrow-switch w/ multi-int
labels → C# switch w/ explicit `break;` per group.

## INSTANCE-HANDLER PORT — batch 3 (2026-06-16) — +8 → 32/37

Ported: LinkgateFoundry (117), TiamatStrongHold (265), DarkPoeta (301), DragonLordsRefuge (348),
AnguishedDragonLordsRefuge (236, `: DragonLordsRefuge`), NightmareCircus (361), IlluminaryObelisk (395),
InfernalIlluminaryObelisk (161, `: IlluminaryObelisk`). All 1:1, all-green (build 0 / 454 / golden 167 / bootstrap 9).
DarkPoeta scoreboard surface was ALL pre-ported (DarkPoetaScore/DarkPoetaScoreWriter/SM_INSTANCE_SCORE/
InstanceProgressionType ext IsStartProgress·IsEndProgress / `(TemporaryPlayerTeam)instance.GetRegisteredTeam()` /
`player.GetAbyssRank().GetRank().GetId() >= AbyssRankEnum.STAR1_OFFICER.GetId()`). NightmareCircus/Obelisk used only
WalkManager + standard surface. **Bounded dep added** (only one needed): 3 IDTIAMAT countdown entries in
`SM_SYSTEM_MESSAGE.cs` (COUNTDOWN_START 1401547 / DRAKAN_ON_DIE 1401551 / COUNTDOWN_OVER 1401563) — the C# catalog is a
subset; ids copied verbatim from the Java oracle. **Batch-3 gotchas:** `PlayerReviveService` ns = `Services.Players`
(plural); `ScheduleAtFixedRate` returns `Task` — use **`ScheduleAtFixedRateTask`** for a cancellable `ScheduledTask`,
which has NO Action overload (pass `_ => {...; return ValueTask.CompletedTask;}` + `TimeSpan` args); `AIActions.UseSkill`/
`TargetCreature` need `(NpcAI)npc.GetAi()` cast (GetAi() returns non-generic `AbstractAI`; AIActions wants
`AbstractAI<T>`); `AtomicInteger.compareAndSet(exp,upd)` → `Interlocked.CompareExchange(ref, upd, exp) == exp`;
`System.currentTimeMillis()` → `DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()`; `Race.GetRaceId` ext is in ns
`Aion.GameServer.Model`; `instance.forEach` → `ForEachObject`; `.stream().allMatch` → `TrueForAll`; the protected
7-arg walker `Spawn(id,x,y,z,h,delay,walkerId)` overload is distinct from base 5/6-arg Spawn.

**Remaining 5 = the heavy-defers only:** ShugoImperialTomb (1329 lines), StonespearReach (925), EternalBastion (867),
DrakenspireDepths (593), TheShugoEmperorsVault (574). Each pulls a larger subsystem (AP-reward reworked services /
heavier scoreboard / siege / multi-stage). Recommend tackling EternalBastion or DrakenspireDepths next (smallest of
the five) after verifying its ApReward/InstanceScore surface; the other 4 are genuine heavy ports.

## INSTANCE-HANDLER PORT — batch 4 (2026-06-16) — +1 → 33/37

Ported the smallest heavy-defer: **TheShugoEmperorsVault (574 lines)** → `TheShugoEmperorsVaultInstance.cs`,
`[InstanceID(301400000)]`, commit e7cfbdf89. STRICT 1:1. **No bounded dep needed** — all deps were already present
and verified before porting: `NormalScore` (Model/Instance/Instancescore) + `TheShugoEmperorsVaultScoreWriter`
(Network/Aion/Instanceinfo) both pre-ported; all SM strings present (`STR_IDSweep_Stage2_End` 24055 /
`STR_MSG_GET_SCORE` / `STR_REBIRTH_MASSAGE_ME`); base `Spawn`/`SpawnAndSetRespawn`/`SendMsg`/`OnStartEffect`/
`OnReviveEvent`/`GetInstanceScore` all on `GeneralInstanceHandler`; `InstanceProgressionType.IsPreparing/IsStartProgress`;
`instance.SetDoorState/ForEachPlayer/ForEachNpc`; `SkillEngine.ApplyEffectDirectly`; `Rnd.Chance()/Get(int,int)`;
`TaskId.DESPAWN` + `controller.AddTask`; `ItemService.AddItem` (ns Services.**Items**); `PlayerReviveService.Revive`
(ns Services.**Players**); `TeleportService.TeleportTo/MoveToInstanceExit`. **Batch-4 gotchas:** `(byte) -29` negative
heading → C# needs `unchecked((byte)-29)` (CS0221 otherwise; same convention as LinkgateFoundry); `ScheduledTask.IsDone()`
is a **method** not a property; `schedule(r, 1, TimeUnit.MINUTES)` → `Schedule(r, 60000L)`; Java `synchronized` methods
→ per-method `private readonly object xLock = new(); lock(xLock){...}`; `Set<Integer> + ConcurrentHashMap.add()` →
`ConcurrentDictionary<int,byte>` + `TryAdd(k,0)`; `AtomicInteger stage` → `int` + `Interlocked.Increment` /
`Volatile.Read`. So TheShugoEmperorsVault was actually NOT a heavy-subsystem defer — its scoreboard/score-writer
pillar was already in place; it was just a large (574L) mechanical port.

## INSTANCE-HANDLER PORT — batch 5 (2026-06-16) — +1 → 34/37

Ported **DrakenspireDepths (593 lines)** → `DrakenspireDepthsInstance.cs`, `[InstanceID(301390000)]`, commit afb7dc119.
STRICT 1:1. **No bounded dep needed** — ANOTHER false-heavy-defer. This handler has **NO scoreboard at all** (no
ScoreWriter, no InstanceScore subtype, no AP-reward path) — it is pure staged-event timer/spawn logic. All deps
verified pre-present before porting: every `STR_MSG_IDSEAL_*` SM string already in the catalog (TWIN/IMMORTAL/WAVE/
WAVE_BONUS/GUARDIAN/VRITRA_HUMAN — used by a sibling); `WalkManager.StartWalking((NpcAI)npc.GetAi())`;
`RespawnService.ScheduleDecayTask(npc, 4000L)`; `npc.GetSpawn().GetStaticId()` + `SetWalkerId` on `SpawnTemplate`;
`SM_EMOTION(npc, EmotionType.CHANGE_SPEED)` + `PacketSendUtility.BroadcastPacket`; `Rnd.Get(min,max)`;
`instance.SetDoorState`; `Skill.UseSkill()` via `Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(...)`.
**Batch-5 gotchas:** `AtomicReference<Race>` → `Race? race` field + per-field `lock` for the compareAndSet-null-guard
in OnEnterInstance; the two `scheduleAtFixedRate(new Runnable(){ int count; run(){ switch(++count) }})` stateful
inner classes → captured local `int count = 0;` + `ScheduleAtFixedRateTask(_ => { switch(++count){...}; return
ValueTask.CompletedTask; }, TimeSpan.FromMilliseconds(initial), TimeSpan.FromMilliseconds(period))` (needs `using
System.Threading.Tasks;`); ScheduleAtFixedRateTask has **no Action overload** (must return ValueTask + TimeSpan args),
but plain `Schedule(()=>{...}, longMillis)` void-lambda binds the `Schedule(Action,long)` overload fine; `getAndSet`
→ `Interlocked.Exchange`; `compareAndSet(exp,upd)` → `Interlocked.CompareExchange(ref,upd,exp)==exp`. Confirms the
pattern: heavy-by-line-count ≠ heavy-by-subsystem.

## INSTANCE-HANDLER PORT — batch 6 (2026-06-16) — +1 → 35/37

Ported **EternalBastion (867 lines)** → `EternalBastionInstance.cs`, `[InstanceID(300540000)]`, commit da0d6ff53.
STRICT 1:1. **No bounded dep needed** — ANOTHER false-heavy-defer. This handler DOES have a scoreboard (NormalScore +
EternalBastionScoreWriter + InstanceScore base), but ALL of it was pre-ported: `NormalScore` (full points/rank/AP +
4 reward item/count pairs), `EternalBastionScoreWriter : InstanceScoreWriter<NormalScore>`, `InstanceScore.IsRewarded/
Set+GetInstanceProgressionType`, `InstanceProgressionType` (PREPARING/START_PROGRESS/END_PROGRESS), all 15
`STR_MSG_IDLDF5b_TD_*` SM strings (MainWave_01-06/AddWave_01-03/Notice_02/04/06), `STR_MSG_GET_SCORE(l10n,points)`,
`SM_INSTANCE_SCORE(mapId, writer, time)`, every service (`AbyssPointsService.AddAp`, `ItemService.AddItem`,
`PlayerReviveService.Revive`, `TeleportService.MoveToInstanceExit`+`TeleportTo`), `Rnd.NextBoolean`,
`Point3D(x,y,z)`+GetX/Y/Z, `instance.ForEachDoor/ForEachNpc/ForEachPlayer/GetPlayersInside`,
`door.SetOpen`, `npc.GetObjectTemplate().GetL10n()`, `Spawn(...).GetSpawn().SetWalkerId(w)`. Pure mechanical 1:1.
**Batch-6 gotchas:** ItemService ns = `Services.Items`, PlayerReviveService ns = `Services.Players` (recurring traps);
`log.LogInformation(...)` needs `using Microsoft.Extensions.Logging;` (base `log` is ILogger, the named-placeholder
overload is an extension method — no prior instance handler had used it so the using was not transitively present);
`ScheduleAtFixedRateTask` has only Runnable(interface — can't `new`) + `Func<CT,ValueTask>` overloads, so use
`_=>{SpawnAssaultWave();return ValueTask.CompletedTask;}`+two `TimeSpan` args; AtomicInteger reads inside `if`
conditions → `Volatile.Read(ref field)`, `.addAndGet(-2)`→`Interlocked.Add(ref,-2)`, `.decrementAndGet`/`.incrementAndGet`
→`Interlocked.Decrement/Increment`; `AtomicBoolean.compareAndSet(false,true)`→`Interlocked.CompareExchange(ref
isRaceSet,1,0)==0`. Confirms again: heavy-by-line-count ≠ heavy-by-subsystem.

## INSTANCE-HANDLER PORT — batch 7 (2026-06-17) — +1 → 36/37

Ported **StonespearReach (925 lines)** → `StonespearReachInstance.cs`, `[InstanceID(301500000)]`, commit b24029fdc.
STRICT 1:1. **No bounded dep needed** — ANOTHER false-heavy-defer (Legion Dominion siege defense, sibling of
EternalBastion/IlluminaryObelisk). Scoreboard surface ALL pre-ported: `LegionDominionScore` (points/rank/finalGP/finalAP +
4 reward item/count pairs), `LegionDominionScoreWriter : InstanceScoreWriter<LegionDominionScore>`, `InstanceScore`
base (IsRewarded/IsStartProgress/IsPreparing/Set+GetInstanceProgressionType/Clear), `LegionDominionService.GetInstance()
.OnFinishInstance(legion,points,time)`, ALL SM strings (`STR_MSG_GET_SCORE`, `STR_MSG_OBJ_Start/_Bomb/_Bomb_Die`,
`STR_MSG_LEGION_DOMINION_MOVE_BIRTHAREA_FRIENDLY(name)`, `STR_MSG_CANT_INSTANCE_TOO_MANY_MEMBERS(num,mapId)`), every
service (`ItemService.AddItem`, `GloryPointsService.AddGp`+`Rates.GP.CalcResult`, `AbyssPointsService.AddAp`,
`PlayerReviveService.Revive`, `TeleportService.TeleportTo`(instance + worldId overloads)+`MoveToBindLocation`),
`Legion.GetCurrentLegionDominion/GetLegionId`, `WorldPosition(mapId,x,y,z,h)`+GetX/Y/Z/Heading, `PositionUtil.GetDistance`,
`Rnd.Get(min,max)`/`Rnd.NextFloat(bound)`, `instance.ForEachPlayer/ForEachNpc/GetPlayersInside/GetNpc/GetMapId`.
**Batch-7 gotchas:** GloryPointsService AND AbyssPointsService each exist in BOTH `Services` and `Services.Abyss`
(CS0104 ambiguous) — Java imports `services.abyss.*` so fully-qualify `Aion.GameServer.Services.Abyss.GloryPointsService`/
`...AbyssPointsService` (LegionDominionService is the plain `Services` ns); Java `synchronized checkRank/canEnter` →
per-instance `lock(@lock)` field (a real field, NOT `new object()` each call); `Collections.shuffle` → manual Fisher-Yates
with `Rnd.Get(0,i)`; `IntStream.range(min,max+1)` → `Enumerable.Range(min, max+1-min)`; `Math.toRadians(d)` → `d*Math.PI/180.0`;
`Future`→`ScheduledTask`, `.isCancelled()`→`.IsCancelled` (property), `.cancel(b)`→`Cancel(b)`; `startTime` `long?` →
use `.Value` in the subtraction; `points.get(i)`→`points[i]`. Confirms again: heavy-by-line-count ≠ heavy-by-subsystem.

## INSTANCE-HANDLER PORT — batch 8 (2026-06-17) — +1 → 37/37 — SET COMPLETE

Ported **ShugoImperialTomb (1329 lines)** → `ShugoImperialTombInstance.cs`, `[InstanceID(300560000)]`, commit d2e7aab80.
STRICT 1:1. **No bounded dep needed** — the LAST instance handler, and an 8th-of-8 false-heavy-defer. 3-stage
tower-defense (Crown Prince / Empress / Emperor zones), each stage a PHASE_1 wave sequence → boss → PHASE_2 finale,
plus bonus stages, exit portals and ~150 relic chests. **NO scoreboard at all** (no ScoreWriter/InstanceScore subtype) —
pure staged spawn-wave timer logic. ALL deps pre-present: every `STR_IDEVENT01_*` string (`_S1_START/_S2_START/_S3_START`,
`_PHASE/_PHASE02/_PHASE03/_PHASE04/_PHASE09/_PHASE10`) already in the catalog; `WalkManager.StartWalking((NpcAI)npc.GetAi())`,
`CreatureState.ACTIVE/WALK_MODE`, `Creature.SetState(state,bool)`, `SM_EMOTION(npc,EmotionType.CHANGE_SPEED,0,objId)`,
`SkillEngine.ApplyEffectDirectly(skillId,Creature,Creature)`, `TeleportService.MoveToInstanceExit`, base
`Spawn`/`DeleteAliveNpcs`/`SendMsg`/`mapId`. **Batch-8 gotchas (all anticipated):** `SkillEngine.GetInstance()` collides
with the `Aion.GameServer.SkillEngine` ns → fully-qualify `Aion.GameServer.SkillEngine.SkillEngine.GetInstance()`;
`AtomicInteger stage.compareAndSet(exp,upd)` → `if (Interlocked.CompareExchange(ref stage,upd,exp) != exp) return;`
(early-return predicate INVERTED vs Java `if (!cas) return;`); `stage.get()` in the transformation switch →
`Volatile.Read(ref stage)`; `Future`→`ScheduledTask`, `.isDone()`→`.IsDone()` (METHOD), `.cancel(true)`→`Cancel(true)`;
the `sp()` walker helper = `(Npc)Spawn(...)` cast + `GetSpawn().SetWalkerId(w)` + `WalkManager.StartWalking((NpcAI)npc.GetAi())`
+ ACTIVE-vs-WALK_MODE state + CHANGE_SPEED emotion broadcast. Confirms the rule one final time: heavy-by-line-count ≠
heavy-by-subsystem.

**INSTANCE-HANDLER SET: 37/37 COMPLETE.** All handlers in `game-server/data/handlers/instance/*.java` ported 1:1 & green
(build 0 / 454 / golden 167 / bootstrap 9). The standing "instance-handler AP-reward reworked services" slop category
(category 3 below) is now fully unblocked — those reworked `*ApRewardService` stand-ins can be retired in favor of the
faithful handlers. **Next recommended vein:** zone handlers in `game-server/data/handlers/zone/` (apply the same
extend-base + auto-register recipe; grep PascalCase + check base classes to avoid false-defers), OR the deferred ~10
spawn-AI/flight quest scripts (need WalkManager/flying-ring threaded into quest tasks), OR golden-suite expansion to
cover instance-handler runtime behavior. See content-handler-scope memory for the running tally.

## CAPSTONE FIDELITY RE-SURVEY — 2026-06-16 (commit e3e1b1184)

Final comprehensive read-only sweep of the whole src tree across the 6 slop/silent-gap categories. Two real
bounded null-config bugs found + fixed (faithful, all-green); the rest confirmed clean OR scoped as the known
LARGER porting frontier (instance/quest/AI handlers). Build 0, full suite 454/0, golden 167/167, bootstrap 9/9.

### Survey results by category
1. **Hollow DataManager holders** — CLEAN. Zero `= new()` / `=> new()` static holders. All ~120 `*_DATA`
   accessors delegate to the live `StaticData` instance (`SD.*`) bound at boot via RegisterInstance. The 13
   wired holders confirmed still wired; no straggler.
2. **Reworked `*Summary`/`*Table` parallel projections** — CLEAN. The retired shadows (NpcTemplateSummary/
   SkillTemplateSummary/ItemTemplateSummary/Tempering/CustomNpcDrop/Housing/NpcSpawn) are gone (grep = 0). The
   remaining ~50 `*Summary` records all live inside `*Table` types that StaticData actually builds + exposes
   (the faithful cache-deserialized loader model, model A) — consumed, not dead-island shadows.
3. **Invented `*Service` micro-fragments** — 14 services lack an exact `<Name>.java`. Of these: 4 are legit
   idiomatic infra (GameServerBootstrapService/GameServerHostedService/OutboundLinkHostedService/
   StaticDataService — allowed per the infra-idiomatic principle). The other 10 are a REWORKED-SUBSTITUTE
   cluster standing in for UNPORTED faithful instance handlers — SCOPED as LARGER below (NOT bounded deletes:
   they're DI-live in Program.cs and deleting them without porting the handler leaves a gap).
4. **Reworked `Sm*` shadowing faithful `SM_*`** — the Npc/House/Loot/Kisk/Rift shadows are retired (grep = 0).
   ONE pair survives: `SmPet`/`SmPetEmote` (snapshot-DTO reworked, NotSupportedException stubs) shadow the
   faithful `SM_PET`/`SM_PET_EMOTE` (17 production consumers). SCOPED below (tied to a design-scaffold test +
   Phase-6 design doc; not a clean 0-consumer delete).
5. **Null Config statics / boot-init** — **2 REAL BUGS FOUND + FIXED** (commit e3e1b1184):
   `MembershipConfig.MEMBERSHIP_TYPES` (null -> NRE on the LIVE enter-world path in PlayerEnterWorldService for
   any membership>0 account; Java loads `{"Premium"}` from membership.properties) and
   `HousingConfig.HOUSE_AUCTION_REGISTER_DAYS` (null -> NRE in HousingBidService `[0]`/`[1]`; Java loads `{1,5}`
   from housing.properties). Both initialized as field initializers to the shipped property-file values
   (faithful, no invented values). All CronExpression statics confirmed initialized (incl. the prior
   PVP_MAP_RANDOM_BOSS_SCHEDULE fix); `ShutdownConfig.RESTART_SCHEDULE = null` is FAITHFUL (Java @Property has
   no default + empty properties => null, and Java's ShutdownHook null-guards it).
6. **Production NotImplemented/TODO in gameplay paths** — CLEAN. All ~35 NotSupportedException are faithful 1:1
   of Java UnsupportedOperationException (LegionWarehouse-behind-proxy, SiegeService cron-convert, InstanceService
   invalid-call, EffectTemplate unhandled-hoptype, etc.). All ~30 TODO/FIXME comments are verbatim carry-overs of
   TODOs in the Java source (correct fidelity, not invented stubs). The only non-faithful stubs are the SmPet
   shadow's (scoped in #4).

### Bounded fix applied
- **commit e3e1b1184** — category 5, the two null-config NREs above. Build 0, full suite 454/0, golden 167/167,
  bootstrap 9/9. The MEMBERSHIP_TYPES fix in particular removes a latent crash directly on the Front-A
  enter-world frontier (any premium account would have NRE'd on enter-world).

### LARGER items scoped (NOT forced — each is the documented handler-porting frontier, not a bounded slop delete)
- **Instance-handler AP-reward reworked services (category 3).** 6 `*ApRewardService`
  (Aturam/EternalBastion/Stonespear + Pvp/PvpArena/PvpInstance) + the timing/scheduler/registration services are
  DI-registered reworked stand-ins for UNPORTED faithful instance handlers. Java has 78 instance handlers under
  game-server/data/handlers/instance; only 8 are ported in C#. E.g. AturamSkyFortressApRewardService is a
  Service+Result-record+Status-enum blow-up of AturamSkyFortressInstance.onDie's 2-line `AbyssPointsService.addAp
  (player, 540)`. FAITHFUL RESOLUTION = port the instance handlers 1:1 (extend the faithful instance-handler base,
  override onDie/onEnterInstance) and retire the services — same family/effort class as the 1,035 quest + 503 AI
  script port (memory: content-handler-scope). NOT a bounded all-green delete.
- **SmPet/SmPetEmote reworked shadow + its design-scaffold test (category 4).** `SmPet.cs`/`SmPetEmote.cs` are a
  reworked snapshot-DTO pet-packet design with NotSupportedException stubs; production uses the faithful
  `SM_PET`/`SM_PET_EMOTE` (17 consumers, golden-tested). The ONLY consumer of the shadow is
  `PetJavaVectorArtifactReaderTests` — a documented Phase-6 design scaffold (docs/Phase-6-BindPointTeleport-
  KnownListPetGoldenVectorDesign.md; the test self-reports "Java known-list pet vector artifacts are not present
  yet"). FAITHFUL RESOLUTION = either complete the Phase-6 known-list-pet golden-vector work against the faithful
  SM_PET (preferred), or retire the shadow + scaffold test together. Held (don't discard documented in-progress
  design work as a "clean delete").

### DEFINITIVE VERDICT
The autonomous IN-MEMORY + DB-ctor fidelity arc is **COMPLETE** for the slop-retirement / hollow-holder /
boot-init / null-config frontier. After this capstone sweep: hollow holders 0, shadow-Summary/Table 0, live
shadow-Sm packets reduced to 1 scaffold-only pair, invented infra services are either legit-idiomatic or the
known handler-port frontier, null-config boot bugs 0 (2 last ones fixed here). The remaining work is NOT
"slop to retire" — it is two well-bounded categories of GENUINE PORTING (78-8=70 instance handlers; the
Phase-6 pet golden-vector design) plus the user-environment-gated **Front-A real-client enter-world test**.
The MEMBERSHIP_TYPES fix notably de-risks that Front-A test (it was a guaranteed enter-world NRE for premium
accounts).

### Honest final fidelity assessment
The boot/data/config/packet-base spine is faithful and green end-to-end (DB-backed full boot validated, golden
parity 167/167, 454/0 suite). The HONEST gap is gameplay BREADTH, not boot fidelity: ~70 instance handlers and
the long tail of content handlers remain to port (consistent with the parity-state memory's ~15-25%
full-gameplay estimate). Nothing invented or orphaned was left behind by this sweep; the two fixes are strict
property-file-faithful. There is no remaining un-blocked autonomous *slop/config* work — the next moves are
deliberate content-handler porting batches (instance/quest/AI) and the environment-gated client test.

## RESOLVED 2026-06-16 — full-suite test-isolation (one-process `dotnet test` now 454/0)

The suite passed per-class but flaked 2-4 tests in a single process. Three failures, all diagnosed + fixed
test-infra-only (no production hack, no weakened assertion):
- **GoldenStatsInfoFixtureTests.CsharpStatsInfoMatchesJavaGoldenFixture** (SM_STATS_INFO byte#4 = game-time D
  = 0x2D vs Java 0) — POLLUTION. `GameServerBootstrapTests` constructs a `GameTimeService` (ctor unconditionally
  sets the `_instance` singleton) and advances it to a non-zero game-minute (`WaitUntilAsync(GameMinutes>0)`),
  racing the golden packet fixtures that read `GameTimeService.GetInstance().GetGameTime().GetTime()` and assert
  time 0. FIX: (1) added `[Collection("GoldenDataManager")]` to GameServerBootstrapTests (serialize, no parallel
  race); (2) the 3 golden fixtures' `EnsureGameTimeSingleton` now ALWAYS reconstructs a 0-minute instance instead
  of skipping when one already exists, so it resets the singleton if a serialized sibling left it advanced.
- **JaxbHolderLoaderTests.LoadFromFile_PopulatesWorldMapsDataFromRealXml** (twin counts 1/0 vs expected 5/6) —
  REAL STALE EXPECTATION (failed isolated too). Stale vs the 2026-06-16 faithful twin-clamp fix (commit 4e0e872a7).
  The test was asserting the clamping accessors `GetTwinCount()`/`GetBeginnerTwinCount()`; it actually verifies XML
  binding, so it now asserts the raw deserialized fields `TwinCount`/`BeginnerTwinCount` (5/6), independent of the
  mutable `WorldConfig` statics.
- **GameServerOptionsTests.LoadDatabaseOptionsFromJavaConfig** (port 3306 vs 3307) — REAL STALE EXPECTATION
  (failed isolated too). `mygs.properties` (loaded last, Java mygs-override-wins parity) points the DB at the local
  Docker MySQL on 3307. Faithful behavior; test expectation corrected 3306 -> 3307.

## EMPIRICAL — DB-backed full-boot smoke RUN against the live MySQL container (2026-06-16)

The prior read-only static analysis (sections below) is now CONFIRMED AT RUNTIME. New opt-in env-gated test
`GameServerBootstrapTests.GameServerBootstrap_DbBackedFullBoot_RunsRealStartAsyncAgainstLiveMySql` (early-returns
unless `AION_GAMESERVER_DB_INTEGRATION=1`, mirroring SystemMailRepositoryDatabaseIntegrationTests' DatabaseFactory/
schema setup): points DatabaseFactory at 3307/aion_gs (root/aion), applies the real `game-server/sql/aion_gs.sql`
schema, loads the REAL DataManager via `DataManager.LoadAsync(repoRoot)` (147 MB cache + game-server/data), inits
AIEngine/ZoneService/GeoService (the spawn-critical engines, as the spawn-backed test does), and runs the FULL
`GameServerBootstrapService.StartAsync` via a pass-through `IStaticDataLoader` + the real `MySqlUsedIdRepository`.

### CORRECTED VERDICT (2026-06-16, root-cause re-investigation): VERDICT (a) — the house-twin throw was a REAL C#
DIVERGENCE, now FIXED. The earlier "Java-latent" conclusion (below) was WRONG: it traced spawnAll/spawnHouses/
storeObject faithfully but MISSED the twin-count clamp. Java's `WorldMapTemplate.getBeginnerTwinCount()` /
`getTwinCount()` (WorldMapTemplate.java:96-108) clamp the raw XML attributes by `WorldConfig`:
- `WORLD_MAX_TWINS_BEGINNER` default **-1** (disabled) => `getBeginnerTwinCount()` returns **0** (NOT the raw 3).
- `WORLD_MAX_TWINS_USUAL` default **1** => `getTwinCount()` = min(1, 0) = 0 => WorldMap defaults to 1.
So Java's `getInstanceCount()` for Heiron/Beluslan = **1**, not 4 — Java pre-creates ONE instance, SpawnHouses runs
ONCE, NO collision. **The C# `WorldMapTemplate.GetTwinCount()/GetBeginnerTwinCount()` returned the RAW XML values
(0 and 3) — skipping the WorldConfig clamp** (a TODO-backlog stub left from before WorldConfig was ported), giving
instanceCount=4 and the twin re-spawn collision. FIX: ported the two clamp methods 1:1 to Java (commit below).
RESULT after fix: the DB-backed full boot completes CLEANLY (IsStarted, world populated, StopAsync clean).

### (superseded) ORIGINAL RESULT: THROWS — at SpawnEngine.SpawnAll() -> HousingService.SpawnHouses() ->
World.StoreObject, a house-twin `DuplicateAionObjectException`:
- **Heiron (mapId 210040000)**: House `HOUSE_6001` objectId **130885** re-spawned into a 2nd twin instance.
- **Beluslan (mapId 220040000)**: House `HOUSE_7001` objectId **152343** re-spawned into a 2nd twin instance.
This was the symptom of the missing twin-count clamp (instanceCount over-counted 4 vs Java's 1), NOT Java-latent.

### #2 Housing no-DB deferral is EMPIRICALLY LIFTED. With the live (empty) players table, `PlayerDAO.GetUsedIDs()`
returned `int[0]` (not null), so the HousingService ctor's `RevokeOwnershipOfDeletedPlayers` did NOT throw
ArgumentNullException — the boot reached deep into SpawnAll and HousingService loaded + began SpawnHouses cleanly.
This proves the ArgumentNullException deferral was purely a no-DB artifact, NOT a port defect. The HousingService
`GetInstance()` block in StartAsync STAYS commented out anyway, because (a) enabling it does not change the SpawnAll
house-twin boundary, and (b) the no-DB bootstrap fixture (empty WORLD_MAPS_DATA => SpawnAll iterates zero maps =>
HousingService never reached) must stay green. The deferral comment in GameServerBootstrapService.cs:152-175 was
updated to record this empirical finding.

### Test disposition (faithful, not faked): the test captures StartAsync's outcome and, on throw, asserts the
flattened exception chain contains the `DuplicateAionObjectException` (the documented house-twin boundary) — NOT an
unrelated DAO/NRE regression. If StartAsync ever boots clean (e.g. seeded non-twin world_maps), the test asserts
IsStarted + world populated + StopAsync. Green either way; faithfully asserts the documented-throw boundary today.

### Genuine remaining frontier (post-empirical): the in-memory + DB-ctor floors are cleared. What remains is purely
environment/spawn-harness gated, NOT porting gaps:
1. **Whole-world clean SpawnAll** requires either seeding a non-twin world_maps subset OR mirroring the faithful
   house-twin throw — there is NOTHING to fix (Java throws identically). To exercise SpawnAll past housing in a
   single-map deterministic way, the spawn-backed test (SpawnObject per Sanctum template) already does this green.
2. **#1 SiegeService.initSieges() + #5 PvpMapService.init()** — RESOLVED 2026-06-16 (see top section below).
   Both now exercised+asserted in the DB-backed full-boot test and proven clean against real data; faithfully
   kept OUT of the always-on minimal-fixture StartAsync path (Java does not guard for empty data). One real
   port defect fixed en route (null CronExpression default). Boot tail is now CLOSED.
3. **Front-A real client -> enter-world** (memory three-server-stack-boots): needs the running server process +
   populated DB, same class of environment-gated work, not more porting. THIS IS NOW THE SOLE REMAINING FRONTIER.

## RESOLVED — boot tail CLOSED: SiegeService.initSieges() + PvpMapService.init() wired faithfully DB-gated (2026-06-16)

The two final deferred GameServer.main wires are now closed.

### Java guard analysis (source of truth)
- **SiegeService.initSieges()** (SiegeService.java:99-101): `if (!isInitialized.compareAndSet(false,true) ||
  !SiegeConfig.SIEGE_ENABLED) return;`. SIEGE_ENABLED defaults **true** (siege.properties
  `gameserver.siege.enable = true`), so the guard does NOT no-op — the full body runs and REQUIRES populated
  SIEGE_LOCATION_DATA. updateFortressNextState() does `getSiegeLocation(id).setNextState(...)` with NO null guard.
- **PvpMapService.init()** (PvpMapService.java:27-30): NO guard at all — not even PVP_MAP_ENABLED (which defaults
  false) is checked. Unconditionally calls `InstanceService.getNextAvailableInstance(301220000, ...)` which
  REQUIRES world map 301220000 to exist. So it runs always and needs real WORLD_MAPS_DATA.

### Disposition: both kept OUT of the always-on StartAsync, exercised+asserted in the DB-backed test (faithful)
Neither Java path guards for empty/disabled data, so wiring them unconditionally in the StartAsync used by the
minimal no-DB fixture (empty SIEGE_LOCATION_DATA / empty WORLD_MAPS_DATA) would NRE the 9/9 bootstrap gate. The
HARD RULE forbids inventing a C# guard Java lacks. Faithful resolution: exercise+assert them in
`GameServerBootstrap_DbBackedFullBoot_*` (real data) right after the clean StartAsync, with CWD pinned to
game-server so siege_schedule.xml's relative path resolves. Both now run CLEAN against live data; test asserts no
throw + PvpMapService handler registered (GetParticipantsSize()==0 live-handler path). The deferral comments in
GameServerBootstrapService.cs were rewritten to "FAITHFUL DB-GATED" with the line-ref evidence.

### Real port defect surfaced + fixed (1:1): null CronExpression default
PvpMapService.Init() -> PvpMapHandler.OnInstanceCreate() -> StartRandomBossTask() schedules off
`CustomConfig.PVP_MAP_RANDOM_BOSS_SCHEDULE`, which was left **null** in C# (CustomConfig.cs) — so
CronService.Schedule NRE'd on `cronExpression.CronExpressionString`. Java declares it with @Property defaultValue
`"0 30 14,18,21 ? * *"` (CustomConfig.java:264). Fixed faithfully by initializing the field inline via
`CronExpressions.GetOrCreate("0 30 14,18,21 ? * *")` — the same default-init pattern AutoGroupConfig uses for its
CronExpression[] fields. SiegeService.InitSieges() itself needed no fix (ran clean against real data first try).

### Green gate after the change: build 0, Golden 167/167, Bootstrap 9/9 (minimal stays green), RealStaticDataLoad 1/1, DbBackedFullBoot 1/1.

## RESOLVED — house-twin-spawn question: VERDICT (c) GENUINE JAVA LATENT BUG, C# mirrors faithfully, NO CODE CHANGE (read-only analysis, 2026-06-16)

QUESTION: full-world SpawnEngine.SpawnAll re-spawns the same address-cached House objectId into each of a
map's getInstanceCount() twin instances -> DuplicateAionObjectException. Is this a real Java latent bug (mirror
it), or does Java avoid it (per-instance objectIds / instanceCount==1 for housing maps / spawnHouses keys off
instanceId)?

### VERDICT: (c) — Java genuinely double-spawns the SAME House object across twin instances and has NO guard.
C# is a byte-for-byte faithful mirror. **NO fix applied** (the hard rule forbids inventing a guard Java lacks).
NOT case (a) (Java does NOT mint a fresh objectId per twin — it reuses the cached House), NOT case (b) (housing
maps DO have getInstanceCount() > 1 — twins happen).

### Java evidence (line refs)
- **SpawnEngine.spawnAll** (game-server SpawnEngine.java:119-126): `worldMap.forEach(instance -> spawnInstance(
  instance, (byte)0, instance.getOwnerId()))` — iterates ALL instances (WorldMap implements Iterable over its
  `instances` map, which holds getInstanceCount() entries created in the WorldMap ctor :31-36). Guarded only by
  `if (!worldMap.isInstanceType())`.
- **spawnInstance** (SpawnEngine.java:187-188): `if (eventTemplate == null) HousingService.getInstance().
  spawnHouses(instance, ownerId);` — called once PER instance, with ownerId==0 at boot (so spawnHouses takes the
  customHouses branch, not spawnStudio).
- **HousingService.spawnHouses** (HousingService.java:149-175): for each HouseAddress on the map,
  `House customHouse = customHouses.get(address.getId());` — **the cache is keyed by address.getId(), NOT by
  instanceId**. First instance: customHouse==null -> `new House(address, instanceId)` (ONE IDFactory objectId,
  House.java:59-60 `this(IDFactory.getInstance().nextId(), ...)`) -> stored in customHouses. Subsequent twin
  instances: `customHouses.get(address.getId())` returns the SAME House -> `customHouse.setPosition(...)` (new
  instance position) -> `SpawnEngine.bringIntoWorld(customHouse)` re-stores the SAME objectId.
- **bringIntoWorld** (SpawnEngine.java:108-114) -> **World.storeObject** (World.java:75-82):
  `allObjects.putIfAbsent(object.getObjectId(), object); if (oldObject != null) throw new
  DuplicateAionObjectException(...)`. **NO `isInWorld(objId)` guard, no try/catch, no per-instance keying.** So on
  instance #2 of a housing map, Java throws DuplicateAionObjectException — identically to C#.
- **WorldMap.getInstanceCount** (WorldMap.java:125-131): `twinCount = twin_count; if (0) ->1; twinCount +=
  beginner_twin_count; return twinCount`.

### Housing-map instanceCount values (game-server/data/static_data/world_maps.xml + housing/houses.xml)
houses.xml carries non-studio addresses on exactly 8 maps. Their world_maps.xml twin config + computed
getInstanceCount():
| map | name | twin_count | beginner_twin_count | instance? | getInstanceCount() | #addresses |
|-----|------|-----------|--------------------|-----------|--------------------|-----------|
| 210040000 | Heiron   | (none) | **3** | no  | **4** | 9 |
| 220040000 | Beluslan | (none) | **3** | no  | **4** | 9 |
| 210050000 | Inggison   | (none) | (none) | no | 1 | (addr present) |
| 220070000 | Gelkmaros  | (none) | (none) | no | 1 | (addr present) |
| 700010000 | Oriel (land)  | (none) | (none) | no | 1 | many |
| 710010000 | Pernon (land) | (none) | (none) | no | 1 | many |
| 720010000 | Oriel (personal)  | — | — | **instance=true** | (skipped by !isInstanceType) | — |
| 730010000 | Pernon (personal) | — | — | **instance=true** | (skipped) | — |

=> **Heiron (210040000) and Beluslan (220040000) are the trigger maps**: getInstanceCount()==4, NOT instance-type,
9 cached Houses each. SpawnAll spawns instance #1 fine (9 Houses, 9 fresh objectIds), then DuplicateAionObjectException
on instance #2. The note in the prior section's (a) claiming Heiron=4 is now CONFIRMED exact, and the trigger is
the `beginner_twin_count="3"` (+1 base) = 4 instances, not `twin_count`.

### C# parity audit (confirms faithful mirror, no divergence to fix)
- HousingService.cs:151-176 SpawnHouses — identical: `customHouses.GetValueOrDefault(address.GetId())` (address-
  keyed cache), create-if-null with `new House(address, instance.GetInstanceId())`, `SetPosition` +
  `SpawnEngine.BringIntoWorld(customHouse)` re-store. 1:1.
- WorldMap.cs:147-153 GetInstanceCount — `twinCount = GetTwinCount(); if 0 ->1; += GetBeginnerTwinCount()`. 1:1.
- World.cs:70-78 StoreObject — `if (!_allObjects.TryAdd(objId, obj)) throw new DuplicateAionObjectException(...)`.
  NO isInWorld guard. 1:1.
- House objectId — `new House(HouseAddress, int)` -> `IDFactory.GetInstance().NextId()` once. 1:1 (no per-twin id).

### Disposition
No code change. This is a faithful reproduction of a Java latent bug. A full-world SpawnEngine.SpawnAll boot would
DuplicateAionObjectException on Heiron/Beluslan instance #2 in BOTH Java and C#. The implication for the DB-backed
full-StartAsync boot (Part 2 below): you CANNOT run an unmodified full SpawnAll over the real world_maps even WITH
a DB — the housing twin-spawn would throw, faithfully, in Java too. **Real Java avoids the crash only because the
live server does not run `spawnAll()` over a world where Heiron/Beluslan got 4 instances created AND houses spawned
into >1 of them in the same boot** — i.e. in the real binary this path is reached but the DuplicateAionObjectException
is a known faithful outcome; mirroring it (let it throw) is correct. Do NOT add an `if(!World.IsInWorld(objId))`
guard — Java has none. If a future DB-backed full-boot test wants to exercise SpawnAll past housing, it must either
(i) restrict the seeded world_maps to non-twin housing maps, or (ii) assert the DuplicateAionObjectException is the
faithful Java behavior — NEVER patch HousingService/World to dedupe.

## SCOPE — DB-backed full-StartAsync bootstrap harness (read-only assessment, 2026-06-16)

GOAL: run a FULL GameServerBootstrapService.StartAsync against the opt-in MySQL container (3307 / aion_gs /
root:aion, gated on AION_GAMESERVER_DB_INTEGRATION=1, the same env switch SystemMailRepositoryDatabaseIntegration
Tests use) and flip on the DB-required wires.

### What the DB unblocks (services that NRE/throw today only because PlayerDAO/etc. return null on no-DB)
- **#2 HousingService block (main:119-123) — the primary DB-gated unblock.** HousingService ctor ->
  RevokeOwnershipOfDeletedPlayers() -> `new HashSet<int>(PlayerDAO.GetUsedIDs())` (HousingService.cs:52;
  PlayerDAO.cs:359). GetUsedIDs() returns null on no-DB (Java identical, no guard) -> ArgumentNullException. WITH
  the DB up, GetUsedIDs returns the real (possibly empty) id array -> ctor completes -> HousingService.GetInstance()
  can be wired at main:119. Also HousesDAO.LoadHouses (HousingService.cs:43) reads the houses table. The prior
  section already CONFIRMED "with the DB up, the full StartAsync boot ran SpawnAll past HousingService" — so the DB
  satisfies the ctor; the remaining blocker past it is the house-twin-spawn (Part 1, faithful — let it throw / seed
  non-twin maps only).
- **PlayerDAO.setAllPlayersOffline() (initUtilityServicesAndConfig)** (PlayerDAO.cs:424) — currently a boot GAP
  (inert with no DB: UPDATE players SET online=0). With the DB it executes and flips the online flag for any
  persisted rows. Cosmetic until real logins persist, but it becomes a real, observable boot step under a populated
  players table.
- **player-offline init / persisted-player-dependent reads** — any boot read keyed off a populated players/
  legions/inventory table (LegionService.GetCachedLegions for PeriodicSaveService's LegionWarehouseSaveTask,
  ServerVariablesDAO for ServerRunTimeSaveTask, BrokerService/AnnouncementService/CommandsAccessService DAO loads)
  goes from "try/catch -> empty + logged" to actually returning seeded rows. None of these BLOCK boot today (all
  DAO-guarded), but with the DB they exercise their real query paths (real DB-fidelity coverage, not just no-DB
  no-op coverage).
- **PeriodicSave task BODIES (main:156)** — already wired + boot-safe (commit 62c408390). With the DB, the
  scheduled LegionWarehouseSaveTask (InventoryDAO.Store + ItemStoneListDAO.Save) and ServerRunTimeSaveTask
  (ServerVariablesDAO.Store "serverLastRun") actually WRITE to the DB instead of try/catch-logging false — turns
  the task-body assertions from "no-throw" into "row persisted".

### What stays DEFERRED even WITH the DB (heavy SPAWN/world-map, NOT DB-gated)
- **#1 SiegeService.initSieges() (main:142)** — needs the full SPAWNS_DATA siege-spawn dir + siege/artifact world
  maps loaded into World so ArtifactSiege.OnSiegeStart -> Siege.InitSiegeBoss finds its boss (else SiegeException
  "Siege Boss not found for siege 1012"). This is a SPAWN-DATA + world-map harness need (scoped in the spawn-data-
  backed harness section below), NOT a DB need. A DB alone does not satisfy it.
- **#5 PvpMapService.init() (main:176)** — needs world map 301220000 in WORLD_MAPS_DATA + spawns keymasters/chests
  (and conflicts with the bootstrap empty-world invariant). SPAWN/world-map need, not DB.
- **The house-twin-spawn (Part 1)** — faithful Java bug; even with the DB, a full SpawnAll over the real
  world_maps throws DuplicateAionObjectException on Heiron/Beluslan instance #2. Not DB-fixable; must seed non-twin
  housing maps or assert-the-throw. NEVER patch.

### What flips green WITH the DB present
- A new DB-gated test (`[Fact]` early-return unless AION_GAMESERVER_DB_INTEGRATION=1, mirroring
  SystemMailRepositoryDatabaseIntegrationTests' InitializeDatabaseFactory/InitializeSchema/Seed pattern) can:
  bring up DatabaseFactory against 3307, run StartAsync with HousingService wired at main:119-123, and assert the
  boot reaches SpawnAll past HousingService (it does — confirmed). To get a CLEAN full-SpawnAll it must seed a
  world_maps subset EXCLUDING the twin housing maps (210040000/220040000) OR expect the faithful
  DuplicateAionObjectException. Everything else (the ~30 already-wired getInstance/init services) is already green
  no-DB.

### Honest frontier assessment
**The autonomous IN-MEMORY work is essentially complete.** All bounded boot-init wires that can run without a DB
or a heavy spawn/world-map load are wired (BOOT-COMPLETENESS CENSUS below: only #1/#2/#5 remain, each at a real
data/DB floor). The three remaining frontiers are ALL user-environment-gated, NOT code gaps:
1. **DB frontier (#2 Housing + setAllPlayersOffline + persisted-player reads)** — requires the 3307 MySQL
   container RUNNING + AION_GAMESERVER_DB_INTEGRATION=1. The C# code is faithful and ready; only the environment
   (a live DB) is missing. This is the SAME frontier as memory three-server-stack-boots' real-client
   login->enter-world test (Front A): both need the populated DB + a running server process, not more porting.
2. **Heavy-spawn/world-map frontier (#1 Siege + #5 Pvp)** — requires the spawn-data-backed harness (scoped below:
   seed real spawns/ + world_maps.xml + NPC_DATA into the test World). Medium effort, IN-MEMORY-doable (no DB), but
   it is a sizeable fixture-data + assert-evolution task, the highest-value remaining bounded in-memory move.
3. **House-twin-spawn** — RESOLVED as faithful (Part 1); no work, just don't patch.

CONCLUSION: there IS one remaining bounded IN-MEMORY-testable move — the spawn-data-backed bootstrap harness
(unblocks #1 + #5 together, scoped in the existing section below). Beyond that, the frontier is genuinely
DB-environment (the 3307 container) and real-client (Front A login->enter-world), both user-environment-gated. The
porting/faithfulness arc has no remaining un-blocked autonomous code work other than that one spawn-harness task.

## RESOLVED — spawn-backed integration test proves NPCs spawn end-to-end (commit pending, 2026-06-16)

Added GameServerBootstrapTests.GameServerBootstrap_RealSpawnDataMaterializesNpcsIntoWorld — the END-TO-END
NPC-spawn proof. It loads the REAL game-server/data + 147MB cache via DataManager.LoadAsync(repoRoot) (same
real-data path as RealStaticDataLoadIntegrationTests), brings up the real boot machinery (DataManager + World
maps + IDFactory + ThreadPool singleton bridges + the AIEngine/ZoneService/GeoService engines the spawn path
needs), then drives the faithful SpawnEngine.SpawnObject path over Sanctum's (110010000) real SPAWNS_DATA spawn
groups and asserts the World store materializes real Npc instances. RESULT: 357 Npc objects (360 spawn calls;
delta = gatherables) materialized into Sanctum, incl. known NPC Euterpe (798173). Before the SPAWNS_DATA fix
this was 0 (hollow SpawnsData singleton). Skips (returns) when the real cache is absent. Per-class GREEN:
build 0, Bootstrap 8/8 (7 minimal + this), Golden 167/167, RealStaticDataLoad 1/1. (Combined-project run still
shows the PRE-EXISTING GoldenStatsInfo DataManager-singleton flake — 2 failures on clean HEAD too, passes
per-class; out of scope, gate per-class per the contract.)

THREE FAITHFULNESS FIXES landed alongside (each a real port bug the spawn path surfaced, not test scaffolding):
1. **GameServerBootstrapService engine-init ORDER** — the engine InitAsync block sat just before
   SpawnEngine.SpawnAll(), i.e. AFTER the location-init cluster's spawning services (VortexService.
   initVortexLocations spawns NPCs). Java (GameServer.main:101-102) inits the engines in PARALLEL right after
   DataManager.getInstance() and BEFORE every spawn path. Moved the C# engine-init up to right after
   DropRegistration-precursor (before the location-init cluster), matching Java. Every spawned Npc resolves its
   AI via AIEngine.NewAI, so the AIEngine MUST be up before any spawn — this was an ordering bug invisible until
   the spawn path ran with real data.
2. **GameServerBootstrapService IDFactory singleton bridge** — StartAsync locked the IDFactory's ids but never
   called IDFactory.RegisterInstance(_idFactory). Every VisibleObject ctor (Npc/Gatherable/...) takes its
   objectId from IDFactory.GetInstance().NextId(), so the FIRST boot spawn NRE'd "IDFactory singleton bridge not
   initialized" — a latent PRODUCTION boot bug (Program.cs didn't bind it either). Now bound right after LockIds,
   mirroring the ThreadPoolManager/World/DataManager bridges. Faithful (Java IDFactory.getInstance() is the
   singleton the spawn path uses).
3. **AIName attribute Inherited=false** — Java @AIName is NOT @Inherited, so AIEngine.getAnnotation(AIName.class)
   returns null for a subclass (SiegeNpcAI extends AggressiveNpcAI does NOT inherit "aggressive"). C# custom
   attributes default to Inherited=true, so GetCustomAttribute<AIName>() on SiegeNpcAI returned the base's
   "aggressive" and double-registered it ("Duplicate AIs with name aggressive"). Set
   [AttributeUsage(..., Inherited = false)] to match Java exactly. Plus a defensive guard in
   OnClassLoadUnloadListener.DoMethodInvoke: the C# ScriptManager scans EVERY loaded assembly (vs Java's source-
   dir scan), so reflecting custom attributes on test-platform methods can raise TypeLoad/FileNotFound for an
   unresolvable attribute type — skip those (never an Aion @OnClassLoad hook; production = game-server assemblies
   only, so behaviour is identical).

### Siege/PvP wire-flip: STILL DEFERRED (Java does NOT guard empty data; full SpawnAll needs a DB) — finding below
- **#1 SiegeService.initSieges() (main:142)** and **#5 PvpMapService.init() (main:176)** were NOT flipped on in
  the shared StartAsync. CONFIRMED via Java source: neither guards empty data. initSieges() guards only
  !SIEGE_ENABLED; its updateFortressNextState() does getSiegeLocation(scheduledLocId).setNextState() with NO null
  guard, and the FULL real siege_schedule.xml schedules SiegeStartRunnables even under the minimal fixture, so
  with empty SIEGE_LOCATION_DATA getSiegeLocation(...) is null -> NRE (Java NPEs identically). PvpMapService.init()
  unconditionally calls InstanceService.getNextAvailableInstance(301220000,...) — needs world map 301220000, absent
  under the minimal fixture. Both run for ALL boots if wired into StartAsync, so flipping them on would break the
  minimal-fixture bootstrap 7/7. Per the hard rule (no un-faithful guard Java lacks), they STAY deferred in
  StartAsync. They can only be wired once the SHARED boot path carries spawn+world+siege data AND a DB.
- **#2 Housing is the real blocker for a full-StartAsync spawn boot.** SpawnEngine.SpawnAll() -> per-instance
  HousingService.SpawnHouses() -> HousingService ctor -> RevokeOwnershipOfDeletedPlayers() ->
  new HashSet<int>(PlayerDAO.GetUsedIDs()); GetUsedIDs() returns null on no-DB (Java NPEs identically, no guard),
  so the full SpawnAll REQUIRES a DB. The opt-in MySQL integration harness (3307, aion_gs, gated on
  AION_GAMESERVER_DB_INTEGRATION=1) DOES satisfy HousingService — verified: with the DB up, the full StartAsync
  boot ran SpawnAll past HousingService. BUT a SECOND whole-world-boot issue then surfaced (see RECOMMENDED NEXT).

RECOMMENDED NEXT (whole-world full-StartAsync spawn boot, DB-backed): two pre-existing whole-world concerns block
a full SpawnAll boot even WITH the DB, and both are FAITHFUL (Java does the same) so they need investigation, not
a quick guard:
  (a) **House double-spawn across twin instances.** SpawnAll iterates worldMap.forEach over ALL getInstanceCount()
      instances (twin_count + beginner_twin_count; e.g. Heiron 210040000 = 4), and HousingService.spawnHouses
      re-uses the SAME address-cached House object per instance -> BringIntoWorld(sameHouse) collides on the House
      objectId in World.StoreObject (DuplicateAionObjectException). Java's worldMap.forEach + spawnHouses is
      identical, so this is either a Java latent bug, or houses-bearing maps actually have twin_count such that
      only one instance carries addresses — confirm against Java/real data before wiring full SpawnAll.
  Once (a) is understood, a DB-backed full-StartAsync boot test + the siege/pvp wire-flip can land together.

## RESOLVED — 2 of the 6 boot-init deferrals unblocked via fixture enrichment (commits 2c05275b8 / 713fef10a, 2026-06-16)

Enriched the GameServerBootstrapTests StaticDataFixture to seed the SPECIFIC REAL static_data files each
deferred service needs at init (copied verbatim from game-server/data/static_data via a FindRepoRoot walk;
never invented; skipped when the repo data tree is absent). The fixture's LoadLeafHoldersFromFiles reads each
holder from a fixed sub-path of the temp static_data dir, so dropping the real file there populates exactly that
holder and leaves every other holder empty (minimal). Then flipped each gated wire on at its Java-correct boot
site and re-gated on the 7/7 bootstrap test (all-green-or-revert). Per-class verify each commit: build 0,
bootstrap 7/7, golden 167/167, RealStaticDataLoad green.

NOW WIRED:
- **#3 PeriodicInstanceManager.getInstance() (main:166)** — UNBLOCKED. Fixture seeds the real
  auto_group/auto_group.xml (every static-init AutoGroupType maskId 1,2,3,21-45,101-103,107,108,109,111 is
  present), so the ctor's GetAGTByMaskId -> GetTemplate resolves non-null templates and schedules the
  dredgion/kamar/ophidan/iron-wall/idgel registration crons. Commit 2c05275b8.
- **#4 HTMLCache.getInstance() (main:163)** — UNBLOCKED. Fixture copies the real static_data/HTML tree to a
  temp dir and points HTMLConfig.HTML_ROOT/HTML_CACHE_FILE at temp paths, so the ctor's Reload(false) ->
  ParseDir caches the real .xhtml files instead of throwing DirectoryNotFoundException. Commit 713fef10a.

STILL DEFERRED after attempt (precise blockers, each faithful 1:1 — NOT a port defect):
- **#1 SiegeService.initSieges() (main:142)** — ATTEMPTED + REVERTED. Seeding siege/siege_locations.xml fixes
  the original UpdateFortressNextState null-GetSiegeLocation NRE, but initSieges goes DEEPER: it StartSiege()s
  every standalone artifact, and ArtifactSiege.OnSiegeStart -> Siege.InitSiegeBoss throws
  `SiegeException: Siege Boss not found for siege 1012` because the artifact boss NPC is not spawned (empty
  SPAWNS_DATA => GetSiegeSpawnsByLocId null => SpawnNpcs no-op; no siege spawns, no artifact world map).
  Faithful (Java throws SiegeException there too without the boss spawn). BLOCKER = HEAVY-DATA/SPAWN: needs the
  full SPAWNS_DATA siege-spawn dir + the siege/artifact world maps loaded into World, not a leaf-holder seed —
  i.e. a real spawn/world boot, which the minimal bootstrap fixture deliberately does not load. Defer to a
  spawn-data-backed harness.
- **#5 PvpMapService.getInstance().init() (main:176)** — NOT ATTEMPTED-to-green (analysis defer). Init ->
  InstanceService.GetNextAvailableInstance(301220000) needs world map 301220000 (=> the full real world_maps.xml
  loaded into World.LoadWorldMaps) AND PvpMapHandler.OnInstanceCreate actively SPAWNS keymasters/treasure
  chests/NPCs (Spawn/BringIntoWorld) — needing NPC_DATA + spawn infra, AND it materializes world objects which
  would break the bootstrap's empty-world invariant (Assert.Equal(0, world.ObjectCount) after stop). BLOCKER =
  HEAVY-DATA/SPAWN + invariant-conflict: same floor as #1. Defer to a spawn-data-backed harness.
- **#2 Housing (main:119-123)** — CONFIRMED DB-HARNESS-GATED (checked Java). Java PlayerDAO.getUsedIDs()
  returns NULL on SQLException (no DB), and revokeOwnershipOfDeletedPlayers does IntStream.of(null) -> Java
  NPEs identically. Java is DB-REQUIRED here; it does NOT guard null. Per the hard rule (DB-required init that
  Java can't run without a DB -> defer to a DB harness, don't fake), NOT wired and NO un-faithful guard added.
  Defer to a DB harness.
- **#6 PeriodicSaveService (main:156)** — RESOLVED (commit 62c408390, 2026-06-16). Re-ported the faithful Java
  singleton 1:1: PeriodicSaveService.GetInstance() + SingletonHolder + the inner PeriodicSaveTask base and the
  TWO tasks — LegionWarehouseSaveTask (period PeriodicSaveConfig.LEGION_ITEMS * 1000 ms = 1200*1000;
  LegionService.GetInstance().GetCachedLegions() -> per-legion GetLegionWarehouse().GetItemsWithKinah() +
  AddRange(GetDeletedItems()) -> InventoryDAO.Store(items, null, null, legionId) + ItemStoneListDAO.Save(items),
  try/catch-logged) and ServerRunTimeSaveTask (period 2 min; ServerVariablesDAO.Store("serverLastRun", nowMillis)).
  Scheduling uses ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask (the faithful Future analogue,
  returns ScheduledTask with Cancel(bool)); OnShutdown() stores+cancels each task. The intervals are the
  PeriodicSaveConfig @Property defaults (LEGION_ITEMS=1200) + the literal TimeUnit.MINUTES.toMillis(2) — never
  invented. The reworked DI GameEngine + its Program.cs:74/77 registration were RETIRED, and the slop-shape test
  (PeriodicSaveService_StoresServerLastRunPeriodicallyAndOnShutdown, which exercised the IServerVariablesRepository
  DI abstraction) was REPLACED with a faithful singleton test (PeriodicSaveService_GetInstanceSchedulesSaveTasks:
  GetInstance() non-null + same-instance, OnShutdown no-throw with empty legion cache). Wired
  PeriodicSaveService.GetInstance() at GameServer.main:156 in GameServerBootstrapService. Task bodies are
  boot-safe: GetCachedLegions() is an in-memory empty map (no DB) and ServerVariablesDAO.Store is fully
  try/catch-guarded (no-DB => logged false). Build 0, golden 167/167, bootstrap 7/7, RealStaticDataLoad green.

SUMMARY: 3/6 deferrals now unblocked (AUTO_GROUP + HTML leaf reads, and #6 PeriodicSaveService re-port). The
remaining 3 sit at a real floor — #1/#5 need the heavy SPAWNS_DATA + world-map boot (and #5 also conflicts with
the empty-world invariant), #2 is DB-required (no Java guard to faithfully port).
RECOMMENDED NEXT: (a) a spawn-data-backed bootstrap harness (load the real spawns/ + world_maps.xml into a
test World) would unblock BOTH #1 and #5 at once — scoped below; (b) a DB-backed test harness unblocks #2.

## SCOPE — spawn-data-backed bootstrap harness for #1 SiegeService.initSieges + #5 PvpMapService (read-only assessment, 2026-06-16)

GOAL: evolve GameServerBootstrapTests so the boot SpawnEngine.SpawnAll() actually populates the test World
(siege/artifact bosses for #1, pvp keymasters/chests for #5), then flip #1/#5 on and change the empty-world
assert (`Assert.Equal(0, world.ObjectCount)`) to an expected populated count.

WHAT THE SUBSTRATE ALREADY GIVES US (no new loader work):
- SPAWNS_DATA and WORLD_MAPS_DATA already load 1:1 from the real XML — proven green by
  RealStaticDataLoadIntegrationTests (SpawnsDh.GetSpawnsByWorldId(110010000) non-empty incl. siege spawn maps;
  WorldMaps2 from world_maps.xml). StaticData.LoadLeafHoldersFromFiles reads `spawns/` (TryLoadMergedHolder,
  singleRootTag) + `world_maps.xml` from fixed sub-paths of the static_data dir.
- SpawnEngine.SpawnAll() is already wired at boot (GameServerBootstrapService:165) and is faithful — it iterates
  WORLD_MAPS_DATA -> per non-instance map SpawnInstance -> SPAWNS_DATA.GetSpawnsByWorldId. It is dormant in the
  bootstrap test ONLY because the minimal StaticDataFixture seeds neither holder.
- The fixture already has the exact mechanism: CopyRealFile(realStaticData, fixtureDir, relativePath) +
  FindRepoRoot walk (used today for auto_group/auto_group.xml + the HTML tree). Same move seeds spawns + world maps.

STEPS:
1. Fixture seed (StaticDataFixture.Create): CopyRealFile the real `world_maps.xml`, and copy the `spawns/` dir
   (every spawn_map file — it's a multi-file merged holder, so the whole dir, like the HTML tree copy). NPC_DATA
   is also needed for SpawnEngine to resolve npc templates when bringing spawns into the world — seed npc_skills/
   the npc data files too (verify which holder SpawnInstance actually dereferences; SpawnAll itself only needs
   SPAWNS_DATA+WORLD_MAPS, but BringIntoWorld/VisibleObject may touch NPC_DATA). Skip-guard when repo data absent
   (same `if (repoRoot != null)` pattern already there) so the test still runs in a data-less checkout.
2. #1 SiegeService.initSieges(): with the siege spawn maps + siege/artifact world maps now in World, the
   ArtifactSiege.OnSiegeStart -> Siege.InitSiegeBoss path finds its boss (no more `SiegeException: Siege Boss not
   found for siege 1012`). Also seed siege_locations.xml (the original UpdateFortressNextState null-GetSiegeLocation
   guard) — already identified. Then flip the initSieges() wire on at main:142.
3. #5 PvpMapService.init(): InstanceService.GetNextAvailableInstance(301220000) needs world map 301220000 (now
   loaded), and PvpMapHandler.OnInstanceCreate spawns keymasters/chests into that instance. Flip init() on at main:176.
4. Assert evolution: SpawnAll + siege + pvp now populate World, so `Assert.Equal(0, world.ObjectCount)` after
   StopAsync must become a populated expectation. Two options: (a) assert a STABLE lower-bound
   (`Assert.True(world.ObjectCount > 0)` after StartAsync, before StopAsync) since the exact count is data-version
   sensitive; (b) pin an exact count from a single known seeded world (e.g. assert N npc objects on map 110010000)
   the way RealStaticDataLoad pins specific npc ids — more brittle but exact. Recommend (a) for the boot test +
   keep the exact-id pins in RealStaticDataLoad. The post-StopAsync assert should verify the world is TORN DOWN
   (objects despawned) rather than "never populated" — change it from `== 0 always` to `populated during run,
   cleared on stop`.

EFFORT: MEDIUM. No new deserialization/loader code (the holders + SpawnEngine are done). The work is fixture
data-seeding (copy real spawns/ + world_maps.xml + NPC_DATA into the temp dir), flipping 2 wires, and reworking
the world-count assertion. The siege-boss spawn dependency chain (artifact world map + siege spawn map both
present) is the one thing to verify end-to-end — if a specific artifact's boss spawn lives in a spawn map the
SpawnEngine doesn't reach (instance-only map, or a handler-gated spawn), initSieges may still throw and that
artifact's siege must be confirmed against Java (Java also requires the spawn).

RISK:
- MEDIUM data-coupling: copying the full spawns/ dir + world_maps.xml makes the bootstrap test load a large
  data set (slower; closer to a real boot). Mitigate by only seeding the maps the asserts touch if SpawnEngine
  tolerates a partial WORLD_MAPS_DATA (it iterates whatever's present — a subset is fine and keeps the test fast).
- LOW-MED test-pollution: RealStaticDataLoad already documented a DataManager static-singleton cross-test
  pollution when run in the same process as the bootstrap test. Populating the bootstrap World from the same real
  holders increases shared-static surface; keep per-class verification (the task contract) and watch the combined
  filter.
- LOW invariant churn: every other assert in GameServerBootstrap_LoadsDataInitializesWorldAndStartsGameTime keys
  off the minimal fixture (e.g. GetElementCount("item")==1). Seeding more holders may change those counts — audit
  each `Assert.Equal(1, ...)` against the enriched fixture or scope the seeding to a SEPARATE new test method
  (recommended: add `GameServerBootstrap_SpawnsSiegeAndPvpWorld` rather than mutating the existing minimal test,
  preserving the minimal-fixture invariants for the other asserts).

RECOMMENDED: add a NEW bootstrap test (spawn-data-backed) seeded with spawns/ + world_maps.xml + NPC_DATA that
asserts world populates + siege/pvp wire cleanly, leaving the existing minimal test (and its empty-world
invariant) intact. This unblocks #1 + #5 together and is the highest-value remaining boot-init move.

## RESOLVED — Second-pass + trailing main services wired (commits 5fdff6c10 / 5a2fe74c4 / 334a07ef4 / 3fc0b0857 / f44838a62, 2026-06-16)

Drained the bounded boot-init long tail across GameServer.main. All in exact Java order, each gated on the
GameServerBootstrapTests (7/7) safety net (wire -> ~Bootstrap -> revert+defer if it NREs). Per-class verify
all-green every commit: build 0, bootstrap 7/7, golden 167/167, RealStaticDataLoad green.

NOW WIRED (in main order):
- DropRegistrationService.getInstance() (main:108) — empty ctor; drop-table singleton.
- HousingService block (main:119-123) — DEFERRED (no-DB edge, see below).
- ChallengeTaskService.getInstance() (main:124) — empty task maps.
- LimitedItemTradeService.getInstance().start() (main:136) — limited-trade NPC collection + reset crons (empty data => no-op).
- PlayerLimitService.getInstance().scheduleUpdate() (main:137-138, guarded LIMITS_ENABLED) — daily sell-limit reset cron.
- SiegeService.initSieges() (main:142) — DEFERRED (fixture data gap, see below).
- BaseService.getInstance().initBases() (main:144) — starts casual/stained/panesterra bases.
- WorldRaidService.getInstance().initWorldRaids() (main:146) — schedules world raids via cron.
- ConquerorAndProtectorService.getInstance().init() (main:148) — registers CP worlds + kills-decrease task.
- AnnouncementService.getInstance() (main:150) — loads announcements (DAO-guarded).
- WeatherService.getInstance() (main:152) — per-zone weather state/rotation.
- BrokerService.getInstance() (main:153) — auction broker load + expiry/save schedules.
- Influence.getInstance() (main:154) — abyss influence ratios.
- ExchangeService.getInstance() (main:155) — empty ctor.
- PeriodicSaveService (main:156) — DEFERRED (reworked DI GameEngine, not faithful 1:1, see below).
- AtreianPassportService.getInstance() (main:157) — passport expire + daily-09:00 reset cron.
- AbyssRankingCache.getInstance() (main:164) — ranking-window packet cache (DAO-guarded).
- AbyssRankUpdateService.scheduleUpdate() (main:165) — rank-update + daily-GP-loss crons.
- PeriodicInstanceManager.getInstance() (main:166) — DEFERRED (AUTO_GROUP_DATA fixture gap, see below).
- EventService.getInstance().start() (main:167) — active-event collection + 5-min check cron.
- AdminService.getInstance() (main:169) — item-restriction list (IOException-guarded).
- CommandsAccessService.loadAccesses() (main:170) — command ACLs (DAO-guarded).
- PlayerTransferService.getInstance() (main:172) — REMOVE_SKILL_LIST '*' default no-op.
- CustomInstanceService.getInstance() (main:177) — empty ctor.

NULL-DEP / FIDELITY FIXES (Java @Property defaults as field initializers via CronExpressions.GetOrCreate, OR
XmlSerializer member public-ization; no invented values):
- SiegeSchedules + WorldRaidSchedules: [XmlElement]/[XmlAttribute] PRIVATE fields -> widened to public (XmlSerializer
  binds public members only; were returning null lists => InitSieges/InitWorldRaids foreach NRE). Real deser bug.
- RankingConfig.TOP_RANKING_UPDATE_RULE='0 0 0 ? * *', TOP_RANKING_DAILY_GP_LOSS_TIME='0 0 12 ? * *'.
- AutoGroupConfig.{DREDGION,KAMAR_BATTLEFIELD,ENGULFED_OPHIDAN_BRIDGE,IRON_WALL_WARFRONT,IDGEL_DOME}_TIMES =
  1-element CronExpression[] (ArrayTransformer splits on commas OUTSIDE quotes => quote-wrapped default = 1 element).
- HousingConfig.HOUSE_AUCTION_END_TIME='0 0 12 ? * SUN', AUCTION_AUTO_FILL_TIME / HOUSE_MAINTENANCE_TIME='0 0 0 ? * MON'.
- CustomConfig.LIMITS_UPDATE='0 0 0 ? * *'.
- EventsConfig.DISABLED_EVENTS = empty set (Java @Property no defaultValue but events.properties is empty =>
  CommaSeparatedValueTransformer('') => empty set, not null).
- EventData.events = new() (empty-but-non-null when timed_events XML absent; matches BaseData.baseTemplates convention).

STILL DEFERRED (each faithful 1:1 — blocked ONLY by a bootstrap test-fixture data gap or a reworked type, NOT a
port defect; the real server has the data/DB and these run):
1. **SiegeService.initSieges() (main:142)** — UpdateFortressNextState() does GetSiegeLocation(scheduledLocId).SetNextState
   with no null guard (Java has none either). The fixture loads EMPTY SIEGE_LOCATION_DATA while siege_schedule.xml
   is the full real file => GetSiegeLocation returns null => NRE. Wire once the fixture seeds matching siege location data.
2. **HousingService + HousingBidService + AuctionEndTask/AuctionAutoFillTask/MaintenanceTask (main:119-123)** —
   HousingService ctor does new HashSet<int>(PlayerDAO.GetUsedIDs()); GetUsedIDs returns NULL on DB failure (Java
   NPEs identically). No-DB fixture => ArgumentNullException. Wire once the harness provides a DB (or GetUsedIDs
   returns empty on no-DB). The 3 auction-task cron DEFAULTS are already populated (fix stands).
3. **PeriodicInstanceManager.getInstance() (main:166)** — ScheduleRegistration log line -> AutoGroupType.GetTemplate()
   -> data[self].Template needs AUTO_GROUP_DATA (absent in fixture). Cron-array hazard already fixed. Wire once the
   fixture seeds AUTO_GROUP_DATA.
4. **HTMLCache.getInstance() (main:163)** — ctor ParseDir('./data/static_data/HTML/') throws DirectoryNotFoundException
   (Java NPEs on null listFiles too). Fixture ships no HTML/ dir or html.cache. Wire once the fixture seeds an HTML dir.
5. **PvpMapService.getInstance().init() (main:176)** — unconditional InstanceService.GetNextAvailableInstance(301220000,...)
   needs that world map; fixture loads empty WORLD_MAPS_DATA. Wire once the fixture carries the pvp-map world.
6. **PeriodicSaveService (main:156)** — the C# type is a reworked DI-constructed GameEngine (only schedules a
   server-last-run variable), NOT a faithful 1:1 of Java's singleton (player/legion periodic saves). Needs a DI
   instance + faithful re-port; out of scope for a bounded getInstance() wire.

RECOMMENDED NEXT: the 5 fixture-data-gap deferrals (#1-5) all unblock with the SAME move — enrich the
GameServerBootstrapTests StaticDataFixture (or add a DB-backed harness) so SIEGE_LOCATION_DATA / AUTO_GROUP_DATA /
WORLD_MAPS(pvp-map) / an HTML dir / a (test) DB are present, then flip each deferred wire on and re-gate. #6
(PeriodicSaveService faithful re-port) is a separate scoped task. All remaining boot-init GAPs are now either wired
or one of these 6 documented deferrals — the boot-init long tail is drained to its data/DB/reworked floor.

## RESOLVED — Location-init cluster wired (commit pending, 2026-06-16)

The boot location-init cluster (GameServer.main lines 111-117 + TownService :127) is now WIRED in
GameServerBootstrapService.StartAsync, in exact Java order. Each is dep-clean: ctors iterate the
faithfully-loaded *_DATA holders (live via StaticData.TryLoadHolder) and any DAO read is try/catch-guarded
(no DB => empty + logged, never NRE). Bounded null-deps fixed with Java @Property defaults (no invented values).

WIRED (all build 0, per-class golden 167/167 + 5/5 LS, bootstrap 7/7, RealStaticDataLoad green):
- **BaseService.getInstance()** (main:111) — ctor builds BaseLocation per BASE_DATA template. Restores base
  capture/spawn location registry.
- **SiegeService.getInstance()** (main:112) — loads SIEGE_LOCATION_DATA fortress/artifact/outpost (guard
  SiegeConfig.SIEGE_ENABLED=true) + SiegeDAO.LoadSiegeLocations (try/catch). Restores fortress-siege location data.
- **WorldRaidService.getInstance().initWorldRaidLocations()** (main:113) — loads WORLD_RAID_DATA (guard
  EventsConfig.ENABLE_WORLDRAID). Restores world-raid locations.
- **VortexService.getInstance().initVortexLocations()** (main:115) — spawns peace-state vortex NPCs +
  schedules Theobomos/Brusthonin invasions (guard CustomConfig.VORTEX_ENABLED=true). Restores dimensional-vortex
  invasion lifecycle.
- **LegionDominionService.getInstance().initLocations()** (main:117) — builds LegionDominionLocation per
  LEGION_DOMINION_DATA + LegionDominionDAO (try/catch). Restores legion-territory-control locations.
- **TownService.getInstance()** (main:127, after SpawnEngine.spawnAll) — loads per-race towns via TownDAO
  (try/catch) + seeds from HOUSE_DATA when empty. Restores town registry.

NULL-DEP FIXES (faithful @Property defaults, same shape as the CronJobService/SiegeConfig fix):
- **CustomConfig.VORTEX_THEOBOMOS_SCHEDULE / VORTEX_BRUSTHONIN_SCHEDULE** were null Quartz.CronExpression
  fields (no initializer) — VortexService.initVortexLocations NRE'd on CronService.Schedule(..., null) (the
  CronJobService failure mode). Initialized from the Java @Property defaultValue cron strings (identical to
  config/main/custom.properties): theobomos `"0 0 16 ? * SUN"`, brusthonin `"0 0 16 ? * SAT"` via
  CronExpressions.GetOrCreate. No invented value.
- **BaseData.baseTemplates** + **LegionDominionData.ldl** ([XmlElement] List fields) were null when the XML
  is absent (e.g. minimal bootstrap-test fixture) — GetAllBaseTemplates()/GetLocationTemplates() returned
  null => foreach NRE. Initialized `= new()` (XmlSerializer add-to-existing / JAXB-faithful: stays empty when
  the element is absent, populated when present). Matches the established pattern on HouseData.lands /
  SiegeLocationData/VortexData/WorldRaidData [XmlIgnore] derived maps.

ORDERING FIX: **CronService.InitSingleton** moved from its late position (after SpawnAll) up to right after
GameTimeService.InitAsync (before the location-init cluster) — faithful to Java, where CronService.initSingleton
runs in initUtilityServicesAndConfig BEFORE the main-body services that schedule through it. VortexService
(main:115) / WorldRaidService.initWorldRaids / RiftService.initRifts all need a live CronService.getInstance().

PRE-EXISTING (NOT introduced here): GoldenStatsInfoFixtureTests fails ONLY when run in the same process as
the bootstrap test (combined --filter), due to shared DataManager static-singleton pollution + run order.
Confirmed identical failure on clean HEAD (1 failed / 174 passed for the combined filter, pre-change). Passes
in isolation. Per-class verification (the task contract) is all-green. This is a test-harness isolation issue,
out of scope for this wire.

## RESOLVED — CronJobService wired + SiegeConfig cron schedules populated (commit pending, 2026-06-16)

The CronJobService deferral (cron-config-transform unported) is FIXED and the service is now wired at boot.

Root cause was bounded, not a subsystem: C# `Config.Load()` is a deferred no-op, so config-holder classes
carry their `@Property` defaults as field initializers. Every SiegeConfig bool/int/float field already had its
default; only the two `Quartz.CronExpression` fields (`MOLTENUS_SPAWN_SCHEDULE`, `AHSERION_START_SCHEDULE`)
were left uninitialized (null) — there was no field initializer for them. When CronJobService's ctor passed
the null CronExpression to `CronService.Schedule`, it NRE'd on `cronExpression.CronExpressionString`
(prior bootstrap test failed with CronServiceException "Failed to start job").

FIX (faithful 1:1): the Java `@Property` defaultValue strings (and config/main/siege.properties — identical)
are `"0 0 22 ? * SUN"` (moltenus) and `"0 50 18 ? * SUN"` (ahserion). Java's `CronExpressionTransformer`
turns these into a CronExpression via `CronExpressions.getOrCreate(value)`. So the two C# SiegeConfig fields
are now initialized with `Aion.GameServer.Services.Cron.CronExpressions.GetOrCreate("0 0 22 ? * SUN")` /
`("0 50 18 ? * SUN")` — exactly what Config.Load + CronExpressionTransformer would produce given no override.
No invented values; defaultValue == properties-file value.

WIRED: `CronJobService.GetInstance()` in GameServerBootstrapService.StartAsync at the Java-correct boot site
(GameServer.main:158 — after AtreianPassportService/DebugService, before CuringZoneService/RoadService, and
after CronService.InitSingleton). Its ctor schedules the Moltenus spawn + Ahserion flight cron jobs, runs the
IdianDepthPortal spawner synchronously, and schedules the weekly LegionDominion calculation ("0 0 9 ? * WED *").

Restored at boot: Moltenus (Berserker Sunayaka) Sunday-22:00 spawn cron, Ahserion Panesterra raid Sunday-18:50
cron, Idian Depth portal spawns (Levinshor/Kaldor/Cygnea/Enshar entrances), weekly Legion Dominion calc.
Verify: build 0, golden 167/167, bootstrap 7/7 (now exercises CronJobService through StartAsync — no throw),
RealStaticDataLoad green.

## BOOT-COMPLETENESS CENSUS (Java GameServer.main vs C# GameServerBootstrapService.StartAsync, 2026-06-16)

initUtilityServicesAndConfig (Java pre-DataManager utility phase):
- UncaughtExceptionHandler set — N/A (infra; .NET host handles unobserved-exception policy).
- PropertyTransformers.register(CronExpressionTransformer) — N/A as a runtime step (Config.Load is a deferred
  no-op); the transform's EFFECT is now reproduced by SiegeConfig field initializers (see RESOLVED above).
- Config.load() — DEFERRED no-op (config-holders carry @Property defaults as field initializers). Inert for
  boot today; live consumers read the hardcoded defaults. (Infra per gameplay-faithful/infra-idiomatic.)
- DatabaseFactory.init() — DONE (Program.cs ConfigureServices / DI).
- PlayerDAO.setAllPlayersOffline() — GAP (inert at boot; matters only with a populated players table — sets
  the online flag false for all rows. No live in-process consumer at boot; cosmetic until real logins persist).
- DatabaseCleaningService.deletePlayersOnInactiveAccounts() (guarded CLEANING_ENABLE=false) — GAP/DEFER
  (default-off; needs a thread-1 pre-boot utility seam — see DEFERRED below). Inert by default.
- ThreadPoolManager.getInstance() — DONE (RegisterInstance bridge early in StartAsync).
- CronService.initSingleton(...) — DONE (guarded once-only init in StartAsync).

main (post-utility):
- JAXBUtil.preLoadContextAsync — N/A (JAXB warmup; C# uses XmlSerializer + cache, no equivalent prewarm needed).
- IDFactory.getInstance() — DONE.
- DataManager.getInstance() — DONE (StaticDataLoader, 13 holders live).
- QuestEngine/AIEngine/InstanceEngine/ChatProcessor/ZoneService/GeoService init (parallel) — DONE (engine list).
- World.getInstance() — DONE (LoadWorldMaps + RegisterInstance).
- GameTimeService.getInstance() — DONE.
- DropRegistrationService.getInstance() — DONE (wired 2026-06-16; empty ctor, bounded singleton touch).
- BaseService.getInstance() — DONE (base location registry; wired 2026-06-16 location-init cluster).
- SiegeService.getInstance() — DONE (siege location data; wired 2026-06-16).
- WorldRaidService.initWorldRaidLocations() — DONE (world-raid locations; wired 2026-06-16).
- VortexService.initVortexLocations() — DONE (vortex locations + invasion cron; wired 2026-06-16, null-cron fixed).
- RiftService.initRiftLocations() — DONE.
- LegionDominionService.initLocations() — DONE (legion-territory locations; wired 2026-06-16).
- HousingService.getInstance() — GAP? (faithful HousingService exists + runs per-instance on spawn; explicit
  boot getInstance() touch not in StartAsync — verify it self-inits via spawn path; likely effectively DONE).
- HousingService/HousingBidService/AuctionEndTask/AuctionAutoFillTask/MaintenanceTask — DEFERRED (no-DB edge:
  HousingService ctor new HashSet(PlayerDAO.GetUsedIDs()) NREs when GetUsedIDs returns null on no-DB; cron defaults fixed).
- ChallengeTaskService.getInstance() — DONE (wired 2026-06-16; empty task maps).
- SpawnEngine.spawnAll() — DONE.
- TownService.getInstance() — DONE (town registry; wired 2026-06-16). NOTE: town NPC SPAWNING still depends on
  the gated SPAWNS_DATA/TOWN_SPAWNS path (#1); this wire restores the town-level/points registry only.
- FlyRingService.getInstance() — DONE.
- RiftService.initRifts() — DONE.
- ratio-limitation block (GSConfig.ENABLE_RATIO_LIMITATION) — N/A by default (config-gated off).
- LimitedItemTradeService.start() — DONE (wired 2026-06-16; empty data => no-op).
- PlayerLimitService.scheduleUpdate() (CustomConfig.LIMITS_ENABLED) — DONE (wired 2026-06-16; LIMITS_UPDATE cron default fixed).
- SiegeService.initSieges() — DEFERRED (fixture gap: UpdateFortressNextState NREs on null GetSiegeLocation when
  SIEGE_LOCATION_DATA empty but siege_schedule.xml full).
- BaseService.initBases() — DONE (wired 2026-06-16; starts casual/stained/panesterra bases).
- WorldRaidService.initWorldRaids() — DONE (wired 2026-06-16; schedules raids via cron).
- ConquerorAndProtectorService.init() — DONE (wired 2026-06-16).
- AnnouncementService.getInstance() — DONE (wired 2026-06-16; DAO-guarded).
- DebugService.getInstance() — DONE.
- WeatherService.getInstance() — DONE (wired 2026-06-16; per-zone weather state/rotation).
- BrokerService.getInstance() — DONE (wired 2026-06-16; broker load + schedules).
- Influence.getInstance() — DONE (wired 2026-06-16; abyss influence ratios).
- ExchangeService.getInstance() — DONE (wired 2026-06-16; empty ctor).
- PeriodicSaveService.getInstance() — DONE (wired 2026-06-16, commit 62c408390; faithful singleton re-port,
  reworked DI GameEngine retired + slop test replaced).
- AtreianPassportService.getInstance() — DONE (wired 2026-06-16; expire + daily reset cron).
- CronJobService.getInstance() — DONE (this tick).
- CuringZoneService.getInstance() (guarded !GEO_MATERIALS_ENABLE; default off) — DONE (guarded, matches Java).
- RoadService.getInstance() — DONE.
- HTMLCache.getInstance() — DONE (wired 2026-06-16, commit 713fef10a; fixture seeds real HTML dir).
- AbyssRankingCache.getInstance() — DONE (wired 2026-06-16; DAO-guarded). AbyssRankUpdateService.scheduleUpdate() —
  DONE (wired 2026-06-16; ranking cron defaults fixed).
- PeriodicInstanceManager.getInstance() — DONE (wired 2026-06-16, commit 2c05275b8; fixture seeds real AUTO_GROUP_DATA).
- EventService.start() — DONE (wired 2026-06-16; empty EVENT_DATA => no-op, DISABLED_EVENTS/events null-fixes).
- AdminService.getInstance() — DONE (wired 2026-06-16; IOException-guarded file read).
- CommandsAccessService.loadAccesses() — DONE (wired 2026-06-16; DAO-guarded).
- PlayerTransferService.getInstance() — DONE (wired 2026-06-16; '*' default no-op).
- GameTimeService.startClock() — DONE.
- PvpMapService.init() — DEFERRED (fixture gap: needs world map 301220000 in WORLD_MAPS_DATA).
- CustomInstanceService.getInstance() — DONE (wired 2026-06-16; empty ctor).
- DataManager.waitForValidationToFinishAndShutdownOnFail() — DONE (ValidationTask await).
- System.gc() — N/A.
- VersionInfo/SystemInfo logAll — N/A (logging).
- PetFeedUnusualStorageArtifactCapture.installIfEnabled() — N/A by default (parity-capture seam, off).
- initNioServer() — DONE-elsewhere (network host startup is the LS/GS/CS stack, not StartAsync).
- ShutdownHook register — partial (StopAsync mirrors orderly shutdown).
- LoginServer.connect / ChatServer.connect — DONE-elsewhere (3-server stack).

GAPs ordered by gameplay impact (those with live consumers = real silent-skip):
1. **SPAWNS_DATA regular-NPC spawns** — already documented/gated below (#1, heavy).
2. **Location-init cluster** (SiegeService/BaseService/VortexService/WorldRaidService/LegionDominion
   getInstance()/initLocations) — DONE (wired 2026-06-16). Remaining: the **second-pass init*()** calls
   (SiegeService.initSieges / BaseService.initBases / WorldRaidService.initWorldRaids /
   ConquerorAndProtectorService.init) which spawn NPCs + schedule sieges/raids — the next bounded batch,
   gated on confirming the spawn/schedule paths are dep-clean.
3. **TownService** — DONE (registry wired 2026-06-16). Town NPC spawning still gated on SPAWNS_DATA (#1).
4. **PeriodicSaveService** — periodic persistence not scheduled (matters once real logins persist).
5. **CommandsAccessService.loadAccesses** — admin/chat command authorization unloaded.
6. **HTMLCache** — NPC dialog HTML uncached.
7. **EventService.start / WeatherService / BrokerService / ExchangeService / AbyssRanking / etc.** — feature
   services, each a bounded getInstance() wire pending port verification.
8. **PlayerDAO.setAllPlayersOffline / DatabaseCleaning** — DB-state, inert until players persist.

Most remaining GAPs are individually bounded getInstance()/init() wires (the same shape as this tick), each
gated only on confirming the target service is faithfully ported and its ctor doesn't NRE on an unported dep
(the CronJobService failure mode). The recommended next bounded tick: audit the location-init cluster (#2)
service-by-service and wire the ones whose ctors are dep-clean.

## RESOLVED — NpcSpawnTable dead-island retired (commit ec289dc00, 2026-06-16)

SPAWNS_DATA is now live-loaded (commit ae2e25a54), so the reworked spawn projection was orphaned and is
DELETED: `NpcSpawnTable`/`NpcRiftSpawnTable`/`NpcVortexSpawnTable` + their `*Summary` records +
`TemporarySpawnSchedule` (NpcSpawnTable.cs gone), the 4 spawn builder classes (StaticData.Builders.cs),
the StaticData streaming-spawn reader blocks (spawn_map/spawn/spot/rift_spawn/vortex_spawn/state_type/
temporary_spawn) + their ctor params / properties / build-call / locals, the now-orphaned
`ReadVortexStateTypeAttribute` helper (StaticData.Helpers.cs), and the slop `TemporarySpawnScheduleTests`.
0-consumer proof: grep PascalCase whole tree found only the island's own definition/builder/reader/test.
`VortexStateType` (Model/Vortex, faithful) and the generic Read*Attribute helpers were KEPT (shared).
Build 0, golden 167/167, bootstrap 7/7, RealStaticDataLoad green. 1140 deletions.

## RESOLVED+VERIFIED — Housing subsystem 100% retired (re-confirmed 2026-06-17 @ HEAD 5403226ff)

Depth-first re-assessment: ALL reworked Housing slop is GONE and COMMITTED (the "commit pending" note below
was already committed). Grep (PascalCase, whole src+tests) for every reworked piece —
HousingObjectTemplateSummary/HousingObjectTemplateTable/HousingWorldService/HousingVisibilityService/
HousingRepository/IHousingRepository/WorldHouse/SmHouseUpdate/SmHouseRender/SmObjectUseUpdate/
PlayerHouse(record)/HouseRegistryEntries/*Summary records/_housesBy* = ZERO .cs hits (only docs mentions).
Faithful pillar is the sole live path (House:VisibleObject + HousingService.FindPlayerHouses->List<House> @
Player.Part4.cs:362 + SM_HOUSE_* SCREAMING_CASE owning all opcodes + PlayerRegisteredItemsDAO +
HOUSING_OBJECT_DATA). NO load-bearing remainder, NO deferred scoped plan needed. Housing is DONE.
Gates: build 0, golden 196/196 byte-exact 0-skip, full suite 459/0. The two sections below are HISTORICAL.

## RESOLVED — Housing SmHouse* dead-island retired (commit pending, 2026-06-16)

The housing registry-summary dead-island is DELETED. 0-consumer re-confirmed (PascalCase grep whole
src+tests): every reference to the reworked types lived inside the island's own files; the faithful
SCREAMING_CASE pillar (SM_HOUSE_EDIT/REGISTRY/BIDS + SM_OBJECT_USE_UPDATE, House/HouseObject/
HousingService/HousingBidService/HOUSING_OBJECT_DATA/PlayerRegisteredItemsDAO) owns every opcode +
registry persistence + template data and is untouched/live.

DELETED (16 files + edits): ServerPackets SmHouseRegistry/SmHouseBids/SmHouseEdit/SmHouseObjects/
SmHouseObject/SmHouseAcquire/SmHousePayRent/SmObjectUseUpdate + HouseObjectPacketWriter;
Model/GameObjects HouseRegistryEntries (all *Summary records: RegisteredHouseObjectSummary/
RegisteredHouseDecorationSummary/HouseRegistrySummary/PlacedHouseObjectSummary) + PlayerHouse +
HouseAuctionBid (HouseAuctionBidPage/Summary/Context — island-only, consumed solely by SmHouseBids);
Dataholders/HousingObjectTemplateTable (+ HousingObjectTemplateSummary); Data/HousingRepository
(IHousingRepository/Empty/MySql). EDITS: Program.cs DI line removed; StaticData ctor-param/assignment/
property/list-decl/two housing_objects reader blocks/build-call removed; StaticData.Builders
IsHousingObjectTemplateElement + GetHousingObjectTypeId helpers removed (island-only);
HousingTemplateTable.GetDecorIds(int, HouseRegistrySummary?) overload removed (0-caller, island-coupled).
KEPT: the rest of faithful HousingTemplateTable (incl. GetPart/TryGetDecorPacketIndex public surface),
faithful PlayerHouse readers live via HousingService.FindPlayerHouses -> List<House> (faithful House is
the live type; reworked PlayerHouse record was island-only and deleted). InventoryItem DTO untouched.
Verify: build 0, golden 167/167, bootstrap 7/7, RealStaticDataLoad green.

ALL dead-island slop is now retired (NpcTemplateSummary/SkillTemplateSummary/ItemTemplateSummary/
NpcSpawnTable/Housing-SmHouse*). The big slop-retirement/correctness arc is COMPLETE. Remaining work
is NOT slop — it is the 2 peripheral service deferrals below (CronJobService cron-config-transform,
DatabaseCleaningService thread-1 seam) + the gated SPAWNS_DATA re-port, all requiring a user decision.

## (historical) PART B VERDICT — Housing SmHouse* subsystem = DEAD-ISLAND, clean-deletable NEXT TICK

Same shape as the proven NpcTemplateSummary / SkillTemplateSummary / ItemTemplateSummary / NpcSpawnTable
dead-islands: a reworked golden-blind projection running in parallel to a fully faithful pillar that is the
live path. Verified READ-ONLY (grep PascalCase whole src+tests).

### Per-element 0-consumer proof
- **Reworked SmHouse* packets** (PascalCase): `SmHouseRegistry`, `SmHouseBids`, `SmHouseEdit`,
  `SmObjectUseUpdate` — NONE are in the opcode table; ZERO external `new SmHouse*()` senders.
  `SmHouseRegistry.CreateRegisteredObjects`/`SmHouseBids.*`/`SmHouseEdit`/`SmObjectUseUpdate` are referenced
  only inside their own files (self-recursive factories). The FAITHFUL SCREAMING_CASE packets own the
  opcodes and are the live senders, 1:1 with Java:
  - `SM_HOUSE_EDIT` (opcode 82) — sent by CM_HOUSE_EDIT, CM_HOUSE_DECORATE, HouseObject, DyeAction.
  - `SM_HOUSE_REGISTRY` (opcode 116) — sent by CM_HOUSE_EDIT.
  - `SM_HOUSE_BIDS` (opcode 256) — sent by CM_GET_HOUSE_BIDS.
  - `SM_OBJECT_USE_UPDATE` (opcode 264) — sent by PostboxObject, StorageObject, UseableItemObject.
- **`HousingObjectTemplateTable` / `HousingObjectTemplateSummary`**: built in StaticData (ctor param :48,
  property :186, list :789, reader :1636-, build-call :2229) but `.HousingObjectTemplates` has ZERO readers.
  Faithful `DataManager.HOUSING_OBJECT_DATA => SD.HousingObjectDataDh` (HousingObjectData) is the live path.
- **`IHousingRepository` / `MySqlHousingRepository` / `EmptyHousingRepository`** (Data/HousingRepository.cs):
  DI-registered in Program.cs:97 (`AddSingleton<IHousingRepository, MySqlHousingRepository>`) but NO
  injection point anywhere — no ctor param, no `GetService<IHousingRepository>`, no field. Its async
  LoadWorld*/etc. methods have 0 live callers. Faithful `PlayerRegisteredItemsDAO` (Dao/) is the live
  registry persistence path (used by faithful HouseRegistry/House/HouseObjectFactory).
- **`HouseRegistryEntries`** (Model/GameObjects): read by NOTHING outside itself; its `GetSpawnedObjects`/
  `GetNotSpawnedObjects` take the reworked `PlayerHouse` record. The live faithful path is
  `HousingService.FindPlayerHouses` -> `List<House>` (faithful House/HouseRegistry), called by
  Player.Part4.cs:362.
- **`PlayerHouse`** (reworked record): referenced only by HouseRegistryEntries + itself. Faithful `House`
  is the live type.
- **Support summary types** `RegisteredHouseObjectSummary` / `HouseRegistrySummary` /
  `PlacedHouseObjectSummary`: referenced only within the island (SmHouse*/HouseRegistryEntries/PlayerHouse/
  HousingObjectTemplateTable) + ONE bleed: faithful `HousingTemplateTable.GetDecorIds(int, HouseRegistrySummary?)`
  — but `GetDecorIds` itself has 0 callers, so that method is island-coupled dead code.

### Clean-delete file/edit list (safe to execute next tick, all-green-or-revert)
- DELETE: `Network/Aion/ServerPackets/SmHouseRegistry.cs`, `SmHouseBids.cs`, `SmHouseEdit.cs`,
  `SmObjectUseUpdate.cs` (+ check `SmHousePayRent.cs`/`SmHouseObjects.cs`/`SmHouseAcquire.cs` —
  same PascalCase pattern, grep senders before deleting each).
- DELETE: `Dataholders/HousingObjectTemplateTable.cs` (+ `HousingObjectTemplateSummary` + the support
  summary records if co-located).
- DELETE: `Data/HousingRepository.cs` (IHousingRepository + Empty + MySql) + remove Program.cs:97 DI line.
- DELETE: `Model/GameObjects/HouseRegistryEntries.cs` + `Model/GameObjects/PlayerHouse.cs`.
- EDIT (relocate-or-remove, ItemStatModifier precedent): remove the `housing_objects` reader block +
  `HousingObjectTemplateTable` ctor-param/property/list/build-call from StaticData.cs/.Builders.cs/.Helpers.cs;
  remove `HousingTemplateTable.GetDecorIds(int, HouseRegistrySummary?)` (0-caller, island-coupled). KEEP
  faithful HousingTemplateTable/HousingService/HouseController/HouseRegistry/House/HouseData/
  PlayerRegisteredItemsDAO/HousingObjectData and ALL SM_HOUSE_*/SM_OBJECT_USE_UPDATE faithful packets.
- DELETE any slop-test-of-slop for these (grep tests/ for the reworked type names before executing).

VERDICT: **DEAD-ISLAND — do-next-tick clean delete, no user go-ahead required.** No live consumer; faithful
pillar already owns every opcode + the registry persistence + the template data. One care-point: scope the
StaticData/HousingTemplateTable edits to the island-coupled members only (the file itself is faithful/live).

## (historical) SPAWNS_DATA: NPC spawning was SILENTLY BROKEN (0 regular NPCs spawn at boot) — now FIXED upstream

`SpawnEngine.SpawnAll()` IS wired at boot (GameServerBootstrapService, after RiftService.InitRiftLocations,
before InitRifts) and is a faithful 1:1 port of Java spawnAll. It iterates `DataManager.WORLD_MAPS_DATA`
(loaded) -> per non-instance WorldMap -> `SpawnInstance` -> **`DataManager.SPAWNS_DATA.GetSpawnsByWorldId(mapId)`**.

`DataManager.SPAWNS_DATA` (DataManager.cs:32) is the ONLY `*_DATA` accessor that is a self-instantiated
hollow object: `public static SpawnsData SPAWNS_DATA { get; } = new();`. Every other holder delegates to
`SD.*` populated by `StaticData.LoadLeafHoldersFromFiles`. Nothing ever populates this singleton's
`Templates` / `_allSpawnMaps`. The only writer is `Event.cs:86 AddRegularSpawns` (event-driven, runtime).

Therefore `GetSpawnsByWorldId()` returns `[]` for every world -> `worldSpawns` empty -> the spawn loop body
never runs -> **zero regular NPCs and gatherables spawn at boot.** (StaticDoorSpawnManager + HousingService
still run per-instance, and rift/siege/vortex/base spawns load via their own holders, but the regular NPC
population is dead.)

### The reworked parallel that exists but is ORPHANED
`StaticData` DOES stream-parse `spawns/*.xml` at boot into reworked summary tables — `NpcSpawnTable` /
`NpcRiftSpawnTable` / `NpcVortexSpawnTable` (NpcSpawnTable.cs, `*Summary` records) built at
StaticData.cs:2475-2478. These tables are **consumed by NOTHING in src/** (only referenced inside
Dataholders/StaticData themselves). All ~20 live consumers use the faithful `DataManager.SPAWNS_DATA`
(SpawnEngine, VortexService, SiegeService, RiftService, MercenaryLocation, AgentSiege, AhserionRaid,
Base, Town(TOWN_SPAWNS), TeleportService, QuestTasks, QuestSpawnAnalyzer, KillSpawned, several quest
handlers, CM_OBJECT_SEARCH, MoveTo, ConquestOfferingPortalAI, Event). So the reworked summary tables are
dead weight; the faithful `SpawnsData` (Initialize/AddRegularSpawns/AddBase/Rift/Siege/Vortex/Mercenary/
Ahserion + all queries + spawn-search) is ALREADY fully written — it just has no loader feeding it.

### Scoped re-port (DO NOT START without user go-ahead — heavy)
Goal: at boot, deserialize `game-server/data/static_data/spawns/**/*.xml` (and the imported spawn map files)
into a real `SpawnsData` and call `Initialize()`, then have `DataManager.SPAWNS_DATA` delegate to it (drop
the hollow `= new()`).

- **Loader work (the actual gap):** SpawnsData uses `[XmlRoot("spawns")]` + `SpawnMap`/`Spawn`/`SpawnGroup`
  polymorphic model. Either (a) wire a JaxbHolderLoader/merged-holder load of the spawns dir into a
  `StaticData.Spawns` property + `DataManager.SPAWNS_DATA => SD.Spawns`, mirroring TOWN_SPAWNS_DATA, then
  call `Spawns.Initialize()` after merge; OR (b) feed the already-parsed streaming builder output
  (NpcSpawnBuilder etc.) into `SpawnsData.AddRegularSpawns/AddRift/...`. Path (a) is cleaner/faithful.
- **Model fidelity risk:** the spawn XML is large + polymorphic (rift/siege/vortex/base/mercenary/ahserion/
  temporary/pool/handler/static-door variants). Need to confirm every `[XmlElement(typeof(...))]` element-name
  binding on SpawnMap/Spawn covers the real files (same sweep discipline as SKILL_DATA). Nullable-enum and
  @XmlList proxies likely needed (handler type, difficult_id, temporary schedule, walker refs).
- **Consumers:** ~20 live (listed above) — all already on the faithful API, so NO consumer rewrite; they
  light up for free once the holder loads.
- **Orphan cleanup:** delete `NpcSpawnTable`/`NpcRiftSpawnTable`/`NpcVortexSpawnTable` + their `*Summary`
  records + the StaticData streaming-spawn builder block (StaticData.cs ~815-1605 spawn portions,
  2475-2478) once the faithful loader replaces them. (TemporarySpawnSchedule helper may be reusable.)
- **Est:** multi-batch heavy re-port (loader + element-name fidelity sweep + golden for spawn counts +
  orphan deletion). Needs a golden/integration assert on "N npc spawns loaded" per world to prove parity.
- **Why user go-ahead:** it's the last hollow holder, flagged heavy/reworked; touches the streaming
  StaticData loader spine and deletes a parallel subsystem — a coordinated big-bang, not an isolated fix.

This is the #1-value fix: until done, the server world is empty of NPCs.

## PART B — faithful-defer service wiring results

WIRED (commit 7c2935abd) — GameServerBootstrapService.StartAsync, 1:1 with GameServer.main:
- ThreadPoolManager singleton bridge bound early (Java initUtilityServicesAndConfig parity) so scheduling
  services resolve `ThreadPoolManager.GetInstance()`.
- **FlyRingService** — `GetInstance()` after SpawnAll (Java main:128). Spawns fly_rings/ templates.
- **DebugService** — `GetInstance()` after initRifts (Java main:151). Periodic world-player analysis task.
- **CuringZoneService** — GUARDED `if (!GeoDataConfig.GEO_MATERIALS_ENABLE)` (Java main:160-161). Default
  GEO_MATERIALS_ENABLE=true => not started by default, exactly like Java.
- **RoadService** — `GetInstance()` (Java main:162). Spawns roads/ templates per instance.

DEFERRED:
- **CronJobService** (Java main:158) — RESOLVED 2026-06-16 (see RESOLVED section at top). The cron-config
  values are now populated via SiegeConfig field initializers (CronExpressions.GetOrCreate of the Java
  @Property defaultValue strings) and the service is wired at the Java-correct boot site. No longer deferred.
- **DatabaseCleaningService** (Java initUtilityServicesAndConfig:227, guarded CleaningConfig.CLEANING_ENABLE
  =false) — DEFER. (1) Its Java boot site is the pre-DataManager utility-init phase; the C# hosted bootstrap
  has no faithful pre-DataManager utility seam (DatabaseFactory.Initialize happens in Program.cs ConfigureServices,
  not a runnable init step). (2) The body requires `Thread.CurrentThread.ManagedThreadId == 1` (throws
  otherwise) which the hosted StartAsync thread cannot satisfy. (3) Default CLEANING_ENABLE=false so a
  guarded-off call restores no observable subsystem. A faithful wire needs a dedicated thread-1 pre-boot
  utility step; out of scope for a bounded wire.
- **CurrentThreadRunnableRunner** — NOT a boot-started service in Java. It is a `RunnableRunner` strategy
  (services/cron/), used as the synchronous-execution variant passed to/used by CronService; GameServer.main
  never calls a getInstance()/start on it. Faithful 1:1 => there is no call site to wire. NO ACTION (the
  class already exists for when CronService selects synchronous execution). Not a defect.

## Gated list (need user decision)
1. **SPAWNS_DATA re-port** (above) — silently-broken, #1 value. Heavy big-bang.
2. **CronJobService cron-config-transform** — RESOLVED 2026-06-16 (cron schedules populated via SiegeConfig
   field initializers, service wired at boot). See RESOLVED section at top. No longer gated.
3. **DatabaseCleaningService thread-1 utility-init seam** — only if a faithful pre-boot utility phase is added.
4. **Housing SmHouse* subsystem** — RESOLVED + DELETED (see RESOLVED section at top, 2026-06-16).
   Dead-island retired; faithful pillar is the sole live path.
5. **Boot location-init cluster** (SiegeService/BaseService/VortexService/WorldRaidService/LegionDominion +
   TownService getInstance/initLocations) — RESOLVED 2026-06-16 (see RESOLVED section at top; all wired
   dep-clean). Remaining bounded getInstance() wires: the second-pass init*() (SiegeService.initSieges /
   BaseService.initBases / WorldRaidService.initWorldRaids / ConquerorAndProtectorService.init) +
   PeriodicSaveService/CommandsAccessService/HTMLCache/EventService/WeatherService/BrokerService/etc., each
   gated on per-service ctor dep-clean verification. NOT slop. See BOOT-COMPLETENESS CENSUS above.
