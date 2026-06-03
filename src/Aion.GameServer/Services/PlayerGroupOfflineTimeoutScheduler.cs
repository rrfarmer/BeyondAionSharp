using Aion.GameServer.Configuration;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Services;

public sealed class PlayerGroupOfflineTimeoutScheduler(
	ThreadPoolManager threadPoolManager,
	PlayerGroupOfflineTimeoutDispatchService dispatchService,
	GameServerOptions options,
	TimeProvider? timeProvider = null)
{
	public static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(1);

	public static readonly TimeSpan Period = TimeSpan.FromSeconds(30);

	private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

	public ScheduledTask Start(CancellationToken cancellationToken = default)
	{
		// Java parity: model/team/group/PlayerGroupService.initializeOfflineCheck schedules
		// OfflinePlayerChecker with delay 1000 ms and period 30 * 1000 ms.
		return threadPoolManager.ScheduleAtFixedRateTask(
			async token => await RunScanOnceAsync(token),
			InitialDelay,
			Period,
			cancellationToken);
	}

	public async ValueTask<PlayerGroupOfflineTimeoutScanResult> RunScanOnceAsync(
		CancellationToken cancellationToken = default)
	{
		// Java parity: OfflinePlayerChecker.run uses GroupConfig.GROUP_REMOVE_TIME.
		return await dispatchService.DispatchExpiredScanAsync(
			_timeProvider.GetUtcNow(),
			options.Group.GroupRemoveTimeSeconds,
			cancellationToken);
	}
}
