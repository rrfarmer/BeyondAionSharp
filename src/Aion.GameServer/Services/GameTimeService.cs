using Aion.GameServer.Model;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.Services;

public sealed class GameTimeService : GameEngine
{
	private static readonly TimeSpan DefaultTickDelay = TimeSpan.FromSeconds(5);
	private static readonly TimeSpan DefaultTickPeriod = TimeSpan.FromSeconds(5);
	private readonly ILogger<GameTimeService> _logger;
	private readonly ThreadPoolManager _threadPoolManager;
	private readonly TimeSpan _tickDelay;
	private readonly TimeSpan _tickPeriod;
	private int _isInitialized;
	private int _isStarted;
	private int _gameMinutes;
	private Task? _clockTask;

	public GameTimeService(ILogger<GameTimeService> logger, ThreadPoolManager threadPoolManager)
		: this(logger, threadPoolManager, DefaultTickDelay, DefaultTickPeriod)
	{
	}

	public GameTimeService(ILogger<GameTimeService> logger, ThreadPoolManager threadPoolManager, TimeSpan tickDelay, TimeSpan tickPeriod)
	{
		_logger = logger;
		_threadPoolManager = threadPoolManager;
		_tickDelay = tickDelay;
		_tickPeriod = tickPeriod;
	}

	public string Name => "GameTimeService";

	public bool IsStarted => Volatile.Read(ref _isStarted) != 0;

	public int GameMinutes => Volatile.Read(ref _gameMinutes);

	public ValueTask InitAsync(CancellationToken cancellationToken = default)
	{
		// Java parity: services/GameTimeService init during GameServer bootstrap.
		if (Interlocked.Exchange(ref _isInitialized, 1) == 0)
			_logger.LogInformation("Initialized GameTime");
		return ValueTask.CompletedTask;
	}

	public void StartClock()
	{
		// Java parity: GameTimeService starts periodic game-time update task.
		if (Interlocked.Exchange(ref _isStarted, 1) != 0)
			throw new InvalidOperationException("Tried to start game time twice.");

		_clockTask = _threadPoolManager.ScheduleAtFixedRate(
			_ =>
			{
				Interlocked.Increment(ref _gameMinutes);
				return ValueTask.CompletedTask;
			},
			_tickDelay,
			_tickPeriod);
		_logger.LogInformation("GameTime started. Update interval: {Seconds}s", (int)_tickPeriod.TotalSeconds);
	}

	public ValueTask ShutdownAsync(CancellationToken cancellationToken = default)
	{
		Volatile.Write(ref _isStarted, 0);
		return ValueTask.CompletedTask;
	}
}
