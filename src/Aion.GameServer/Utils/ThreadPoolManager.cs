using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.Utils;

public sealed class ThreadPoolManager : IAsyncDisposable
{
	private readonly ILogger<ThreadPoolManager> _logger;
	private readonly ConcurrentBag<Task> _scheduledTasks = new();
	private readonly CancellationTokenSource _shutdownTokenSource = new();
	private int _isShutdown;

	public ThreadPoolManager(ILogger<ThreadPoolManager> logger)
	{
		_logger = logger;
	}

	public Task ScheduleAtFixedRate(
		Func<CancellationToken, ValueTask> action,
		TimeSpan initialDelay,
		TimeSpan period,
		CancellationToken cancellationToken = default)
	{
		// Java parity: utils/ThreadPoolManager.scheduleAtFixedRate.
		if (Volatile.Read(ref _isShutdown) != 0)
			throw new InvalidOperationException("ThreadPoolManager is shut down.");

		var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_shutdownTokenSource.Token, cancellationToken);
		var task = Task.Run(() => RunFixedRateAsync(action, initialDelay, period, linkedTokenSource), CancellationToken.None);
		_scheduledTasks.Add(task);
		return task;
	}

	public async Task ShutdownAsync(TimeSpan gracePeriod = default)
	{
		// Java parity: ThreadPoolManager shutdown during game-server stop.
		if (Interlocked.Exchange(ref _isShutdown, 1) != 0)
			return;

		if (gracePeriod == default)
			gracePeriod = TimeSpan.FromSeconds(2);

		_shutdownTokenSource.Cancel();
		var tasks = _scheduledTasks.ToArray();
		if (tasks.Length == 0)
			return;

		await Task.WhenAny(Task.WhenAll(tasks), Task.Delay(gracePeriod));
	}

	private async Task RunFixedRateAsync(
		Func<CancellationToken, ValueTask> action,
		TimeSpan initialDelay,
		TimeSpan period,
		CancellationTokenSource linkedTokenSource)
	{
		using var _ = linkedTokenSource;
		var cancellationToken = linkedTokenSource.Token;
		try
		{
			if (initialDelay > TimeSpan.Zero)
				await Task.Delay(initialDelay, cancellationToken);

			while (!cancellationToken.IsCancellationRequested)
			{
				await action(cancellationToken);
				await Task.Delay(period, cancellationToken);
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Scheduled game-server task failed");
		}
	}

	public async ValueTask DisposeAsync()
	{
		await ShutdownAsync();
		_shutdownTokenSource.Dispose();
	}
}
