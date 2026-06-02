using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.Utils;

public sealed class ThreadPoolManager : IAsyncDisposable
{
	private readonly ILogger<ThreadPoolManager> _logger;
	private readonly Action<ThreadPoolScheduleObservation>? _scheduleObserver;
	private readonly ConcurrentBag<Task> _scheduledTasks = new();
	private readonly CancellationTokenSource _shutdownTokenSource = new();
	private int _isShutdown;

	public ThreadPoolManager(
		ILogger<ThreadPoolManager> logger,
		Action<ThreadPoolScheduleObservation>? scheduleObserver = null)
	{
		_logger = logger;
		_scheduleObserver = scheduleObserver;
	}

	public ScheduledTask Schedule(
		Func<CancellationToken, ValueTask> action,
		TimeSpan delay,
		CancellationToken cancellationToken = default)
	{
		// Java parity: utils/ThreadPoolManager.schedule.
		if (Volatile.Read(ref _isShutdown) != 0)
			throw new InvalidOperationException("ThreadPoolManager is shut down.");

		var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_shutdownTokenSource.Token, cancellationToken);
		var task = Task.Run(() => RunOnceAsync(action, delay, linkedTokenSource.Token), CancellationToken.None);
		_scheduledTasks.Add(task);
		_scheduleObserver?.Invoke(new ThreadPoolScheduleObservation(ThreadPoolScheduleKind.Once, delay, Period: null));
		return new ScheduledTask(task, linkedTokenSource);
	}

	public Task ScheduleAtFixedRate(
		Func<CancellationToken, ValueTask> action,
		TimeSpan initialDelay,
		TimeSpan period,
		CancellationToken cancellationToken = default)
	{
		return ScheduleAtFixedRateTask(action, initialDelay, period, cancellationToken).Completion;
	}

	public ScheduledTask ScheduleAtFixedRateTask(
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
		_scheduleObserver?.Invoke(new ThreadPoolScheduleObservation(ThreadPoolScheduleKind.FixedRate, initialDelay, period));
		return new ScheduledTask(task, linkedTokenSource);
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

	private async Task RunOnceAsync(
		Func<CancellationToken, ValueTask> action,
		TimeSpan delay,
		CancellationToken cancellationToken)
	{
		try
		{
			if (delay > TimeSpan.Zero)
				await Task.Delay(delay, cancellationToken);

			if (!cancellationToken.IsCancellationRequested)
				await action(cancellationToken);
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

public sealed record ThreadPoolScheduleObservation(
	ThreadPoolScheduleKind Kind,
	TimeSpan Delay,
	TimeSpan? Period);

public enum ThreadPoolScheduleKind
{
	Once,
	FixedRate,
}

public sealed class ScheduledTask
{
	private readonly CancellationTokenSource _cancellationTokenSource;
	private int _isComplete;

	internal ScheduledTask(Task completion, CancellationTokenSource cancellationTokenSource)
	{
		Completion = completion;
		_cancellationTokenSource = cancellationTokenSource;
		_ = completion.ContinueWith(
			_ =>
			{
				Interlocked.Exchange(ref _isComplete, 1);
				_cancellationTokenSource.Dispose();
			},
			CancellationToken.None,
			TaskContinuationOptions.ExecuteSynchronously,
			TaskScheduler.Default);
	}

	public Task Completion { get; }

	public bool Cancel()
	{
		if (Volatile.Read(ref _isComplete) != 0)
			return false;

		try
		{
			_cancellationTokenSource.Cancel();
			return true;
		}
		catch (ObjectDisposedException)
		{
			return false;
		}
	}
}
