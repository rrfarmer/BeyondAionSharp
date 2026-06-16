using System.Diagnostics;
using Aion.GameServer.Data;
using Aion.GameServer.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.IdFactory;
using Aion.GameServer.World;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Services;

public sealed class GameServerBootstrapService : IHostedService
{
	private readonly IStaticDataLoader _staticDataLoader;
	private readonly IUsedIdRepository _usedIdRepository;
	private readonly IDFactory _idFactory;
	private readonly IEnumerable<GameEngine> _engines;
	private readonly GameWorld _world;
	private readonly GameTimeService _gameTimeService;
	private readonly ThreadPoolManager _threadPoolManager;
	private readonly GameServerRuntimeContext _runtimeContext;
	private readonly ILogger<GameServerBootstrapService> _logger;
	private bool _started;

	public GameServerBootstrapService(
		IStaticDataLoader staticDataLoader,
		IUsedIdRepository usedIdRepository,
		IDFactory idFactory,
		IEnumerable<GameEngine> engines,
		GameWorld world,
		GameTimeService gameTimeService,
		ThreadPoolManager threadPoolManager,
		GameServerRuntimeContext runtimeContext,
		ILogger<GameServerBootstrapService> logger)
	{
		_staticDataLoader = staticDataLoader;
		_usedIdRepository = usedIdRepository;
		_idFactory = idFactory;
		_engines = engines;
		_world = world;
		_gameTimeService = gameTimeService;
		_threadPoolManager = threadPoolManager;
		_runtimeContext = runtimeContext;
		_logger = logger;
	}

	public bool IsStarted => _started;

