using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public sealed class PeriodicInstanceRegistrationService
{
	private readonly object _sync = new();
	private readonly HashSet<int> _openedRegistrations = [];

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

public enum PeriodicInstanceRegistrationBroadcastStatus
{
	Opened,
	AlreadyOpen,
	Closed,
	NotOpen,
}
