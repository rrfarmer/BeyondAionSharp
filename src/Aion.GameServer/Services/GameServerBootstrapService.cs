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

		// Java parity: GameServer.main loads rift location data before spawns (RiftService.getInstance().initRiftLocations()).
		Aion.GameServer.Services.RiftService.GetInstance().InitRiftLocations();

		var engineTasks = _engines.Select(engine => InitEngineAsync(engine, cancellationToken).AsTask()).ToArray();
		if (engineTasks.Length > 0)
			await Task.WhenAll(engineTasks);

		// Java parity: GameServer.main calls SpawnEngine.spawnAll() after RiftService.initRiftLocations()
		// and HousingService init, before RiftService.initRifts(). This is the single boot NPC-spawn path:
		// it materializes faithful Npc/VisibleObject into the faithful World store (_allObjects) via
		// VisibleObjectSpawner -> BringIntoWorld -> World.StoreObject/Spawn. Replaces the reworked
		// WorldNpcSpawnService GameEngine, which populated the reworked _objects store with struct WorldNpc.
		Aion.GameServer.SpawnEngine.SpawnEngine.SpawnAll();

		// Java parity: GameServer.main initializes the CronService singleton during early utility init
		// (CronService.initSingleton(ThreadPoolManagerRunnableRunner.class, TimeZone.getTimeZone(GSConfig.TIME_ZONE_ID)))
		// before RiftService.initRifts() schedules rift openings through it. Guard the once-only init so a
		// re-entrant boot (e.g. test host running StartAsync repeatedly in one process) doesn't throw "already initialized".
		if (Aion.GameServer.Services.Cron.CronService.GetInstance() == null)
			Aion.GameServer.Services.Cron.CronService.InitSingleton(
				typeof(Aion.GameServer.Utils.Cron.ThreadPoolManagerRunnableRunner),
				Aion.GameServer.Configs.Main.GSConfig.TIME_ZONE_ID ?? System.TimeZoneInfo.Local);

		// Java parity: GameServer.main registers scheduled rift openings after SpawnEngine.spawnAll() (RiftService.getInstance().initRifts()).
		Aion.GameServer.Services.RiftService.GetInstance().InitRifts();

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
