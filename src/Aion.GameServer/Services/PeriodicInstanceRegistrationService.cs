using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public sealed class PeriodicInstanceRegistrationService
{
	private readonly object _sync = new();
	private readonly HashSet<int> _openedRegistrations = [];
	private readonly Dictionary<int, PeriodicInstanceRegistrationCloseTaskIntent> _closeTaskIntentsByMaskId = [];

	public bool OpenRegistration(int maskId)
	{
		lock (_sync)
			return _openedRegistrations.Add(maskId);
	}

	public bool CloseRegistration(int maskId)
	{
		lock (_sync)
			return _openedRegistrations.Remove(maskId);
	}

	public bool IsRegistrationOpen(int maskId)
	{
		lock (_sync)
			return _openedRegistrations.Contains(maskId);
	}

	public bool HasCloseTaskIntent(int maskId)
	{
		lock (_sync)
			return _closeTaskIntentsByMaskId.ContainsKey(maskId);
	}

	public PeriodicInstanceRegistrationCloseTaskIntent? GetCloseTaskIntent(int maskId)
	{
		lock (_sync)
			return _closeTaskIntentsByMaskId.TryGetValue(maskId, out var intent) ? intent : null;
	}

	public static SmSystemMessage? CreateOpeningMessageForMaskId(int maskId)
	{
		// Java parity: PeriodicInstanceManager constructor passes these
		// SM_SYSTEM_MESSAGE helpers to scheduleRegistration for each mask id.
		return maskId switch
		{
			1 => SmSystemMessage.InstanceOpenIdab1Dredgion(),
			2 => SmSystemMessage.InstanceOpenIdDredgion02(),
			3 => SmSystemMessage.InstanceOpenIdDredgion03(),
			107 => SmSystemMessage.InstanceOpenIdKamar(),
			108 => SmSystemMessage.InstanceOpenIdLdf5Under01War(),
			109 => SmSystemMessage.InstanceOpenIdF5TdWar(),
			111 => SmSystemMessage.InstanceOpenIdLdf5FortressRe(),
			_ => null,
		};
	}

	public static PeriodicInstanceRegistrationSchedulePlan CreateDefaultSchedulePlan(bool autoGroupEnabled)
	{
		// Java parity: PeriodicInstanceManager constructor checks
		// AutoGroupConfig.AUTO_GROUP_ENABLE before scheduling registrations.
		if (!autoGroupEnabled)
			return new PeriodicInstanceRegistrationSchedulePlan(false, Array.Empty<PeriodicInstanceRegistrationScheduleEntry>());

		return new PeriodicInstanceRegistrationSchedulePlan(
			true,
			[
				CreateScheduleEntry(1, ["0 0 0,12,20 ? * *"], 60),
				CreateScheduleEntry(2, ["0 0 0,12,20 ? * *"], 60),
				CreateScheduleEntry(3, ["0 0 0,12,20 ? * *"], 60),
				CreateScheduleEntry(107, ["0 0 0,20 ? * MON,WED,SAT"], 60),
				CreateScheduleEntry(108, ["0 0 12,19 ? * *"], 60),
				CreateScheduleEntry(109, ["0 0 0,12 ? * SUN"], 60),
				CreateScheduleEntry(111, ["0 0 23 ? * *"], 60),
			]);
	}

	public PeriodicInstanceRegistrationBroadcastPlan CreateOpenRegistrationBroadcastPlan(
		int maskId,
		AutoGroupTable? autoGroups,
		IReadOnlyList<Player> players,
		SmSystemMessage? openingMessage = null)
	{
		lock (_sync)
		{
			if (!_openedRegistrations.Add(maskId))
				return PeriodicInstanceRegistrationBroadcastPlan.AlreadyOpen(maskId);
		}

		return CreateRegistrationBroadcastPlan(
			maskId,
			autoGroups,
			players,
			isClosed: false,
			status: PeriodicInstanceRegistrationBroadcastStatus.Opened,
			openingMessage);
	}

	public PeriodicInstanceRegistrationBroadcastPlan CreateScheduledOpenRegistrationBroadcastPlan(
		PeriodicInstanceRegistrationScheduleEntry scheduleEntry,
		AutoGroupTable? autoGroups,
		IReadOnlyList<Player> players)
	{
		lock (_sync)
		{
			if (!_openedRegistrations.Add(scheduleEntry.MaskId))
				return PeriodicInstanceRegistrationBroadcastPlan.AlreadyOpen(scheduleEntry.MaskId);

			_closeTaskIntentsByMaskId[scheduleEntry.MaskId] = new PeriodicInstanceRegistrationCloseTaskIntent(
				scheduleEntry.MaskId,
				scheduleEntry.CloseDelay);
		}

		return CreateRegistrationBroadcastPlan(
			scheduleEntry.MaskId,
			autoGroups,
			players,
			isClosed: false,
			status: PeriodicInstanceRegistrationBroadcastStatus.Opened,
			CreateOpeningMessageForMaskId(scheduleEntry.MaskId));
	}

	public async Task<PeriodicInstanceRegistrationBroadcastDispatchResult> OpenRegistrationAndBroadcastAsync(
		int maskId,
		AutoGroupTable? autoGroups,
		IGameClientConnectionRegistry connectionRegistry,
		SmSystemMessage? openingMessage = null,
		CancellationToken cancellationToken = default)
	{
		var players = GetOnlinePlayers(connectionRegistry);
		var plan = CreateOpenRegistrationBroadcastPlan(maskId, autoGroups, players, openingMessage);
		var sentPackets = await SendBroadcastPlanAsync(plan, connectionRegistry, cancellationToken);
		return new PeriodicInstanceRegistrationBroadcastDispatchResult(plan, sentPackets, StoppedRegistrationsByMaskId: false);
	}

	public PeriodicInstanceRegistrationBroadcastPlan CreateCloseRegistrationBroadcastPlan(
		int maskId,
		AutoGroupTable? autoGroups,
		IReadOnlyList<Player> players)
	{
		lock (_sync)
		{
			if (!_openedRegistrations.Remove(maskId))
				return PeriodicInstanceRegistrationBroadcastPlan.NotOpen(maskId);

			_closeTaskIntentsByMaskId.Remove(maskId);
		}

		return CreateRegistrationBroadcastPlan(
			maskId,
			autoGroups,
			players,
			isClosed: true,
			status: PeriodicInstanceRegistrationBroadcastStatus.Closed,
			openingMessage: null);
	}

	public async Task<PeriodicInstanceRegistrationBroadcastDispatchResult> CloseRegistrationAndBroadcastAsync(
		int maskId,
		AutoGroupTable? autoGroups,
		IGameClientConnectionRegistry connectionRegistry,
		Func<int, CancellationToken, ValueTask>? stopRegistrationsByMaskId = null,
		CancellationToken cancellationToken = default)
	{
		var players = GetOnlinePlayers(connectionRegistry);
		var plan = CreateCloseRegistrationBroadcastPlan(maskId, autoGroups, players);
		var sentPackets = await SendBroadcastPlanAsync(plan, connectionRegistry, cancellationToken);
		var stoppedRegistrations = false;
		if (plan.WouldStopRegistrationsByMaskId && stopRegistrationsByMaskId != null)
		{
			await stopRegistrationsByMaskId(maskId, cancellationToken);
			stoppedRegistrations = true;
		}

		return new PeriodicInstanceRegistrationBroadcastDispatchResult(plan, sentPackets, stoppedRegistrations);
	}

	public Task<PeriodicInstanceRegistrationBroadcastDispatchResult> CloseRegistrationAndBroadcastAsync(
		int maskId,
		AutoGroupTable? autoGroups,
		IGameClientConnectionRegistry connectionRegistry,
		AutoGroupLookingPartyRegistrationService lookingPartyRegistrations,
		CancellationToken cancellationToken = default)
	{
		return CloseRegistrationAndBroadcastAsync(
			maskId,
			autoGroups,
			connectionRegistry,
			async (closedMaskId, token) =>
			{
				await lookingPartyRegistrations.StopRegistrationsByMaskIdAsync(
					closedMaskId,
					autoGroups,
					connectionRegistry,
					token);
			},
			cancellationToken);
	}

	public IReadOnlyList<SmAutoGroup> CreateOpenRegistrationPackets(
		Player player,
		AutoGroupTable? autoGroups,
		InstanceCooltimeTable? instanceCooltimes,
		DateTimeOffset now)
	{
		if (autoGroups == null || instanceCooltimes == null)
			return Array.Empty<SmAutoGroup>();

		int[] openedMaskIds;
		lock (_sync)
			openedMaskIds = _openedRegistrations.ToArray();

		var packets = new List<SmAutoGroup>();
		foreach (var maskId in openedMaskIds)
		{
			var autoGroup = autoGroups.GetTemplateByInstanceMaskId(maskId);
			if (autoGroup == null)
				continue;
			if (player.Level < autoGroup.MinLevel || player.Level > autoGroup.MaxLevel)
				continue;
			if (PlayerPortalCooldownService.IsPortalUseDisabled(player, autoGroup.InstanceMapId, instanceCooltimes, now))
				continue;

			packets.Add(new SmAutoGroup(autoGroup, SmAutoGroup.EntryIconWindowId, close: false));
		}

		return packets;
	}

	public SmAutoGroup? CreateRequestPacket(Player player, int maskId, AutoGroupTable? autoGroups)
	{
		// Java parity: PeriodicInstanceManager.handleRequest only checks whether the
		// requested registration is open and whether the player is inside the level range.
		lock (_sync)
		{
			if (!_openedRegistrations.Contains(maskId))
				return null;
		}

		var autoGroup = autoGroups?.GetTemplateByInstanceMaskId(maskId);
		if (autoGroup == null)
			return null;
		if (player.Level < autoGroup.MinLevel || player.Level > autoGroup.MaxLevel)
			return null;

		return new SmAutoGroup(autoGroup);
	}

	private static PeriodicInstanceRegistrationBroadcastPlan CreateRegistrationBroadcastPlan(
		int maskId,
		AutoGroupTable? autoGroups,
		IReadOnlyList<Player> players,
		bool isClosed,
		PeriodicInstanceRegistrationBroadcastStatus status,
		SmSystemMessage? openingMessage)
	{
		// Java parity: PeriodicInstanceManager.broadcastRegistrationUpdate gets
		// AutoGroupType by mask id, filters only by player level, then sends
		// SM_AUTO_GROUP(maskId, WND_ENTRY_ICON, isClosed) and optional opening message.
		var autoGroup = autoGroups?.GetTemplateByInstanceMaskId(maskId);
		if (autoGroup == null)
		{
			return new PeriodicInstanceRegistrationBroadcastPlan(
				maskId,
				status,
				HasAutoGroupData: false,
				WouldStopRegistrationsByMaskId: isClosed,
				PlayerBroadcasts: Array.Empty<PeriodicInstanceRegistrationPlayerBroadcast>());
		}

		var broadcasts = new List<PeriodicInstanceRegistrationPlayerBroadcast>();
		foreach (var player in players)
		{
			if (player.Level < autoGroup.MinLevel || player.Level > autoGroup.MaxLevel)
				continue;

			var packets = new List<GameServerPacket>
			{
				new SmAutoGroup(autoGroup, SmAutoGroup.EntryIconWindowId, close: isClosed),
			};
			if (openingMessage != null)
				packets.Add(openingMessage);

			broadcasts.Add(new PeriodicInstanceRegistrationPlayerBroadcast(player.ObjectId, packets));
		}

		return new PeriodicInstanceRegistrationBroadcastPlan(
			maskId,
			status,
			HasAutoGroupData: true,
			WouldStopRegistrationsByMaskId: isClosed,
			PlayerBroadcasts: broadcasts);
	}

	private static IReadOnlyList<Player> GetOnlinePlayers(IGameClientConnectionRegistry connectionRegistry)
	{
		var players = new List<Player>();
		connectionRegistry.ForEachOnlinePlayer(players.Add);
		return players;
	}

	private static PeriodicInstanceRegistrationScheduleEntry CreateScheduleEntry(
		int maskId,
		IReadOnlyList<string> cronExpressions,
		long registrationPeriodMinutes)
	{
		var openingMessage = CreateOpeningMessageForMaskId(maskId)
			?? throw new InvalidOperationException($"Missing Java periodic registration opening message for mask {maskId}.");
		return new PeriodicInstanceRegistrationScheduleEntry(
			maskId,
			cronExpressions,
			registrationPeriodMinutes,
			TimeSpan.FromMinutes(registrationPeriodMinutes),
			openingMessage.MessageId);
	}

	private static async Task<int> SendBroadcastPlanAsync(
		PeriodicInstanceRegistrationBroadcastPlan plan,
		IGameClientConnectionRegistry connectionRegistry,
		CancellationToken cancellationToken)
	{
		var sentPackets = 0;
		foreach (var broadcast in plan.PlayerBroadcasts)
		{
			foreach (var packet in broadcast.Packets)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (await connectionRegistry.SendPacketToPlayerAsync(broadcast.PlayerObjectId, packet))
					sentPackets++;
			}
		}

		return sentPackets;
	}
}

