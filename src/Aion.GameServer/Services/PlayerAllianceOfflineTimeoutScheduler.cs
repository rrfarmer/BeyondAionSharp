using Aion.GameServer.Configuration;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Services;

public sealed class PlayerAllianceOfflineTimeoutScheduler(
	ThreadPoolManager threadPoolManager,
	PlayerAllianceOfflineTimeoutDispatchService dispatchService,
	GameServerOptions options,
	TimeProvider? timeProvider = null)
{
	public static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(1);

	public static readonly TimeSpan Period = TimeSpan.FromSeconds(30);

	private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

	public ScheduledTask Start(CancellationToken cancellationToken = default)
	{
		// Java parity: model/team/alliance/PlayerAllianceService.initializeOfflineCheck schedules
		// OfflinePlayerAllianceChecker with delay 1000 ms and period 30 * 1000 ms.
		return threadPoolManager.ScheduleAtFixedRateTask(
			async token => await RunScanOnceAsync(token),
			InitialDelay,
			Period,
			cancellationToken);
	}

	public async ValueTask<PlayerAllianceOfflineTimeoutScanResult> RunScanOnceAsync(
		CancellationToken cancellationToken = default)
	{
		// Java parity: OfflinePlayerAllianceChecker.run uses GroupConfig.ALLIANCE_REMOVE_TIME for
		// normal alliances; auto alliances still resolve to the Java hard-coded 60 seconds in runtime.
		return await dispatchService.DispatchExpiredScanAsync(
			_timeProvider.GetUtcNow(),
			options.Group.AllianceRemoveTimeSeconds,
			cancellationToken);
	}
}
