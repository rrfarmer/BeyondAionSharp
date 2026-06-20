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

	// Singleton-bridge slot (see docs/HANDOFF.md "SINGLETON-BRIDGE"): the instance is created via DI;
	// the composition root calls RegisterInstance(...) once at startup so per-instance domain objects
	// (Creature, AggroList, life/game stats, ...) can reach it exactly as Java's getInstance() does.
	private static ThreadPoolManager? _instance;

	public ThreadPoolManager(
		ILogger<ThreadPoolManager> logger,
		Action<ThreadPoolScheduleObservation>? scheduleObserver = null)
	{
		_logger = logger;
		_scheduleObserver = scheduleObserver;
	}

	// Java parity: ThreadPoolManager.getInstance().
	public static ThreadPoolManager GetInstance() =>
		_instance ?? throw new InvalidOperationException("ThreadPoolManager singleton bridge not initialized; call RegisterInstance(...) at startup.");

	/// <summary>Composition-root hook: bind the DI-created instance to the Java-style static accessor.</summary>
	public static void RegisterInstance(ThreadPoolManager instance) => _instance = instance;

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
		return new ScheduledTask(task, linkedTokenSource, DateTimeOffset.UtcNow + delay);
	}

	/// <summary>
	/// Java parity: <c>new FutureTask&lt;&gt;(runnable, null)</c> stored but NOT submitted to any executor.
	/// The body does not run until <see cref="ScheduledTask.Run"/> is called (e.g. from CM_TELEPORT_ANIMATION_DONE
	/// once the client reports the teleport fade-out animation finished). This is distinct from <see cref="Schedule"/>
	/// (which starts the body on the pool immediately): scheduling the spawn-in eagerly would push the new-map packets
	/// before the client is ready, breaking cross-map / instance teleports.
	/// </summary>
	public ScheduledTask Deferred(Action body) => new ScheduledTask(new Task(body));

	public Task ScheduleAtFixedRate(
		Func<CancellationToken, ValueTask> action,
		TimeSpan initialDelay,
		TimeSpan period,
		CancellationToken cancellationToken = default)
	{
		return ScheduleAtFixedRateTask(action, initialDelay, period, cancellationToken).Completion;
	}

	// Java parity: ThreadPoolManager.execute(Runnable) - run on the pool immediately (zero delay).
	public void Execute(Func<CancellationToken, ValueTask> action) => Schedule(action, TimeSpan.Zero);

	public void Execute(Action action) => Schedule(_ => { action(); return ValueTask.CompletedTask; }, TimeSpan.Zero);

	public void Execute(Runnable runnable) => Schedule(_ => { runnable.Run(); return ValueTask.CompletedTask; }, TimeSpan.Zero);

	// Java parity: ThreadPoolManager.executeLongRunning(Runnable). The long-running hint is advisory in C#.
	public void ExecuteLongRunning(Func<CancellationToken, ValueTask> action) => Schedule(action, TimeSpan.Zero);

	public void ExecuteLongRunning(Action action) => Schedule(_ => { action(); return ValueTask.CompletedTask; }, TimeSpan.Zero);

	public void ExecuteLongRunning(Runnable runnable) => Schedule(_ => { runnable.Run(); return ValueTask.CompletedTask; }, TimeSpan.Zero);

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
		return new ScheduledTask(task, linkedTokenSource, DateTimeOffset.UtcNow + initialDelay);
	}

	// Java parity: schedule/scheduleAtFixedRate take long millisecond delays (Runnable or async delegate).
	public ScheduledTask Schedule(Func<CancellationToken, ValueTask> action, long delayMillis) =>
		Schedule(action, TimeSpan.FromMilliseconds(delayMillis));

	public ScheduledTask Schedule(Runnable runnable, long delayMillis) =>
		Schedule(_ => { runnable.Run(); return ValueTask.CompletedTask; }, TimeSpan.FromMilliseconds(delayMillis));

	public Task ScheduleAtFixedRate(Func<CancellationToken, ValueTask> action, long initialDelayMillis, long periodMillis) =>
		ScheduleAtFixedRate(action, TimeSpan.FromMilliseconds(initialDelayMillis), TimeSpan.FromMilliseconds(periodMillis));

	public Task ScheduleAtFixedRate(Runnable runnable, long initialDelayMillis, long periodMillis) =>
		ScheduleAtFixedRate(_ => { runnable.Run(); return ValueTask.CompletedTask; }, TimeSpan.FromMilliseconds(initialDelayMillis), TimeSpan.FromMilliseconds(periodMillis));

	public ScheduledTask ScheduleAtFixedRateTask(Runnable runnable, long initialDelayMillis, long periodMillis) =>
		ScheduleAtFixedRateTask(_ => { runnable.Run(); return ValueTask.CompletedTask; }, TimeSpan.FromMilliseconds(initialDelayMillis), TimeSpan.FromMilliseconds(periodMillis));

	public ScheduledTask Schedule(Action action, long delayMillis) =>
		Schedule(_ => { action(); return ValueTask.CompletedTask; }, TimeSpan.FromMilliseconds(delayMillis));

	public Task ScheduleAtFixedRate(Action action, long initialDelayMillis, long periodMillis) =>
		ScheduleAtFixedRate(_ => { action(); return ValueTask.CompletedTask; }, TimeSpan.FromMilliseconds(initialDelayMillis), TimeSpan.FromMilliseconds(periodMillis));

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

	private readonly DateTimeOffset _dueTimeUtc;

	// Non-null only for deferred (run-on-demand) tasks created via ThreadPoolManager.Deferred. For pool-scheduled
	// tasks this is null and Run() stays a no-op observe marker.
	private readonly Task? _deferredTask;

	internal ScheduledTask(Task completion, CancellationTokenSource cancellationTokenSource, DateTimeOffset dueTimeUtc)
	{
		Completion = completion;
		_cancellationTokenSource = cancellationTokenSource;
		_dueTimeUtc = dueTimeUtc;
		_deferredTask = null;
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

	// Java parity: a stored RunnableFuture (new FutureTask<>(runnable, null)) that has NOT started. Unlike the
	// pool-scheduled ctor, the body runs only when Run() is invoked; IsDone() stays false until then.
	internal ScheduledTask(Task deferredTask)
	{
		_deferredTask = deferredTask;
		Completion = deferredTask;
		_cancellationTokenSource = new CancellationTokenSource();
		_dueTimeUtc = DateTimeOffset.UtcNow;
		_ = deferredTask.ContinueWith(
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

	// Java parity: Future.isCancelled()
	public bool IsCancelled => _cancellationTokenSource.IsCancellationRequested;

	// Java parity: Future.isDone() — true once the task completed (normally, exceptionally, or cancelled).
	public bool IsDone() => Completion.IsCompleted;

	// Java parity: RunnableFuture.run() — execute the task now. For a deferred task (ThreadPoolManager.Deferred)
	// the body has not started yet, so this runs it synchronously on the caller's thread (matching FutureTask.run()
	// invoked from CM_TELEPORT_ANIMATION_DONE). For pool-scheduled tasks the body already ran on the pool, so Run()
	// is a no-op observe marker and the subsequent Get() surfaces the result. RunSynchronously stores any thrown
	// exception on the task (it does not propagate here); Get() re-throws it, mirroring FutureTask.get().
	public void Run()
	{
		Task? deferred = _deferredTask;
		if (deferred != null && deferred.Status == TaskStatus.Created)
			deferred.RunSynchronously();
	}

	// Java parity: Future.get() — block until the task completes, surfacing any execution exception
	// (wrapped, so e.InnerException mirrors ExecutionException.getCause()).
	public void Get()
	{
		Completion.GetAwaiter().GetResult();
	}

	// Java parity: Future.cancel(boolean mayInterruptIfRunning). The flag is advisory; C# cooperative
	// cancellation always signals the token, so we ignore it and defer to the no-arg Cancel().
	public bool Cancel(bool mayInterruptIfRunning) => Cancel();

	// Java parity: java.util.concurrent.Delayed.getDelay(TimeUnit) — remaining time until the task is due (may be <=0 once past due).
	public long GetDelay(TimeUnit unit)
	{
		TimeSpan remaining = _dueTimeUtc - DateTimeOffset.UtcNow;
		return unit.Convert(remaining);
	}

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

/// <summary>Java parity: java.util.concurrent.TimeUnit — the subset used by the port for ScheduledTask.GetDelay conversions.</summary>
public enum TimeUnit
{
	NANOSECONDS,
	MICROSECONDS,
	MILLISECONDS,
	SECONDS,
	MINUTES,
	HOURS,
	DAYS,
}

internal static class TimeUnitExtensions
{
	// Java parity: TimeUnit.convert(Duration) — express a TimeSpan in this unit, truncating toward zero.
	public static long Convert(this TimeUnit unit, TimeSpan duration) => unit switch
	{
		TimeUnit.NANOSECONDS => (long)(duration.Ticks * 100L),
		TimeUnit.MICROSECONDS => duration.Ticks / 10L,
		TimeUnit.MILLISECONDS => (long)duration.TotalMilliseconds,
		TimeUnit.SECONDS => (long)duration.TotalSeconds,
		TimeUnit.MINUTES => (long)duration.TotalMinutes,
		TimeUnit.HOURS => (long)duration.TotalHours,
		TimeUnit.DAYS => (long)duration.TotalDays,
		_ => (long)duration.TotalMilliseconds,
	};
}