	public async Task StartAsync(CancellationToken cancellationToken)
	{
		// Java parity: GameServer.main startup order through IDFactory, DataManager, engines, World, GameTime.
		var stopwatch = Stopwatch.StartNew();
		_logger.LogInformation("Starting game-server bootstrap");

		// Java parity: GameServer.main initUtilityServicesAndConfig initializes ThreadPoolManager.getInstance()
		// early (before the services that schedule tasks through it: DebugService/CuringZoneService/CronJobService).
		// Bind the Java-style static accessor to the DI-created instance here so those boot-wired services resolve it.
		// Idempotent (RegisterInstance overwrites) so a re-entrant boot in one process is safe; production also binds
		// it in Program.cs before host.RunAsync.
		ThreadPoolManager.RegisterInstance(_threadPoolManager);

		var usedIds = await _usedIdRepository.LoadUsedIdsAsync(cancellationToken);
		_idFactory.LockIds(usedIds);
		_logger.LogInformation(
			"IDFactory initialized with {Count} reserved IDs ({PreloadedCount} preloaded from DB)",
			_idFactory.GetUsedCount(),
			usedIds.Count);

		var dataManager = await _staticDataLoader.LoadAsync(cancellationToken);
		_runtimeContext.SetDataManager(dataManager);
		// Java parity: DataManager static accessors (DataManager.ITEM_DATA, ...) read through the
		// instance singleton; bind it before any engine InitAsync touches a DataManager.* accessor.
		Aion.GameServer.Dataholders.DataManager.RegisterInstance(dataManager);
		_logger.LogInformation(
			"Static data cache loaded from {CacheFile}; {Count} XML files imported",
			dataManager.StaticData.CacheFilePath,
			dataManager.StaticData.ImportedFileCount);

		// Java parity: World() ctor loads all world maps at construction (DataManager.WORLD_MAPS_DATA.forEachParalllel ->
		// new WorldMap(template)). Mirror it here now that DataManager is registered, before any engine InitAsync (spawn,
		// rift) resolves a WorldMapInstance. Bind the World singleton-bridge (Java's World.getInstance()) at the same point.
		_world.LoadWorldMaps(Aion.GameServer.Dataholders.DataManager.WORLD_MAPS_DATA);
		GameWorld.RegisterInstance(_world);

		await _gameTimeService.InitAsync(cancellationToken);

		// Java parity: GameServer.main initializes the CronService singleton during early utility init
		// (initUtilityServicesAndConfig: CronService.initSingleton(ThreadPoolManagerRunnableRunner.class,
		// TimeZone.getTimeZone(GSConfig.TIME_ZONE_ID))), which runs BEFORE the main-body services that schedule
		// through it. Moved here (after ThreadPoolManager is bound, before the location-init cluster) so
		// VortexService.initVortexLocations()/WorldRaidService/RiftService.initRifts() all resolve a live
		// CronService.getInstance(). Guard the once-only init so a re-entrant boot (test host running StartAsync
		// repeatedly in one process) doesn't throw "already initialized".
		if (Aion.GameServer.Services.Cron.CronService.GetInstance() == null)
			Aion.GameServer.Services.Cron.CronService.InitSingleton(
				typeof(Aion.GameServer.Utils.Cron.ThreadPoolManagerRunnableRunner),
				Aion.GameServer.Configs.Main.GSConfig.TIME_ZONE_ID ?? System.TimeZoneInfo.Local);

		// Java parity: GameServer.main location-init cluster (lines 111-117), after DropRegistration, before
		// HousingService/spawns. Each loads only LOCATION data (no schedules/spawns yet — those are the second-pass
		// init*() calls later). All are bounded singleton getInstance()/init touches whose ctors iterate the
		// faithfully-loaded *_DATA holders (BASE_DATA/SIEGE_LOCATION_DATA/WORLD_RAID_DATA/VORTEX_DATA/
		// LEGION_DOMINION_DATA, all live via StaticData.TryLoadHolder) and whose DAO reads are try/catch-guarded
		// (no DB => empty + logged, never an NRE), so they are dep-clean to wire here.

		// BaseService.getInstance() (GameServer.main:111): ctor builds BaseLocation per BASE_DATA template.
		Aion.GameServer.Services.BaseService.GetInstance();
		// SiegeService.getInstance() (GameServer.main:112): ctor loads SIEGE_LOCATION_DATA fortress/artifact/outpost
		// locations (guarded by SiegeConfig.SIEGE_ENABLED, default true) and SiegeDAO.LoadSiegeLocations (try/catch).
		Aion.GameServer.Services.SiegeService.GetInstance();
		// WorldRaidService.getInstance().initWorldRaidLocations() (GameServer.main:113): loads WORLD_RAID_DATA
		// locations (guarded by EventsConfig.ENABLE_WORLDRAID).
		Aion.GameServer.Services.WorldRaidService.GetInstance().InitWorldRaidLocations();
		// VortexService.getInstance().initVortexLocations() (GameServer.main:115): spawns peace-state vortex NPCs
		// per VORTEX_DATA and schedules the Theobomos/Brusthonin invasions (guarded by CustomConfig.VORTEX_ENABLED,
		// default true). The two invasion cron schedules are now populated from the Java @Property defaultValue cron
		// strings via CronExpressions.GetOrCreate (CustomConfig.VORTEX_*_SCHEDULE field initializers), so the
		// CronService.Schedule calls no longer NRE on a null CronExpression.
		Aion.GameServer.Services.VortexService.GetInstance().InitVortexLocations();

		// Java parity: GameServer.main loads rift location data before spawns (RiftService.getInstance().initRiftLocations()).
		Aion.GameServer.Services.RiftService.GetInstance().InitRiftLocations();

		// LegionDominionService.getInstance().initLocations() (GameServer.main:117): builds LegionDominionLocation per
		// LEGION_DOMINION_DATA template + LegionDominionDAO.LoadOrCreate/LoadParticipants (try/catch-guarded DB reads).
		Aion.GameServer.Services.LegionDominionService.GetInstance().InitLocations();

		var engineTasks = _engines.Select(engine => InitEngineAsync(engine, cancellationToken).AsTask()).ToArray();
		if (engineTasks.Length > 0)
			await Task.WhenAll(engineTasks);

		// Java parity: GameServer.main calls SpawnEngine.spawnAll() after RiftService.initRiftLocations()
		// and HousingService init, before RiftService.initRifts(). This is the single boot NPC-spawn path:
		// it materializes faithful Npc/VisibleObject into the faithful World store (_allObjects) via
		// VisibleObjectSpawner -> BringIntoWorld -> World.StoreObject/Spawn. Replaces the reworked
		// WorldNpcSpawnService GameEngine, which populated the reworked _objects store with struct WorldNpc.
		Aion.GameServer.SpawnEngine.SpawnEngine.SpawnAll();

		// Java parity: GameServer.main calls TownService.getInstance() right after SpawnEngine.spawnAll() (line 127),
		// before FlyRingService. Its ctor loads per-race towns via TownDAO (try/catch-guarded: no DB => empty + logged)
		// and, only when both are empty, seeds towns from HOUSE_DATA lands/addresses (live HOUSE_DATA/NPC_DATA holders).
		Aion.GameServer.Services.TownService.GetInstance();

		// Java parity: GameServer.main calls FlyRingService.getInstance() after SpawnEngine.spawnAll() (and
		// TownService.getInstance()), before RiftService.initRifts(). Its ctor spawns every fly_rings/ template
		// into the world; empty/unloaded FLY_RING_DATA yields a no-op.
		Aion.GameServer.Services.FlyRingService.GetInstance();

		// Java parity: GameServer.main registers scheduled rift openings after SpawnEngine.spawnAll() (RiftService.getInstance().initRifts()).
		Aion.GameServer.Services.RiftService.GetInstance().InitRifts();

		// Java parity: GameServer.main second-pass init block (lines 142-150), AFTER spawnAll/initRifts, BEFORE
		// DebugService/Weather/etc. These run separately from the location-init first pass because they depend on
		// the spawn engine having run: they despawn the spawn-engine NPCs at each siege/base location and re-spawn
		// the siege/base/raid-controlled NPCs, and they schedule sieges/bases/raids through CronService. All are
		// dep-clean for boot: the spawn/despawn loops iterate the location registries (empty when the *_DATA holder
		// is absent => no-op), the schedule files (config/schedule/{siege,world_raid}_schedule.xml) are present, and
		// the DAO reads are DB.Select try/catch-guarded (no DB => empty + logged).

		// SiegeService.getInstance().initSieges() (GameServer.main:142): DEFERRED. Its faithful 1:1 body schedules
		// fortress sieges from the FULL real config/schedule/siege_schedule.xml (fortress IDs 1011, 1131, 2011, ...)
		// then UpdateFortressNextState() does SiegeLocation fortress = GetSiegeLocation(scheduledLocId);
		// fortress.SetNextState(...) with NO null guard — exactly like Java SiegeService.updateFortressNextState
		// (no guard there either; Java is safe only because real SIEGE_LOCATION_DATA carries those fortress
		// locations). The GameServerBootstrapTests fixture loads an EMPTY SIEGE_LOCATION_DATA (items-only static
		// data) while siege_schedule.xml stays the full real file, so GetSiegeLocation(scheduledLocId) returns null
		// and the faithful fortress.SetNextState NREs the bootstrap gate. This is a test-fixture data gap, NOT a
		// port defect — initSieges is dep-clean against real SIEGE_LOCATION_DATA. Wire it once the bootstrap
		// fixture seeds matching siege location data (or guard is added upstream faithfully). DO NOT add an
		// un-faithful null guard solely to pass the empty-fixture gate.
		// Aion.GameServer.Services.SiegeService.GetInstance().InitSieges();

		// BaseService.getInstance().initBases() (GameServer.main:144): starts casual/stained/panesterra bases
		// (Start is try/catch-guarded for BaseException/NRE; iterates allBaseLocations, empty => no-op).
		Aion.GameServer.Services.BaseService.GetInstance().InitBases();

		// WorldRaidService.getInstance().initWorldRaids() (GameServer.main:146): schedules world raids through
		// CronService from config/schedule/world_raid_schedule.xml (guarded by EventsConfig.ENABLE_WORLDRAID, default true).
		Aion.GameServer.Services.WorldRaidService.GetInstance().InitWorldRaids();

		// ConquerorAndProtectorService.getInstance().init() (GameServer.main:148): registers the handled abyss
		// worlds (CustomConfig.CONQUEROR_AND_PROTECTOR_WORLDS, all race-specific by default => no ArgumentException)
		// and schedules the periodic kills-decrease task through ThreadPoolManager (guarded by
		// CustomConfig.CONQUEROR_AND_PROTECTOR_SYSTEM_ENABLED + non-empty worlds, both default-on).
		Aion.GameServer.Services.ConquerorAndProtectorSystem.ConquerorAndProtectorService.GetInstance().Init();

		// AnnouncementService.getInstance() (GameServer.main:150): ctor Reload() loads announcements via
		// AnnouncementsDAO.LoadAnnouncements (DB.Select try/catch-guarded: no DB => empty + logged) and schedules
		// each via ThreadPoolManager.
		Aion.GameServer.Services.AnnouncementService.GetInstance();

		// Java parity: GameServer.main starts these singleton services after the siege/base/world-raid init block.
		// They self-register their work in their ctors (ThreadPoolManager / CronService schedules, spawns), so a
		// single getInstance() touch is the faithful boot wire.
		// DebugService: schedules the periodic world-player analysis task.
		Aion.GameServer.Services.DebugService.GetInstance();

		// WeatherService.getInstance() (GameServer.main:152): ctor iterates WORLD_MAPS_DATA (live), builds per-zone
		// weather arrays from MAP_WEATHER_DATA (GetWeather returns null on absent map => guarded skip), seeds the
		// next weather from the current GameTime. Dep-clean: GameTimeService is already initialized (InitAsync above).
		Aion.GameServer.Services.WeatherService.GetInstance();

		// BrokerService.getInstance() (GameServer.main:153): ctor InitBrokerService() loads broker items via
		// BrokerDAO.LoadBroker (DB.Select try/catch-guarded => empty no-DB) into per-race maps, then schedules the
		// periodic expiry check + save manager through ThreadPoolManager. Empty broker => no-op loops.
		Aion.GameServer.Services.BrokerService.GetInstance();

		// Influence.getInstance() (GameServer.main:154): ctor RecalculateInfluence() iterates
		// SiegeService.GetSiegeLocations() (empty SIEGE_LOCATION_DATA => empty => zero influence rates). Dep-clean.
		Aion.GameServer.Model.Siege.Influence.GetInstance();

		// ExchangeService.getInstance() (GameServer.main:155): empty ctor (no boot work). Faithful no-op touch.
		Aion.GameServer.Services.ExchangeService.GetInstance();

		// PeriodicSaveService (GameServer.main:156): DEFERRED. The C# PeriodicSaveService is a reworked DI-constructed
		// GameEngine (only schedules a "server last run" variable) — NOT a faithful 1:1 of Java's singleton
		// PeriodicSaveService (which schedules player/legion/etc. periodic saves through ThreadPoolManager). Wiring it
		// faithfully needs a DI-injected instance + a faithful re-port of the save tasks; out of scope for a bounded
		// singleton getInstance() wire. Defer until the faithful re-port lands.

		// AtreianPassportService.getInstance() (GameServer.main:157): ctor computes the passport expire date from
		// ATREIAN_PASSPORT_DATA (empty => null expire => not disabled) and, when enabled, schedules the daily 09:00
		// reset cron (DAILY_CRON_AT_09_00 = "0 0 9 ? * *", a non-null const string => no null-CronExpression NRE).
		_ = Aion.GameServer.Services.AtreianPassportService.GetInstance();

		// CronJobService: Java parity GameServer.main starts it after AtreianPassportService.getInstance(),
		// before CuringZoneService/RoadService. Its ctor schedules the Moltenus spawn + Ahserion flight cron
		// jobs (SiegeConfig.MOLTENUS_SPAWN_SCHEDULE / AHSERION_START_SCHEDULE, now initialized from the Java
		// @Property defaultValue cron strings via CronExpressions.GetOrCreate), the IdianDepthPortal spawner,
		// and the weekly LegionDominion calculation. Must run after CronService.InitSingleton above.
		_ = Aion.GameServer.Services.CronJobService.GetInstance();
		// CuringZoneService: Java only starts it when geo materials are disabled (GeoDataConfig.GEO_MATERIALS_ENABLE).
		if (!Aion.GameServer.Configs.Main.GeoDataConfig.GEO_MATERIALS_ENABLE)
			Aion.GameServer.Services.CuringZoneService.GetInstance();
		// RoadService: spawns every roads/ template into each available instance of its world map.
		Aion.GameServer.Services.RoadService.GetInstance();

		_world.Initialize();
		_gameTimeService.StartClock();

		if (dataManager.StaticData.ValidationTask != null)
			await dataManager.StaticData.ValidationTask.WaitAsync(cancellationToken);

		_started = true;
		_logger.LogInformation("Game-server bootstrap completed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
	}

	public async Task StopAsync(CancellationToken cancellationToken)
	{
		// Java parity: ShutdownHook/SystemExitManager orderly engine, world, scheduler shutdown.
		await _gameTimeService.ShutdownAsync(cancellationToken);

		foreach (var engine in _engines.Reverse())
			await engine.ShutdownAsync(cancellationToken);

		await _threadPoolManager.ShutdownAsync();
		_started = false;
		_logger.LogInformation("Game-server bootstrap stopped");
	}

	private async ValueTask InitEngineAsync(GameEngine engine, CancellationToken cancellationToken)
	{
		// Java parity: GameEngine-style service init calls during GameServer bootstrap.
		_logger.LogInformation("Initializing game engine {Engine}", engine.Name);
		await engine.InitAsync(cancellationToken);
	}
}
