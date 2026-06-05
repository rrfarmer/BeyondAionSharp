using Aion.GameServer.Configuration;
using Aion.GameServer.Model;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.Services;

public sealed class LimitedItemTradeSchedulerService : GameEngine
{
	private readonly GameServerRuntimeContext _runtimeContext;
	private readonly ThreadPoolManager _threadPoolManager;
	private readonly GameServerOptions _options;
	private readonly ILogger<LimitedItemTradeSchedulerService> _logger;
	private readonly Func<DateTimeOffset>? _clock;

	public LimitedItemTradeSchedulerService(
		GameServerRuntimeContext runtimeContext,
		ThreadPoolManager threadPoolManager,
		GameServerOptions? options = null,
		ILogger<LimitedItemTradeSchedulerService>? logger = null,
		Func<DateTimeOffset>? clock = null)
	{
		_runtimeContext = runtimeContext;
		_threadPoolManager = threadPoolManager;
		_options = options ?? new GameServerOptions();
		_logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<LimitedItemTradeSchedulerService>.Instance;
		_clock = clock;
	}

	public string Name => "LimitedItemTradeService";

	public ValueTask InitAsync(CancellationToken cancellationToken = default)
	{
		// Java parity: GameServer startup calls LimitedItemTradeService.start after
		// DataManager is loaded and CronService is initialized.
		var result = _runtimeContext.LimitedItems.StartScheduledResets(
			_threadPoolManager,
			_options.Core.GetTimeZone(),
			_clock,
			cancellationToken);
		foreach (var skipped in result.SkippedItems)
		{
			_logger.LogWarning(
				"Skipping limited-item reset schedule {SalesTime} for NPC {NpcId}, item {ItemId}: {Reason}",
				skipped.SalesTime,
				skipped.NpcId,
				skipped.ItemId,
				skipped.Reason);
		}

		_logger.LogInformation("Scheduled {Count} limited-item reset jobs", result.ScheduledCount);
		return ValueTask.CompletedTask;
	}

	public ValueTask ShutdownAsync(CancellationToken cancellationToken = default)
	{
		_runtimeContext.LimitedItems.ShutdownScheduledResets();
		return ValueTask.CompletedTask;
	}
}
