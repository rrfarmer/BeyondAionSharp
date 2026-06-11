using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.Services;

public sealed class ShutdownHook
{
	private const int UnsetDelay = int.MinValue;
	private readonly IHostApplicationLifetime _applicationLifetime;
	private readonly ILogger<ShutdownHook> _logger;
	private readonly TimeSpan _tickInterval;
	private int _remainingSeconds = UnsetDelay;
	private int _exitCode;
	private Task? _shutdownTask;

	public ShutdownHook(IHostApplicationLifetime applicationLifetime, ILogger<ShutdownHook> logger)
		: this(applicationLifetime, logger, TimeSpan.FromSeconds(1))
	{
	}

	public ShutdownHook(IHostApplicationLifetime applicationLifetime, ILogger<ShutdownHook> logger, TimeSpan tickInterval)
	{
		_applicationLifetime = applicationLifetime;
		_logger = logger;
		_tickInterval = tickInterval;
		_instance = this;
	}

	// Singleton-bridge slot (same idiom as ThreadPoolManager): the DI-created instance binds the Java-style
	// static accessor so GameServer.isShuttingDownSoon()/isShutdownScheduled() can reach it as in Java.
	private static ShutdownHook? _instance;

	// Java parity: ShutdownHook.getInstance().
	public static ShutdownHook? Instance => _instance;

	public bool IsRunning => Volatile.Read(ref _remainingSeconds) != UnsetDelay;

	public int RemainingSeconds => Volatile.Read(ref _remainingSeconds);

	public int ExitCode => Volatile.Read(ref _exitCode);

	public void InitShutdown(int exitCode, int delaySeconds)
	{
		// Java parity: ShutdownHook/SystemExitManager delayed shutdown countdown.
		if (delaySeconds < 0)
			return;

		Volatile.Write(ref _exitCode, exitCode);
		var previousValue = Interlocked.CompareExchange(ref _remainingSeconds, delaySeconds, UnsetDelay);
		if (previousValue == UnsetDelay)
		{
			_shutdownTask = Task.Run(CountdownAndStopAsync);
			return;
		}

		if (previousValue > 1)
			Volatile.Write(ref _remainingSeconds, delaySeconds);
	}

	private async Task CountdownAndStopAsync()
	{
		while (RemainingSeconds > 0)
		{
			_logger.LogInformation("Runtime is shutting down in {Seconds} seconds.", RemainingSeconds);
			await Task.Delay(_tickInterval);
			Interlocked.Decrement(ref _remainingSeconds);
		}

		_applicationLifetime.StopApplication();
	}
}