public sealed record PeriodicInstanceRegistrationBroadcastPlan(
	int MaskId,
	PeriodicInstanceRegistrationBroadcastStatus Status,
	bool HasAutoGroupData,
	bool WouldStopRegistrationsByMaskId,
	IReadOnlyList<PeriodicInstanceRegistrationPlayerBroadcast> PlayerBroadcasts)
{
	public bool Changed => Status is PeriodicInstanceRegistrationBroadcastStatus.Opened
		or PeriodicInstanceRegistrationBroadcastStatus.Closed;

	public static PeriodicInstanceRegistrationBroadcastPlan AlreadyOpen(int maskId)
	{
		return new PeriodicInstanceRegistrationBroadcastPlan(
			maskId,
			PeriodicInstanceRegistrationBroadcastStatus.AlreadyOpen,
			HasAutoGroupData: false,
			WouldStopRegistrationsByMaskId: false,
			PlayerBroadcasts: Array.Empty<PeriodicInstanceRegistrationPlayerBroadcast>());
	}

	public static PeriodicInstanceRegistrationBroadcastPlan NotOpen(int maskId)
	{
		return new PeriodicInstanceRegistrationBroadcastPlan(
			maskId,
			PeriodicInstanceRegistrationBroadcastStatus.NotOpen,
			HasAutoGroupData: false,
			WouldStopRegistrationsByMaskId: false,
			PlayerBroadcasts: Array.Empty<PeriodicInstanceRegistrationPlayerBroadcast>());
	}
}

public sealed record PeriodicInstanceRegistrationPlayerBroadcast(
	int PlayerObjectId,
	IReadOnlyList<GameServerPacket> Packets);

public sealed record PeriodicInstanceRegistrationBroadcastDispatchResult(
	PeriodicInstanceRegistrationBroadcastPlan Plan,
	int SentPackets,
	bool StoppedRegistrationsByMaskId);

public sealed record PeriodicInstanceRegistrationSchedulePlan(
	bool AutoGroupEnabled,
	IReadOnlyList<PeriodicInstanceRegistrationScheduleEntry> Entries);

public sealed record PeriodicInstanceRegistrationScheduleEntry(
	int MaskId,
	IReadOnlyList<string> CronExpressions,
	long RegistrationPeriodMinutes,
	TimeSpan CloseDelay,
	int OpeningMessageId);

public sealed record PeriodicInstanceRegistrationCloseTaskIntent(
	int MaskId,
	TimeSpan Delay);

public enum PeriodicInstanceRegistrationBroadcastStatus
{
	Opened,
	AlreadyOpen,
	Closed,
	NotOpen,
}
