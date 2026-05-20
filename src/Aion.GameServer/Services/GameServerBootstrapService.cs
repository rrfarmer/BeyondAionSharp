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
		_logger.LogInformation(
			"Static data cache loaded from {CacheFile}; {Count} XML files imported",
			dataManager.StaticData.CacheFilePath,
			dataManager.StaticData.ImportedFileCount);

		var engineTasks = _engines.Select(engine => InitEngineAsync(engine, cancellationToken).AsTask()).ToArray();
		if (engineTasks.Length > 0)
			await Task.WhenAll(engineTasks);

		_world.Initialize();
		await _gameTimeService.InitAsync(cancellationToken);
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

		_world.Clear();
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
