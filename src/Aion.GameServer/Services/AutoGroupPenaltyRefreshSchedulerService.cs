using System.Collections.Concurrent;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Services;

public sealed class AutoGroupPenaltyRefreshSchedulerService
{
	private readonly ConcurrentDictionary<int, byte> _pendingRefreshesByPlayerObjectId = [];
	private readonly ThreadPoolManager _threadPoolManager;
	private readonly PeriodicInstanceRegistrationService _periodicInstanceRegistrations;
	private readonly GameServerRuntimeContext _runtimeContext;
	private readonly Func<DateTimeOffset> _clock;

	public AutoGroupPenaltyRefreshSchedulerService(
		ThreadPoolManager threadPoolManager,
		PeriodicInstanceRegistrationService periodicInstanceRegistrations,
		GameServerRuntimeContext runtimeContext)
		: this(threadPoolManager, periodicInstanceRegistrations, runtimeContext, () => DateTimeOffset.UtcNow)
	{
	}

	public AutoGroupPenaltyRefreshSchedulerService(
		ThreadPoolManager threadPoolManager,
		PeriodicInstanceRegistrationService periodicInstanceRegistrations,
		GameServerRuntimeContext runtimeContext,
		Func<DateTimeOffset> clock)
	{
		_threadPoolManager = threadPoolManager;
		_periodicInstanceRegistrations = periodicInstanceRegistrations;
		_runtimeContext = runtimeContext;
		_clock = clock;
	}

	public AutoGroupPenaltyRefreshScheduleResult ScheduleRefreshes(
		IEnumerable<AutoGroupPenaltyRefreshIntent> intents,
		IGameClientConnectionRegistry connectionRegistry,
		TimeSpan? delayOverride = null)
	{
		var entries = new List<AutoGroupPenaltyRefreshScheduleEntry>();
		foreach (var intent in intents
			.GroupBy(intent => intent.PlayerObjectId)
			.Select(group => group.First()))
		{
			if (!_pendingRefreshesByPlayerObjectId.TryAdd(intent.PlayerObjectId, 0))
			{
				entries.Add(AutoGroupPenaltyRefreshScheduleEntry.AlreadyPending(intent.PlayerObjectId, intent.Delay));
				continue;
			}

			var delay = delayOverride ?? intent.Delay;
			try
			{
				var scheduledTask = _threadPoolManager.Schedule(
					async cancellationToken =>
					{
						try
						{
							await ExecuteRefreshAsync(intent.PlayerObjectId, connectionRegistry, cancellationToken);
						}
						finally
						{
							_pendingRefreshesByPlayerObjectId.TryRemove(intent.PlayerObjectId, out _);
						}
					},
					delay);

				entries.Add(AutoGroupPenaltyRefreshScheduleEntry.Scheduled(intent.PlayerObjectId, delay, scheduledTask));
			}
			catch
			{
				_pendingRefreshesByPlayerObjectId.TryRemove(intent.PlayerObjectId, out _);
				throw;
			}
		}

		return new AutoGroupPenaltyRefreshScheduleResult(
			entries,
			"AutoGroupService.penalisePlayerAndScheduleRemoval -> penalties.add(objectId) dedupes and schedules PeriodicInstanceManager.checkAndSendOpenRegistrations(objectId)");
	}

	public async ValueTask<int> ExecuteRefreshAsync(
		int playerObjectId,
		IGameClientConnectionRegistry connectionRegistry,
		CancellationToken cancellationToken = default)
	{
		var player = TryGetOnlinePlayerByObjectId(playerObjectId, connectionRegistry);
		if (player == null)
			return 0;

		var staticData = _runtimeContext.DataManager?.StaticData;
		var packets = _periodicInstanceRegistrations.CreateOpenRegistrationPackets(
			player,
			staticData?.AutoGroups,
			staticData?.InstanceCooltimes,
			_clock());
		var sentPackets = 0;
		foreach (var packet in packets)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (await connectionRegistry.SendPacketToPlayerAsync(playerObjectId, packet))
				sentPackets++;
		}

		return sentPackets;
	}

	public bool HasPendingRefresh(int playerObjectId)
	{
		return _pendingRefreshesByPlayerObjectId.ContainsKey(playerObjectId);
	}

	private static Player? TryGetOnlinePlayerByObjectId(
		int playerObjectId,
		IGameClientConnectionRegistry connectionRegistry)
	{
		Player? player = null;
		connectionRegistry.ForEachOnlinePlayer(candidate =>
		{
			if (candidate.ObjectId == playerObjectId)
				player = candidate;
		});
		return player;
	}
}

public sealed record AutoGroupPenaltyRefreshScheduleResult(
	IReadOnlyList<AutoGroupPenaltyRefreshScheduleEntry> Entries,
	string JavaSource)
{
	public IReadOnlyList<int> ScheduledPlayerObjectIds =>
		Entries.Where(entry => entry.Status == AutoGroupPenaltyRefreshScheduleStatus.Scheduled)
			.Select(entry => entry.PlayerObjectId)
			.ToArray();

	public IReadOnlyList<int> AlreadyPendingPlayerObjectIds =>
		Entries.Where(entry => entry.Status == AutoGroupPenaltyRefreshScheduleStatus.AlreadyPending)
			.Select(entry => entry.PlayerObjectId)
			.ToArray();
}

public sealed record AutoGroupPenaltyRefreshScheduleEntry(
	int PlayerObjectId,
	TimeSpan Delay,
	AutoGroupPenaltyRefreshScheduleStatus Status,
	ScheduledTask? ScheduledTask)
{
	public static AutoGroupPenaltyRefreshScheduleEntry Scheduled(
		int playerObjectId,
		TimeSpan delay,
		ScheduledTask scheduledTask)
	{
		return new AutoGroupPenaltyRefreshScheduleEntry(
			playerObjectId,
			delay,
			AutoGroupPenaltyRefreshScheduleStatus.Scheduled,
			scheduledTask);
	}

	public static AutoGroupPenaltyRefreshScheduleEntry AlreadyPending(int playerObjectId, TimeSpan delay)
	{
		return new AutoGroupPenaltyRefreshScheduleEntry(
			playerObjectId,
			delay,
			AutoGroupPenaltyRefreshScheduleStatus.AlreadyPending,
			ScheduledTask: null);
	}
}

public enum AutoGroupPenaltyRefreshScheduleStatus
{
	Scheduled,
	AlreadyPending,
}
