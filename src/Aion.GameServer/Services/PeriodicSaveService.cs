using Aion.GameServer.Data;
using Aion.GameServer.Model;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.Services;

public sealed class PeriodicSaveService : GameEngine
{
	private const string ServerLastRunVariable = "serverLastRun";
	private static readonly TimeSpan DefaultServerRuntimeDelay = TimeSpan.FromMinutes(2);
	private static readonly TimeSpan DefaultServerRuntimePeriod = TimeSpan.FromMinutes(2);
	private readonly IServerVariablesRepository _serverVariablesRepository;
	private readonly ThreadPoolManager _threadPoolManager;
	private readonly ILogger<PeriodicSaveService> _logger;
	private readonly TimeSpan _serverRuntimeDelay;
	private readonly TimeSpan _serverRuntimePeriod;
	private int _isInitialized;
	private Task? _serverRuntimeTask;

	public PeriodicSaveService(
		IServerVariablesRepository serverVariablesRepository,
		ThreadPoolManager threadPoolManager,
		ILogger<PeriodicSaveService> logger)
		: this(serverVariablesRepository, threadPoolManager, logger, DefaultServerRuntimeDelay, DefaultServerRuntimePeriod)
	{
	}

	public PeriodicSaveService(
		IServerVariablesRepository serverVariablesRepository,
		ThreadPoolManager threadPoolManager,
		ILogger<PeriodicSaveService> logger,
		TimeSpan serverRuntimeDelay,
		TimeSpan serverRuntimePeriod)
	{
		_serverVariablesRepository = serverVariablesRepository;
		_threadPoolManager = threadPoolManager;
		_logger = logger;
		_serverRuntimeDelay = serverRuntimeDelay;
		_serverRuntimePeriod = serverRuntimePeriod;
	}

	public string Name => "PeriodicSaveService";

	public ValueTask InitAsync(CancellationToken cancellationToken = default)
	{
		// Java parity: services/PeriodicSaveService constructor schedules ServerRunTimeSaveTask.
		if (Interlocked.Exchange(ref _isInitialized, 1) == 0)
		{
			_serverRuntimeTask = _threadPoolManager.ScheduleAtFixedRate(
				async token => await StoreServerLastRunAsync(token),
				_serverRuntimeDelay,
				_serverRuntimePeriod,
				cancellationToken);
		}

		return ValueTask.CompletedTask;
	}

	public async ValueTask ShutdownAsync(CancellationToken cancellationToken = default)
	{
		// Java parity: services/PeriodicSaveService.onShutdown -> PeriodicSaveTask.storeDataAndCancel.
		_logger.LogInformation("Starting data save on shutdown.");
		await StoreServerLastRunAsync(cancellationToken);
		_logger.LogInformation("Data successfully saved.");
	}

	public Task<bool> StoreServerLastRunAsync(CancellationToken cancellationToken = default)
	{
		// Java parity: PeriodicSaveService.ServerRunTimeSaveTask.run.
		return _serverVariablesRepository.StoreAsync(
			ServerLastRunVariable,
			DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
			cancellationToken);
	}
}
