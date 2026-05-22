using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.Services;

public sealed class RiftScheduleService : GameEngine
{
	private readonly RiftService _riftService;
	private readonly RiftInformerService _riftInformer;
	private readonly ThreadPoolManager _threadPoolManager;
	private readonly GameServerOptions _options;
	private readonly ILogger<RiftScheduleService> _logger;
	private readonly Func<DateTimeOffset> _clock;
	private readonly string? _scheduleFilePath;
	private readonly object _syncRoot = new();
	private readonly List<ScheduledTask> _scheduledTasks = [];
	private readonly List<RiftScheduledOpening> _openings = [];
	private CancellationTokenSource? _lifetimeTokenSource;

	public RiftScheduleService(
		RiftService riftService,
		RiftInformerService riftInformer,
		ThreadPoolManager threadPoolManager,
		GameServerOptions? options = null,
		ILogger<RiftScheduleService>? logger = null,
		Func<DateTimeOffset>? clock = null,
		string? scheduleFilePath = null)
	{
		_riftService = riftService;
		_riftInformer = riftInformer;
		_threadPoolManager = threadPoolManager;
		_options = options ?? new GameServerOptions();
		_logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<RiftScheduleService>.Instance;
		_clock = clock ?? (() => DateTimeOffset.Now);
		_scheduleFilePath = scheduleFilePath;
	}

	public string Name => "RiftScheduleService";

	public IReadOnlyList<RiftScheduledOpening> Openings
	{
		get
		{
			lock (_syncRoot)
				return _openings.ToArray();
		}
	}

	public int ScheduledTaskCount
	{
		get
		{
			lock (_syncRoot)
				return _scheduledTasks.Count;
		}
	}

	public ValueTask InitAsync(CancellationToken cancellationToken = default)
	{
		// Java parity: services/RiftService.initRifts loads RiftSchedule and registers one RiftOpenRunnable per <open>.
		if (!_options.Custom.RiftEnabled)
			return ValueTask.CompletedTask;

		var schedulePath = ResolveSchedulePath();
		if (schedulePath == null || !File.Exists(schedulePath))
		{
			_logger.LogWarning("Rift schedule file was not found; scheduled rift openings are disabled.");
			return ValueTask.CompletedTask;
		}

		_lifetimeTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		var schedule = RiftScheduleTable.LoadFromFile(schedulePath);
		foreach (var entry in schedule.Entries)
		{
			if (!JavaQuartzCronExpression.TryParse(entry.ScheduleExpression, out var cronExpression))
			{
				_logger.LogWarning(
					"Skipping unsupported rift schedule {Schedule} for world {WorldId}",
					entry.ScheduleExpression,
					entry.WorldId);
				continue;
			}

			var opening = new RiftScheduledOpening(entry.WorldId, entry.SpawnGuards, entry.ScheduleExpression, cronExpression);
			lock (_syncRoot)
				_openings.Add(opening);
			ScheduleOpening(opening);
		}

		_logger.LogInformation("Registered {Count} scheduled rift openings", Openings.Count);
		return ValueTask.CompletedTask;
	}

	public ValueTask ShutdownAsync(CancellationToken cancellationToken = default)
	{
		// Java parity: CronService shutdown cancels registered rift cron jobs during game-server stop.
		_lifetimeTokenSource?.Cancel();
		lock (_syncRoot)
		{
			foreach (var task in _scheduledTasks)
				task.Cancel();
			_scheduledTasks.Clear();
			_openings.Clear();
		}
		_lifetimeTokenSource?.Dispose();
		_lifetimeTokenSource = null;
		return ValueTask.CompletedTask;
	}

	public async ValueTask RunOpeningAsync(RiftScheduledOpening opening, CancellationToken cancellationToken = default)
	{
		// Java parity: services/rift/RiftOpenRunnable.run opens, schedules autoclose, then broadcasts rift info.
		_riftService.PrepareRiftOpening(opening.WorldId, opening.SpawnGuards);
		var closeTask = _threadPoolManager.Schedule(
			_ =>
			{
				_riftService.CloseAutoCloseableRifts(opening.WorldId);
				return ValueTask.CompletedTask;
			},
			GetAutoCloseDelay(),
			cancellationToken);
		TrackScheduledTask(closeTask);
		await _riftInformer.SendRiftsInfoAsync(opening.WorldId).ConfigureAwait(false);
	}

	private void ScheduleOpening(RiftScheduledOpening opening)
	{
		var lifetimeToken = _lifetimeTokenSource?.Token ?? CancellationToken.None;
		if (lifetimeToken.IsCancellationRequested)
			return;

		var now = _clock();
		var nextRun = opening.CronExpression.GetNextRunAfter(now);
		var delay = nextRun - now;
		if (delay < TimeSpan.Zero)
			delay = TimeSpan.Zero;

		var task = _threadPoolManager.Schedule(
			async token =>
			{
				await RunOpeningAsync(opening, token).ConfigureAwait(false);
				if (!lifetimeToken.IsCancellationRequested)
					ScheduleOpening(opening);
			},
			delay,
			lifetimeToken);
		TrackScheduledTask(task);
	}

	private void TrackScheduledTask(ScheduledTask task)
	{
		lock (_syncRoot)
			_scheduledTasks.Add(task);
		_ = task.Completion.ContinueWith(
			_ =>
			{
				lock (_syncRoot)
					_scheduledTasks.Remove(task);
			},
			CancellationToken.None,
			TaskContinuationOptions.ExecuteSynchronously,
			TaskScheduler.Default);
	}

	private TimeSpan GetAutoCloseDelay()
	{
		return TimeSpan.FromMilliseconds(_options.Custom.RiftDuration * 3540 * 1000);
	}

	private string? ResolveSchedulePath()
	{
		if (!string.IsNullOrWhiteSpace(_scheduleFilePath))
			return _scheduleFilePath;

		var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
		return repoRoot == null
			? null
			: Path.Combine(repoRoot, "game-server", "config", "schedule", "rift_schedule.xml");
	}

	private static string? FindRepoRoot(string startDirectory)
	{
		var directory = new DirectoryInfo(startDirectory);
		while (directory != null)
		{
			if (File.Exists(Path.Combine(directory.FullName, "game-server", "config", "schedule", "rift_schedule.xml")))
				return directory.FullName;
			directory = directory.Parent;
		}

		return null;
	}
}

public sealed record RiftScheduledOpening(
	int WorldId,
	bool SpawnGuards,
	string ScheduleExpression,
	JavaQuartzCronExpression CronExpression);
